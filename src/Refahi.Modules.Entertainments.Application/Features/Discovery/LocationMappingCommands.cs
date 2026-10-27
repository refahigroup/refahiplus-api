using FluentValidation;
using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
namespace Refahi.Modules.Entertainments.Application.Features.Discovery;

public sealed record SetEntertainmentLocationMappingCommand(string Code, int CityId, Guid ActorId) : IRequest;
public sealed class SetEntertainmentLocationMappingValidator : AbstractValidator<SetEntertainmentLocationMappingCommand>
{
    public SetEntertainmentLocationMappingValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(200).WithMessage("کد موقعیت معتبر نیست");
        RuleFor(x => x.CityId).GreaterThan(0).WithMessage("شهر معتبر نیست");
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("هویت مدیر معتبر نیست");
    }
}
public sealed class SetEntertainmentLocationMappingHandler(IEntertainmentDiscovery discovery) : IRequestHandler<SetEntertainmentLocationMappingCommand>
{
    public async Task<Unit> Handle(SetEntertainmentLocationMappingCommand request, CancellationToken ct)
    { await discovery.SetMappingAsync(request.Code, request.CityId, request.ActorId, ct); return Unit.Value; }
}
