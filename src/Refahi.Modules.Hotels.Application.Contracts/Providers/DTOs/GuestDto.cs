namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

public sealed class GuestDto
{
    public string FullName { get; set; } = default!;
    public int Age { get; set; }
    public string Type { get; set; } = default!;
    public int? RoomIndex { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Nationality { get; set; }
}
