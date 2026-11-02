using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetProgram;
public sealed class GetEntertainmentProgramQueryHandler(IEntertainmentContentReader content) : IRequestHandler<GetEntertainmentProgramQuery, EntertainmentProgramDto?>
{
    public Task<EntertainmentProgramDto?> Handle(GetEntertainmentProgramQuery request, CancellationToken ct) => content.ProfileAsync(Guid.Parse(request.ProgramKey), ct);
}
