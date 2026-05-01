using MediatR;
using Refahi.Modules.Media.Application.Contracts.DTOs;

namespace Refahi.Modules.Media.Application.Contracts.Queries;

public sealed record GetMediaAssetQuery(Guid Id) : IRequest<MediaAssetDto?>;
