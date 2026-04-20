using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Modules;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Modules;

public sealed record GetModuleBySlugQuery(string Slug) : IRequest<ModuleDto?>;
