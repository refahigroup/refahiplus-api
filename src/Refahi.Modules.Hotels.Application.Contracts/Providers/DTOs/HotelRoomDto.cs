using System.Xml.Serialization;

namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

public sealed class HotelRoomDto
{
    public long RoomId { get; set; }
    public string RoomName { get; set; } = default!;
    public int Capacity { get; set; }
    public long CustomerPrice { get; set; }
    public string BoardType { get; set; } = default!;
    public string Description { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public List<string> Facilities { get; set; }


}

