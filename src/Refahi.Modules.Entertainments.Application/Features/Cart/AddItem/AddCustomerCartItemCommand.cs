using FluentValidation;
using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Domain;
namespace Refahi.Modules.Entertainments.Application.Features.Cart.AddItem;
public sealed record AddCustomerCartItemCommand(Guid UserId, string ProgramKey, string OfferKey, string PurchaseOptionKey, int Quantity, long ExpectedUnitPriceMinor) : IRequest<EntertainmentCartDto>;
public sealed class AddCustomerCartItemValidator : AbstractValidator<AddCustomerCartItemCommand>
{
    public AddCustomerCartItemValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("کاربر معتبر نیست");
        RuleFor(x => x.ProgramKey).Must(x => Guid.TryParse(x, out var id) && id != Guid.Empty).WithMessage("خدمت معتبر نیست");
        RuleFor(x => x.OfferKey).NotEmpty().MaximumLength(300).WithMessage("سانس معتبر نیست");
        RuleFor(x => x.PurchaseOptionKey).NotEmpty().MaximumLength(80).WithMessage("نوع بلیت معتبر نیست");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100).WithMessage("تعداد معتبر نیست");
        RuleFor(x => x.ExpectedUnitPriceMinor).GreaterThan(0).WithMessage("قیمت معتبر نیست");
    }
}
public sealed class AddCustomerCartItemHandler(IEntertainmentProgramIdentity identities, IMediator sender) : IRequestHandler<AddCustomerCartItemCommand, EntertainmentCartDto>
{
    public async Task<EntertainmentCartDto> Handle(AddCustomerCartItemCommand request, CancellationToken ct)
    {
        var programKey = Guid.Parse(request.ProgramKey);
        var identity = await identities.ResolveAsync(programKey, ct) ?? throw new EntertainmentDomainException("خدمت در دسترس نیست", "PROGRAM_UNAVAILABLE");
        return await sender.Send(new AddEntertainmentCartItemCommand(request.UserId, identity.ProviderKey, identity.SupplierId, programKey.ToString("N"), request.OfferKey, request.PurchaseOptionKey, request.Quantity, request.ExpectedUnitPriceMinor), ct);
    }
}
