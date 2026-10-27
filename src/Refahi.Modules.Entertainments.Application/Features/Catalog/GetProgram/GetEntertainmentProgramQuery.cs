using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;

namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetProgram;

public sealed record GetEntertainmentProgramQuery(
    string ProviderKey, 
    string ProgramKey
) : IRequest<EntertainmentProgramDto?>;

