using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class Review
{
    private Review() { }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid UserId { get; private set; }
    public int Rating { get; private set; }             // 1-5
    public string? Comment { get; private set; }
    public bool IsApproved { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Review Create(Guid productId, Guid userId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new StoreDomainException("امتیاز باید بین ۱ تا ۵ باشد", "INVALID_RATING");

        return new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UserId = userId,
            Rating = rating,
            Comment = comment,
            IsApproved = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Approve() { IsApproved = true; }
}
