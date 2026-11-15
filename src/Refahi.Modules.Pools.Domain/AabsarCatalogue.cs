namespace Refahi.Modules.Pools.Domain;

public sealed class AabsarVenue
{
    private AabsarVenue() { }
    public int Id { get; private set; }
    public int CityId { get; private set; }
    public int ProvinceId { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    public static AabsarVenue Create() => new() { Id = 1 };

    public void Publish(int cityId, int provinceId, DateTimeOffset now)
    {
        CityId = cityId;
        ProvinceId = provinceId;
        PublishedAt = now;
    }
}

public sealed class AabsarProduct
{
    private AabsarProduct() { }
    public Guid Id { get; private set; }
    public string EventId { get; private set; } = "";
    public string Title { get; private set; } = "";
    public bool IsActive { get; private set; }

    public static AabsarProduct Create(string eventId) => new() { Id = Guid.NewGuid(), EventId = eventId };
    public void Publish(string title) { Title = title; IsActive = true; }
    public void Deactivate() => IsActive = false;
}

public sealed class AabsarShowtime
{
    private AabsarShowtime() { }
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string ShowtimeId { get; private set; } = "";
    public string Title { get; private set; } = "";
    public DateTimeOffset StartsAt { get; private set; }
    public string? VendorName { get; private set; }
    public int Capacity { get; private set; }
    public long AdultPrice { get; private set; }
    public long ChildPrice { get; private set; }
    public long? AdultOldPrice { get; private set; }
    public long? ChildOldPrice { get; private set; }
    public bool IsActive { get; private set; }

    public static AabsarShowtime Create(string showtimeId, Guid productId)
        => new() { Id = Guid.NewGuid(), ShowtimeId = showtimeId, ProductId = productId };

    public void Publish(string title, DateTimeOffset startsAt, string? vendorName, int capacity,
        long adultPrice, long childPrice, long? adultOldPrice, long? childOldPrice)
    {
        Title = title;
        StartsAt = startsAt;
        VendorName = vendorName;
        Capacity = capacity;
        AdultPrice = adultPrice;
        ChildPrice = childPrice;
        AdultOldPrice = adultOldPrice;
        ChildOldPrice = childOldPrice;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
}
