using MediatR;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Contracts.Vouchers;

public sealed record VoucherSourceCountsDto(int Available, int Reserved, int Assigned, int Expired, int Disabled);

public sealed record VoucherSourceDto(
    Guid Id,
    Guid SupplierId,
    string Title,
    VoucherSourceType SourceType,
    VoucherRedemptionMode RedemptionMode,
    int? DefaultValidityDays,
    bool IsActive,
    VoucherSourceCountsDto Counts,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version);

public sealed record CreateVoucherSourceCommand(
    Guid ActorUserId, bool IsAdmin, Guid SupplierId, string Title,
    VoucherSourceType SourceType, VoucherRedemptionMode RedemptionMode,
    int? DefaultValidityDays) : IRequest<VoucherSourceDto>;

public sealed record UpdateVoucherSourceCommand(
    Guid ActorUserId, bool IsAdmin, Guid SourceId, string Title,
    VoucherRedemptionMode RedemptionMode, int? DefaultValidityDays,
    uint ExpectedVersion) : IRequest<VoucherSourceDto>;

public sealed record SetVoucherSourceActivationCommand(
    Guid ActorUserId, bool IsAdmin, Guid SourceId, bool IsActive,
    uint ExpectedVersion) : IRequest<VoucherSourceDto>;

public sealed record ListVoucherSourcesQuery(
    Guid ActorUserId, bool IsAdmin, Guid SupplierId, bool IncludeInactive)
    : IRequest<IReadOnlyList<VoucherSourceDto>>;

public sealed record GetVoucherSourceQuery(Guid ActorUserId, bool IsAdmin, Guid SourceId)
    : IRequest<VoucherSourceDto?>;

public sealed record VoucherCodeInput(int RowNumber, string Code, DateTimeOffset? ExpiresAtUtc);
public sealed record VoucherCodePreviewRowDto(int RowNumber, string MaskedCode, string Status, string? Error);
public sealed record VoucherCodePreviewDto(
    int TotalCount, int ValidCount, int DuplicateCount, int InvalidCount,
    IReadOnlyList<VoucherCodePreviewRowDto> Rows);

public sealed record PreviewVoucherCodesCommand(
    Guid ActorUserId, bool IsAdmin, Guid SourceId, IReadOnlyList<VoucherCodeInput> Codes)
    : IRequest<VoucherCodePreviewDto>;

public sealed record ImportVoucherCodesCommand(
    Guid ActorUserId, bool IsAdmin, Guid SourceId, string IdempotencyKey,
    IReadOnlyList<VoucherCodeInput> Codes) : IRequest<VoucherCodeImportResultDto>;

public sealed record VoucherCodeImportResultDto(
    Guid BatchId, int TotalCount, int AcceptedCount, int DuplicateCount,
    int RejectedCount, IReadOnlyList<VoucherCodePreviewRowDto> Rows);

public sealed record VoucherSourceCodeDto(
    Guid Id, string MaskedCode, string Status, DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? ExpiresAtUtc, uint Version);

public sealed record VoucherSourceCodePageDto(
    int Page, int PageSize, int Total, IReadOnlyList<VoucherSourceCodeDto> Items);

public sealed record GetVoucherSourceCodesQuery(
    Guid ActorUserId, bool IsAdmin, Guid SourceId, VoucherSourceCodeStatus? Status,
    int Page = 1, int PageSize = 50) : IRequest<VoucherSourceCodePageDto>;

public sealed record DisableVoucherSourceCodeCommand(
    Guid ActorUserId, bool IsAdmin, Guid SourceId, Guid CodeId, uint ExpectedVersion)
    : IRequest<VoucherSourceCodeDto>;

public sealed record SetProductVoucherSourceCommand(
    Guid ActorUserId, bool IsAdmin, Guid ProductId, Guid VoucherSourceId)
    : IRequest<Unit>;

public sealed record SetProductVariantVoucherSourceCommand(
    Guid ActorUserId, bool IsAdmin, Guid ProductId, Guid VariantId, Guid? VoucherSourceId)
    : IRequest<Unit>;
