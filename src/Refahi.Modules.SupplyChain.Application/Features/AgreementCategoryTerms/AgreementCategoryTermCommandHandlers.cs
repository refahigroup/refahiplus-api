using MediatR;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementCategoryTerms;

public sealed class AddAgreementCategoryTermCommandHandler
    : IRequestHandler<AddAgreementCategoryTermCommand, AddAgreementCategoryTermResponse>
{
    private readonly IAgreementRepository _repository;
    private readonly IMediator _mediator;

    public AddAgreementCategoryTermCommandHandler(
        IAgreementRepository repository,
        IMediator mediator
    ) => (_repository, _mediator) = (repository, mediator);

    public async Task<AddAgreementCategoryTermResponse> Handle(
        AddAgreementCategoryTermCommand request,
        CancellationToken cancellationToken
    )
    {
        await EnsureActiveCategoryAsync(request.CategoryId, cancellationToken);
        var agreement = await GetAgreementAsync(request.AgreementId, cancellationToken);
        var term = agreement.AddCategoryTerm(
            request.CategoryId,
            (SalesChannel)request.AllowedSalesChannels,
            request.CommissionPercent
        );

        _repository.AddCategoryTerm(term);
        await _repository.SaveChangesAsync(cancellationToken);
        return new AddAgreementCategoryTermResponse(term.Id);
    }

    private async Task EnsureActiveCategoryAsync(int categoryId, CancellationToken ct)
    {
        var category = await _mediator.Send(new GetCategoryByIdQuery(categoryId), ct);
        if (category is null || !category.IsActive)
            throw new SupplyChainDomainException("دسته‌بندی فعال یافت نشد", "CATEGORY_NOT_FOUND");
    }

    private async Task<Domain.Aggregates.Agreement> GetAgreementAsync(Guid id, CancellationToken ct)
    {
        var agreement = await _repository.GetByIdAsync(id, true, ct);
        if (agreement is null || agreement.IsDeleted)
            throw new SupplyChainDomainException("قرارداد یافت نشد", "AGREEMENT_NOT_FOUND");
        return agreement;
    }
}

public sealed class UpdateAgreementCategoryTermCommandHandler
    : IRequestHandler<UpdateAgreementCategoryTermCommand, Unit>
{
    private readonly IAgreementRepository _repository;
    private readonly IMediator _mediator;

    public UpdateAgreementCategoryTermCommandHandler(
        IAgreementRepository repository,
        IMediator mediator
    ) => (_repository, _mediator) = (repository, mediator);

    public async Task<Unit> Handle(
        UpdateAgreementCategoryTermCommand request,
        CancellationToken cancellationToken
    )
    {
        var category = await _mediator.Send(
            new GetCategoryByIdQuery(request.CategoryId),
            cancellationToken
        );
        if (category is null || !category.IsActive)
            throw new SupplyChainDomainException("دسته‌بندی فعال یافت نشد", "CATEGORY_NOT_FOUND");

        var agreement = await _repository.GetByIdAsync(
            request.AgreementId,
            true,
            cancellationToken
        );
        if (agreement is null || agreement.IsDeleted)
            throw new SupplyChainDomainException("قرارداد یافت نشد", "AGREEMENT_NOT_FOUND");

        agreement.UpdateCategoryTerm(
            request.TermId,
            request.CategoryId,
            (SalesChannel)request.AllowedSalesChannels,
            request.CommissionPercent
        );
        await _repository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class RemoveAgreementCategoryTermCommandHandler
    : IRequestHandler<RemoveAgreementCategoryTermCommand, Unit>
{
    private readonly IAgreementRepository _repository;

    public RemoveAgreementCategoryTermCommandHandler(IAgreementRepository repository) =>
        _repository = repository;

    public async Task<Unit> Handle(
        RemoveAgreementCategoryTermCommand request,
        CancellationToken cancellationToken
    )
    {
        var agreement = await _repository.GetByIdAsync(
            request.AgreementId,
            true,
            cancellationToken
        );
        if (agreement is null || agreement.IsDeleted)
            throw new SupplyChainDomainException("قرارداد یافت نشد", "AGREEMENT_NOT_FOUND");

        agreement.RemoveCategoryTerm(request.TermId);
        await _repository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
