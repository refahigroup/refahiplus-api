namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

public sealed class CityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public ProvinceDto Province { get; set; }

}

public class ProvinceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string NameEn { get; set; } = default!;
}
