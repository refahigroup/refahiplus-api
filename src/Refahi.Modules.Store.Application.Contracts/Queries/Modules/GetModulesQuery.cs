using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Modules;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Modules;

public sealed record GetModulesQuery(bool IncludeInactive = false) : IRequest<List<ModuleDto>>;
