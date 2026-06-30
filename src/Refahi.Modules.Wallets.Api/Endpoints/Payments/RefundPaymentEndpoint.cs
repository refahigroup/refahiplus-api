using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Api;
using Refahi.Modules.Wallets.Api.Models;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Shared.Presentation;
using System.Text.Json;

namespace Refahi.Modules.Wallets.Api.Endpoints.Payments;

public class RefundPaymentEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder)
            return;
    }
}

/// <summary>
/// Request body for refund endpoint.
/// </summary>
public sealed record RefundPaymentRequestBody(
    string? Reason,
    string? MetadataJson);
