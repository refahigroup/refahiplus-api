using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;

internal static class CanonicalRequestHasher
{
    public const short PaymentIntentVersion = 1;
    public const short RefundVersion = 1;

    public static byte[] HashPaymentIntent(
        Guid orderId,
        long amountMinor,
        string currency,
        Guid? payerOwnerId,
        Guid? destinationWalletId,
        IReadOnlyList<AllocationInput> allocations,
        IReadOnlyList<PaymentPostingInput> postings,
        IReadOnlyList<string?>? categoryCodes,
        string? metadataJson
    ) => Hash(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("kind", "wallet.payment-intent");
        writer.WriteNumber("version", PaymentIntentVersion);
        writer.WriteString("orderId", orderId);
        writer.WriteNumber("amountMinor", amountMinor);
        writer.WriteString("currency", currency.ToUpperInvariant());
        WriteNullableGuid(writer, "payerOwnerId", payerOwnerId);
        WriteNullableGuid(writer, "destinationWalletId", destinationWalletId);

        writer.WritePropertyName("allocations");
        writer.WriteStartArray();
        foreach (var allocation in allocations.OrderBy(x => x.WalletId).ThenBy(x => x.AmountMinor))
        {
            writer.WriteStartObject();
            writer.WriteString("walletId", allocation.WalletId);
            writer.WriteNumber("amountMinor", allocation.AmountMinor);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("postings");
        writer.WriteStartArray();
        foreach (var posting in postings
            .OrderBy(x => x.WalletId)
            .ThenBy(x => x.Direction)
            .ThenBy(x => x.AmountMinor)
            .ThenBy(x => x.Purpose, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("walletId", posting.WalletId);
            writer.WriteNumber("direction", posting.Direction);
            writer.WriteNumber("amountMinor", posting.AmountMinor);
            writer.WriteString("purpose", posting.Purpose);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("categoryCodes");
        if (categoryCodes is null)
            writer.WriteNullValue();
        else
        {
            writer.WriteStartArray();
            foreach (var code in categoryCodes
                .Select(NormalizeCategoryCode)
                .OrderBy(x => x, StringComparer.Ordinal))
            {
                if (code is null)
                    writer.WriteNullValue();
                else
                    writer.WriteStringValue(code);
            }
            writer.WriteEndArray();
        }

        WriteCanonicalJson(writer, "metadata", metadataJson);
        writer.WriteEndObject();
    });

    public static byte[] HashRefund(
        Guid paymentId,
        long amountMinor,
        string? reason,
        string? metadataJson
    ) => Hash(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("kind", "wallet.refund");
        writer.WriteNumber("version", RefundVersion);
        writer.WriteString("paymentId", paymentId);
        writer.WriteNumber("amountMinor", amountMinor);
        if (reason is null)
            writer.WriteNull("reason");
        else
            writer.WriteString("reason", reason);
        WriteCanonicalJson(writer, "metadata", metadataJson);
        writer.WriteEndObject();
    });

    private static byte[] Hash(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            write(writer);
            writer.Flush();
        }
        return SHA256.HashData(buffer.WrittenSpan);
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, string name, string? json)
    {
        writer.WritePropertyName(name);
        if (json is null)
        {
            writer.WriteNullValue();
            return;
        }

        using var document = JsonDocument.Parse(json);
        WriteElement(writer, document.RootElement);
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteElement(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(NormalizeJsonNumber(element.GetRawText()), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON token {element.ValueKind}.");
        }
    }

    private static string? NormalizeCategoryCode(string? value) =>
        value is null ? null : value.Trim().ToLowerInvariant();

    private static string NormalizeJsonNumber(string raw)
    {
        var negative = raw.Length > 0 && raw[0] == '-';
        var unsigned = negative ? raw[1..] : raw;
        var exponentIndex = unsigned.IndexOfAny(['e', 'E']);
        var significand = exponentIndex >= 0 ? unsigned[..exponentIndex] : unsigned;
        var exponent = exponentIndex >= 0
            ? int.Parse(unsigned[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
            : 0;
        var decimalIndex = significand.IndexOf('.');
        var fractionalDigits = decimalIndex >= 0 ? significand.Length - decimalIndex - 1 : 0;
        var digits = significand.Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0)
            return "0";

        exponent = checked(exponent - fractionalDigits);
        var trailingZeros = digits.Length - digits.TrimEnd('0').Length;
        if (trailingZeros > 0)
        {
            digits = digits[..^trailingZeros];
            exponent = checked(exponent + trailingZeros);
        }

        var sign = negative ? "-" : string.Empty;
        return exponent == 0 ? sign + digits : $"{sign}{digits}e{exponent}";
    }

    private static void WriteNullableGuid(Utf8JsonWriter writer, string name, Guid? value)
    {
        if (value.HasValue)
            writer.WriteString(name, value.Value);
        else
            writer.WriteNull(name);
    }
}
