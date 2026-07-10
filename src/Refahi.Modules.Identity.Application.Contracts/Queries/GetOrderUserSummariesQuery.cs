#nullable enable

using System;
using System.Collections.Generic;
using MediatR;

namespace Refahi.Modules.Identity.Application.Contracts.Queries;

public sealed record GetOrderUserSummariesQuery(
    IReadOnlyCollection<Guid>? UserIds = null,
    string? MobileNumber = null) : IRequest<IReadOnlyList<OrderUserSummaryDto>>;

public sealed record OrderUserSummaryDto(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? MobileNumber);

public static class MobileNumberSearchNormalizer
{
    public static bool TryNormalize(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var buffer = new char[value.Length];
        var length = 0;

        foreach (var character in value.Trim())
        {
            if (character is >= '0' and <= '9')
            {
                buffer[length++] = character;
            }
            else if (character is >= '۰' and <= '۹')
            {
                buffer[length++] = (char)(character - '۰' + '0');
            }
            else if (character is >= '٠' and <= '٩')
            {
                buffer[length++] = (char)(character - '٠' + '0');
            }
            else if (!char.IsWhiteSpace(character) && character != '-')
            {
                return false;
            }
        }

        normalized = length == 0 ? null : new string(buffer, 0, length);
        return true;
    }
}
