using MediatR;
using Refahi.Modules.Media.Application.Contracts.Commands;
using Refahi.Modules.Media.Domain.Repositories;

namespace Refahi.Modules.Media.Application.Features.Link;

public class LinkMediaToEntityCommandHandler : IRequestHandler<LinkMediaToEntityCommand, Unit>
{
    private readonly IMediaAssetRepository _repository;

    public LinkMediaToEntityCommandHandler(IMediaAssetRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(LinkMediaToEntityCommand request, CancellationToken ct)
    {
        var asset = await _repository.GetByIdAsync(request.MediaId, ct)
            ?? throw new KeyNotFoundException("فایل مدیا یافت نشد");

        if (asset.IsDeleted)
            throw new InvalidOperationException("فایل حذف شده است");

        if (!request.IsAdmin && asset.UploadedByUserId != request.RequestedByUserId)
            throw new UnauthorizedAccessException("اجازه ویرایش این فایل را ندارید");

        asset.LinkToEntity(request.EntityType, request.EntityId);
        await _repository.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
