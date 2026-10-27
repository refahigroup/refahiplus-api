using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetProgram;

public sealed class GetEntertainmentProgramQueryHandler(IEntertainmentProviderFactory providers) :
    IRequestHandler<GetEntertainmentProgramQuery, EntertainmentProgramDto?>
{
    public async Task<EntertainmentProgramDto?> Handle(GetEntertainmentProgramQuery request, CancellationToken ct)
    {
        return await providers.GetRequired(request.ProviderKey).GetProgramAsync(request.ProgramKey, ct);
    }
}
