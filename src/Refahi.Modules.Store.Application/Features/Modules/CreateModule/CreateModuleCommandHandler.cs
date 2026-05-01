using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Modules;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Modules.CreateModule;

public class CreateModuleCommandHandler : IRequestHandler<CreateModuleCommand, CreateModuleResponse>
{
    private readonly IStoreModuleRepository _moduleRepo;

    public CreateModuleCommandHandler(IStoreModuleRepository moduleRepo)
        => _moduleRepo = moduleRepo;

    public async Task<CreateModuleResponse> Handle(
        CreateModuleCommand request, CancellationToken cancellationToken)
    {
        if (await _moduleRepo.SlugExistsAsync(request.Slug.Trim().ToLower(), ct: cancellationToken))
            throw new StoreDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        var module = StoreModule.Create(
            request.Name,
            request.Slug,
            request.Description,
            request.IconUrl,
            request.SortOrder,
            request.CategoryId);

        await _moduleRepo.AddAsync(module, cancellationToken);

        return new CreateModuleResponse(module.Id, module.Name, module.Slug);
    }
}
