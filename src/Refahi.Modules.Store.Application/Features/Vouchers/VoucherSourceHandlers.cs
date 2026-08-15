using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Vouchers;

public sealed class VoucherSourceHandlers(
    IVoucherSourceRepository sources,
    IProductRepository products,
    IMediator mediator,
    IVoucherCodeProtector protector,
    TimeProvider clock)
    : IRequestHandler<CreateVoucherSourceCommand, VoucherSourceDto>,
      IRequestHandler<UpdateVoucherSourceCommand, VoucherSourceDto>,
      IRequestHandler<SetVoucherSourceActivationCommand, VoucherSourceDto>,
      IRequestHandler<ListVoucherSourcesQuery, IReadOnlyList<VoucherSourceDto>>,
      IRequestHandler<GetVoucherSourceQuery, VoucherSourceDto?>,
      IRequestHandler<PreviewVoucherCodesCommand, VoucherCodePreviewDto>,
      IRequestHandler<ImportVoucherCodesCommand, VoucherCodeImportResultDto>,
      IRequestHandler<GetVoucherSourceCodesQuery, VoucherSourceCodePageDto>,
      IRequestHandler<DisableVoucherSourceCodeCommand, VoucherSourceCodeDto>,
      IRequestHandler<SetProductVoucherSourceCommand, Unit>,
      IRequestHandler<SetProductVariantVoucherSourceCommand, Unit>
{
    public async Task<VoucherSourceDto> Handle(CreateVoucherSourceCommand r, CancellationToken ct)
    {
        await Authorize(r.ActorUserId, r.IsAdmin, r.SupplierId, ct);
        var value = VoucherSource.Create(r.SupplierId, r.Title, r.SourceType,
            r.RedemptionMode, r.DefaultValidityDays, clock.GetUtcNow());
        await sources.AddAsync(value, ct);
        return await Map(value, ct);
    }

    public async Task<VoucherSourceDto> Handle(UpdateVoucherSourceCommand r, CancellationToken ct)
    {
        var value = await Require(r.SourceId, ct);
        await Authorize(r.ActorUserId, r.IsAdmin, value.SupplierId, ct);
        EnsureVersion(value.Version, r.ExpectedVersion);
        value.Update(r.Title, r.RedemptionMode, r.DefaultValidityDays, clock.GetUtcNow());
        await sources.UpdateAsync(value, ct);
        return await Map(value, ct);
    }

    public async Task<VoucherSourceDto> Handle(SetVoucherSourceActivationCommand r, CancellationToken ct)
    {
        var value = await Require(r.SourceId, ct);
        await Authorize(r.ActorUserId, r.IsAdmin, value.SupplierId, ct);
        EnsureVersion(value.Version, r.ExpectedVersion);
        if (!r.IsActive && await sources.IsUsedByActiveCatalogAsync(value.Id, ct))
            throw new VoucherApplicationException("VOUCHER_SOURCE_IN_USE",
                "این منبع در محصول یا تنوع فعال استفاده می‌شود");
        if (r.IsActive) value.Activate(clock.GetUtcNow()); else value.Deactivate(clock.GetUtcNow());
        await sources.UpdateAsync(value, ct);
        return await Map(value, ct);
    }

    public async Task<IReadOnlyList<VoucherSourceDto>> Handle(ListVoucherSourcesQuery r, CancellationToken ct)
    {
        await Authorize(r.ActorUserId, r.IsAdmin, r.SupplierId, ct);
        var rows = await sources.GetBySupplierAsync(r.SupplierId, r.IncludeInactive, ct);
        var result = new List<VoucherSourceDto>(rows.Count);
        foreach (var row in rows) result.Add(await Map(row, ct));
        return result;
    }

    public async Task<VoucherSourceDto?> Handle(GetVoucherSourceQuery r, CancellationToken ct)
    {
        var value = await sources.GetByIdAsync(r.SourceId, ct);
        if (value is null) return null;
        await Authorize(r.ActorUserId, r.IsAdmin, value.SupplierId, ct);
        return await Map(value, ct);
    }

    public async Task<VoucherCodePreviewDto> Handle(PreviewVoucherCodesCommand r, CancellationToken ct)
    {
        var source = await RequirePreloaded(r.SourceId, ct);
        await Authorize(r.ActorUserId, r.IsAdmin, source.SupplierId, ct);
        return await Preview(source, r.Codes, ct);
    }

    public async Task<VoucherCodeImportResultDto> Handle(ImportVoucherCodesCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.IdempotencyKey))
            throw new VoucherApplicationException("IDEMPOTENCY_KEY_REQUIRED", "کلید یکتایی الزامی است");
        var source = await RequirePreloaded(r.SourceId, ct);
        await Authorize(r.ActorUserId, r.IsAdmin, source.SupplierId, ct);
        var fingerprint = Fingerprint(r.Codes);
        var existingBatch = await sources.GetImportBatchAsync(source.Id, r.IdempotencyKey.Trim(), ct);
        if (existingBatch is not null)
        {
            if (existingBatch.RequestFingerprint != fingerprint)
                throw new VoucherApplicationException("IDEMPOTENCY_PAYLOAD_MISMATCH",
                    "کلید یکتایی با اطلاعات متفاوتی استفاده شده است");
            return new(existingBatch.Id, existingBatch.TotalCount, existingBatch.AcceptedCount,
                existingBatch.DuplicateCount, existingBatch.RejectedCount, []);
        }
        var preview = await Preview(source, r.Codes, ct);
        var validRows = preview.Rows.Where(x => x.Status == "Valid").Select(x => x.RowNumber).ToHashSet();
        var now = clock.GetUtcNow();
        var codes = r.Codes.Where(x => validRows.Contains(x.RowNumber)).Select(x =>
        {
            var normalized = VoucherCode.Normalize(x.Code);
            return VoucherSourceCode.Register(source.Id, source.SupplierId,
                VoucherCode.Hash(normalized), protector.Protect(x.Code.Trim()), now, x.ExpiresAtUtc);
        }).ToArray();
        var batch = VoucherCodeImportBatch.Create(source.Id, source.SupplierId, r.ActorUserId,
            r.IdempotencyKey, fingerprint, preview.TotalCount, codes.Length,
            preview.DuplicateCount, preview.InvalidCount, now);
        await sources.AddCodesAsync(batch, codes, ct);
        return new(batch.Id, batch.TotalCount, batch.AcceptedCount, batch.DuplicateCount,
            batch.RejectedCount, preview.Rows);
    }

    public async Task<VoucherSourceCodePageDto> Handle(GetVoucherSourceCodesQuery r, CancellationToken ct)
    {
        var source = await RequirePreloaded(r.SourceId, ct);
        await Authorize(r.ActorUserId, r.IsAdmin, source.SupplierId, ct);
        var page = Math.Max(1, r.Page); var pageSize = Math.Clamp(r.PageSize, 1, 200);
        var now = clock.GetUtcNow();
        var rows = await sources.GetCodesAsync(source.Id, r.Status, page, pageSize, now, ct);
        return new(page, pageSize, rows.Total, rows.Items.Select(x => MapCode(x, now)).ToArray());
    }

    public async Task<VoucherSourceCodeDto> Handle(DisableVoucherSourceCodeCommand r, CancellationToken ct)
    {
        var source = await RequirePreloaded(r.SourceId, ct);
        await Authorize(r.ActorUserId, r.IsAdmin, source.SupplierId, ct);
        var code = await sources.GetCodeAsync(source.Id, r.CodeId, ct)
            ?? throw new VoucherApplicationException("VOUCHER_SOURCE_CODE_NOT_FOUND", "کد یافت نشد");
        EnsureVersion(code.Version, r.ExpectedVersion);
        code.Disable(); await sources.UpdateCodeAsync(code, ct);
        return MapCode(code, clock.GetUtcNow());
    }

    public async Task<Unit> Handle(SetProductVoucherSourceCommand r, CancellationToken ct)
    {
        var product = await products.GetByIdForAdminAsync(r.ProductId, ct)
            ?? throw new VoucherApplicationException("PRODUCT_NOT_FOUND", "محصول یافت نشد");
        await Authorize(r.ActorUserId, r.IsAdmin, product.SupplierId, ct);
        await EnsureSourceForProduct(r.VoucherSourceId, product.SupplierId, ct);
        product.SetVoucherSource(r.VoucherSourceId); await products.UpdateAsync(product, ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(SetProductVariantVoucherSourceCommand r, CancellationToken ct)
    {
        var product = await products.GetByIdForAdminAsync(r.ProductId, ct)
            ?? throw new VoucherApplicationException("PRODUCT_NOT_FOUND", "محصول یافت نشد");
        await Authorize(r.ActorUserId, r.IsAdmin, product.SupplierId, ct);
        if (r.VoucherSourceId.HasValue)
            await EnsureSourceForProduct(r.VoucherSourceId.Value, product.SupplierId, ct);
        product.SetVariantVoucherSource(r.VariantId, r.VoucherSourceId);
        await products.UpdateAsync(product, ct); return Unit.Value;
    }

    private async Task<VoucherCodePreviewDto> Preview(
        VoucherSource source, IReadOnlyList<VoucherCodeInput> input, CancellationToken ct)
    {
        if (input.Count is 0 or > 5000)
            throw new VoucherApplicationException("INVALID_VOUCHER_IMPORT_SIZE",
                "تعداد کدها باید بین یک تا پنج هزار باشد");
        var now = clock.GetUtcNow();
        var normalized = new Dictionary<int, (string Value, string Hash)>();
        var rows = new List<VoucherCodePreviewRowDto>(input.Count);
        foreach (var row in input)
        {
            try
            {
                var value = VoucherCode.Normalize(row.Code);
                if (row.ExpiresAtUtc.HasValue && row.ExpiresAtUtc <= now)
                    throw new VoucherApplicationException("INVALID_VOUCHER_CODE_EXPIRY", "تاریخ انقضا باید در آینده باشد");
                normalized[row.RowNumber] = (value, VoucherCode.Hash(value));
            }
            catch (Exception ex) when (ex is VoucherApplicationException or StoreDomainException)
            {
                rows.Add(new(row.RowNumber, Mask(row.Code), "Invalid", ex.Message));
            }
        }
        var duplicateRows = normalized.GroupBy(x => x.Value.Hash)
            .Where(x => x.Count() > 1).SelectMany(x => x.Select(y => y.Key)).ToHashSet();
        var existing = await sources.GetExistingHashesAsync(source.SupplierId,
            normalized.Values.Select(x => x.Hash).Distinct().ToArray(), ct);
        foreach (var row in input.Where(x => normalized.ContainsKey(x.RowNumber)))
        {
            var hash = normalized[row.RowNumber].Hash;
            if (duplicateRows.Contains(row.RowNumber) || existing.Contains(hash))
                rows.Add(new(row.RowNumber, Mask(row.Code), "Duplicate", "کد تکراری است"));
            else rows.Add(new(row.RowNumber, Mask(row.Code), "Valid", null));
        }
        rows.Sort((a, b) => a.RowNumber.CompareTo(b.RowNumber));
        return new(rows.Count, rows.Count(x => x.Status == "Valid"),
            rows.Count(x => x.Status == "Duplicate"), rows.Count(x => x.Status == "Invalid"), rows);
    }

    private async Task Authorize(Guid actor, bool admin, Guid supplierId, CancellationToken ct)
    {
        if (admin) return;
        if (!await mediator.Send(new AuthorizeStoreResourceQuery(actor, supplierId, null,
                StorePermissions.ManageCatalog), ct))
            throw new VoucherApplicationException("VOUCHER_SOURCE_FORBIDDEN", "دسترسی مدیریت منبع ووچر را ندارید");
    }

    private async Task<VoucherSource> Require(Guid id, CancellationToken ct) =>
        await sources.GetByIdAsync(id, ct)
        ?? throw new VoucherApplicationException("VOUCHER_SOURCE_NOT_FOUND", "منبع ووچر یافت نشد");

    private async Task<VoucherSource> RequirePreloaded(Guid id, CancellationToken ct)
    {
        var source = await Require(id, ct);
        if (source.SourceType != VoucherSourceType.Preloaded)
            throw new VoucherApplicationException("VOUCHER_SOURCE_NOT_PRELOADED", "این منبع دارای فهرست کد نیست");
        return source;
    }

    private async Task EnsureSourceForProduct(Guid id, Guid supplierId, CancellationToken ct)
    {
        var source = await Require(id, ct);
        if (!source.IsActive || source.SupplierId != supplierId)
            throw new VoucherApplicationException("INVALID_PRODUCT_VOUCHER_SOURCE", "منبع ووچر محصول معتبر نیست");
    }

    private async Task<VoucherSourceDto> Map(VoucherSource x, CancellationToken ct)
    {
        var c = await sources.GetCountsAsync(x.Id, clock.GetUtcNow(), ct);
        return new(x.Id, x.SupplierId, x.Title, x.SourceType, x.RedemptionMode,
            x.DefaultValidityDays, x.IsActive, new(c.Available, c.Reserved, c.Assigned, c.Expired, c.Disabled),
            x.CreatedAtUtc, x.UpdatedAtUtc, x.Version);
    }

    private static VoucherSourceCodeDto MapCode(VoucherSourceCode x, DateTimeOffset now) =>
        new(x.Id, $"****{x.CodeHash[^4..]}",
            x.Status == VoucherSourceCodeStatus.Available && x.ExpiresAtUtc <= now ? "Expired" : x.Status.ToString(),
            x.RegisteredAtUtc, x.ExpiresAtUtc, x.Version);

    private static string Mask(string? code)
    {
        var x = code?.Trim() ?? string.Empty;
        return x.Length <= 4 ? "****" : $"****{x[^4..]}";
    }

    private static string Fingerprint(IReadOnlyList<VoucherCodeInput> rows) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            rows.OrderBy(x => x.RowNumber).Select(x => new { x.RowNumber, Code = x.Code.Trim(), x.ExpiresAtUtc })))));

    private static void EnsureVersion(uint actual, uint expected)
    {
        if (actual != expected)
            throw new VoucherApplicationException("CONCURRENCY_CONFLICT", "اطلاعات تغییر کرده است؛ صفحه را تازه‌سازی کنید");
    }
}
