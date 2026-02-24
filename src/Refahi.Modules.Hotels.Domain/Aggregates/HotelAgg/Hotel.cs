using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Refahi.Modules.Hotels.Domain.Aggregates.HotelAgg;

public sealed class Hotel
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string TitleEn { get; set; }
    public string AccommodationType { get; set; } // Hotel
    public string AccommodationTitle { get; set; } // هتل
    public string Description { get; set; }
    public short Stars { get; set; }
    public string Address { get; set; }
    public bool IsMarketplace { get; set; } = false;
    public bool Enable { get; set; } = true;


    public HotelCover Cover { get; set; }
    public HotelReview Reviews { get; set; }
    public City City { get; set; }
    public Location Location { get; set; }
    public List<HotelFacility> Facilities { get; set; }
    public HotelPocilities Policies { get; set; }
    public List<HotelGalleryItem> Gallery { get; set; }
}

public class HotelReview
{
    public float Ratings { get; set; }
    public int Reviews { get; set; }
}

public class HotelCover
{
    public string Url { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
}

public class City
{
    public int Id { get; set; }
    public string Title { get; set; }
}

public class State
{
    public int Id { get; set; }
    public string Title { get; set; }
}

public class Location
{
    public long Lat { get; set; }
    public long Lon { get; set; }
}

public class HotelFacility
{
    public int Id { get; set; }
    public string Icon { get; set; }
    public string Title { get; set; }
}

public class HotelPocilities
{
    public int ChildAge { get; set; }
    public int InfantAge { get; set; }
    public string CheckInIime { get; set; }
    public string CheckOutIime { get; set; }
    public bool ForeignersFee { get; set; }
    public string Cancellation { get; set; }
    public string[] FreeTransfers { get; set; }
    public string FreeTransferPolicy { get; set; }
}

public class HotelGalleryItem
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Url { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
}
