using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Client;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Exceptions;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;

public sealed class CinemaProviderSettings
{
    public string[] CinemaCategories { get; set; } = [];
    public string[] TheaterCategories { get; set; } = [];
    public string[] ArtCategories { get; set; } = [];
    public string BoxOfficeCurrency { get; set; } = "IRT";
    public string BannerPlacement { get; set; } = "home-1";
    public string[] ReservedStatuses { get; set; } = ["reserved"];
    public string[] ConfirmedStatuses { get; set; } = ["confirmed"];
    public string[] CancelledStatuses { get; set; } = ["cancelled", "canceled"];
}
public sealed class CinemaProviderFactory(IEnumerable<ICinemaProvider> providers) : ICinemaProviderFactory
{
    public ICinemaProvider Get(string key) => providers.SingleOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
        ?? throw new CinemaException("تأمین‌کننده سینما تنظیم نشده است", 503);
}
public sealed class ITicketCinemaProvider(IiTicketClient client, IOptions<CinemaProviderSettings> settings,
    IOptions<iTicketOptions> connection, IMemoryCache cache, ILogger<ITicketCinemaProvider> logger) : ICinemaProvider
{
    // Shared across request-scoped provider instances; striped locks coalesce enrichment reads.
    private static readonly SemaphoreSlim EnrichmentConcurrency = new(4);
    private static readonly SemaphoreSlim[] EnrichmentLocks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();
    public string Key => "iticket";
    public async Task<IReadOnlyList<CinemaCity>> CitiesAsync(CancellationToken ct)
        => Items(await Read("provinces",ct,true)).SelectMany(p=>Items(P(p,"cities")).Select(c=>new CinemaCity((int)N(c,"id"),S(c,"name"),S(p,"name")))).ToArray();
    private CinemaProviderSettings Settings => settings.Value;
    public bool IsReserved(string status) => Settings.ReservedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
    public bool IsConfirmed(string status) => Settings.ConfirmedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
    public bool IsCancelled(string status) => Settings.CancelledStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
    private async Task<JsonElement> Read(string path, CancellationToken ct, bool cached = false)
    {
        var key = $"cinema:{connection.Value.BaseUrl}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(connection.Value.AccessToken)))}:{path}";
        if (cached && cache.TryGetValue<JsonElement>(key, out var found)) return found;
        JsonElement result = default;
        for (var attempt = 0; ; attempt++)
        {
            try { result = await client.GetDocumentAsync(path, ct); break; }
            catch (Exception ex) when (attempt < 2 && (ex is ITicketTransportException || ex is ITicketApiException api && (int)api.StatusCode >= 500))
            { await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), ct); }
            catch(ITicketNotFoundException){throw new CinemaException("نمایش یا سانس موردنظر یافت نشد",404);}
            catch(ITicketValidationException){throw new CinemaException("فیلتر یا شناسه درخواست معتبر نیست",400);}
            catch(ITicketException){throw new CinemaException("ارتباط با تأمین‌کننده سینما برقرار نشد",503);}
        }
        if (cached) cache.Set(key, result, TimeSpan.FromMinutes(5));
        return result;
    }
    private async Task<JsonElement> Mutate(string path, object? body, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        try { return await client.PostDocumentAsync(path, body, ct); }
        catch (ITicketValidationException) { throw new CinemaException("درخواست توسط تأمین‌کننده پذیرفته نشد؛ وضعیت صندلی و رزرو را بررسی کنید"); }
        catch (ITicketApiException ex) when ((int)ex.StatusCode < 500)
        { throw new CinemaException("عملیات تأمین‌کننده پذیرفته نشد"); }
        catch (Exception ex) when (ex is ITicketException or OperationCanceledException)
        { throw new CinemaProviderAmbiguousException(ex); }
        finally { logger.LogInformation("Cinema provider operation {Operation} completed in {LatencyMs}ms", path.Split('/').Last(), started.ElapsedMilliseconds); }
    }
    public async Task<CinemaLanding> LandingAsync(int? city, CancellationToken ct)
    {
        var cinema = await ShowsAsync("cinema", city, null, 1, ct);
        var theater = await ShowsAsync("theater", city, null, 1, ct);
        var art = await ShowsAsync("art", city, null, 1, ct);
        var banners = Items(await Read("marketing/banners?placement=" + E(Settings.BannerPlacement), ct, true))
            .Select(x => A(x)).OrderBy(a => N(a,"sort_order"))
            .Select(a => new CinemaBanner(S(a,"title"), Image(a,"image"), SafeLink(P(a,"link"))))
            .Where(x => x.ImageUrl.Length > 0).ToArray();
        logger.LogInformation("Cinema landing received {BannerCount} valid banners for placement {Placement}", banners.Length, Settings.BannerPlacement);
        var rankings = Items(await Read("schedules/box-office?limit=6",ct,true)).Select((x,i) =>
        {
            if (x.ValueKind == JsonValueKind.String)
            {
                try { x = JsonDocument.Parse(x.GetString()!).RootElement.Clone(); } catch (JsonException) { return null; }
            }
            var a = A(x); var id = S(a,"show_id",S(x,"id"));
            var amount=NullableNumber(a,"final_price")??NullableNumber(a,"total_amount");
            var currency=S(a,"currency_code",Settings.BoxOfficeCurrency);
            return new CinemaRanking(i+1,id,S(a,"title",S(a,"show_title")),amount.HasValue?Money(amount.Value,currency):null);
        }).Where(x => x != null && x.Title.Length > 0).Cast<CinemaRanking>().ToArray();
        return new(banners,cinema.Items,theater.Items,art.Items,rankings);
    }
    public async Task<CinemaCatalog> ShowsAsync(string kind, int? city, string? search, int page, CancellationToken ct)
    {
        var categories = kind switch { "theater" => Settings.TheaterCategories, "art" => Settings.ArtCategories, _ => Settings.CinemaCategories };
        if (categories.Length == 0)
        {
            logger.LogWarning("Cinema catalog section {Kind} has no configured provider categories", kind);
            return new([],page,1);
        }
        var path = $"shows?page={page}&per_page=24&sort=-display_priority&only_with_active_sessions=true";
        foreach (var category in categories) path += "&category[]=" + E(category);
        if (city.HasValue) path += "&city[]=" + city;
        if (!string.IsNullOrWhiteSpace(search)) path += "&search=" + E(search);
        var root = await Read(path,ct,true);
        return new(Items(root).Select(x => MapShow(x,kind)).ToArray(),page,(int)N(P(root,"meta"),"last_page",1));
    }
    public async Task<CinemaShow> ShowAsync(string id, CancellationToken ct)
    {
        var show = MapShow(Resource(await Read("shows/"+E(id),ct,true)));
        var artists = await Task.WhenAll((show.ArtistDetails ?? []).Select(async artist =>
        {
            if (!string.IsNullOrWhiteSpace(artist.Portrait) || string.IsNullOrWhiteSpace(artist.Id)) return artist;
            var detail = await OptionalDetail("show-artists/" + E(artist.Id), ct);
            if (detail is not JsonElement value) return artist;
            var mapped = MapArtist(Resource(value));
            return artist with { Portrait = mapped.Portrait, Name = artist.Name.Length > 0 ? artist.Name : mapped.Name };
        }));
        return show with { ArtistDetails = artists.Where(x=>!string.IsNullOrWhiteSpace(x.Name)).ToArray(), Artists = artists.Where(x=>!string.IsNullOrWhiteSpace(x.Name)).Select(x => x.Name).ToArray() };
    }
    private CinemaShow MapShow(JsonElement x, string? forcedKind = null)
    {
        x=Resource(x);var a=A(x); var cats=Items(P(a,"categories")).Select(c=>S(c,"id")).ToArray();
        var kind=forcedKind ?? (cats.Intersect(Settings.TheaterCategories).Any()?"theater":cats.Intersect(Settings.ArtCategories).Any()?"art":"cinema");
        return new(S(x,"id"),S(a,"title"),kind,Image(P(a,"media"),"poster"),Image(P(a,"media"),"banner"),
            S(a,"summary"),S(a,"description"),(int?)NullableNumber(a,"duration_minutes"),S(a,"age_group"),
            Items(P(a,"artists")).Select(MapArtist).Where(y=>y.Name.Length>0).Select(y=>y.Name).ToArray(),
            Items(P(a,"genres")).Select(g=>new CinemaGenre(S(g,"id"), S(A(g),"name"))).Where(g=>g.Name.Length>0).ToArray(),
            Items(P(a,"artists")).Select(MapArtist).Where(y=>y.Name.Length>0 || y.Id.Length>0).ToArray(),
            CinemaHtml.Sanitize(S(a,"summary")), CinemaHtml.Sanitize(S(a,"description")), CinemaHtml.PlainText(S(a,"summary")));
    }
    public async Task<IReadOnlyList<CinemaDisplayDay>> PlacesAsync(string show, int? city, string? search, CancellationToken ct)
    {
        var path="schedules/shows/"+E(show)+"/places?days=14";
        if(city.HasValue)path+="&city[]="+city;
        if(!string.IsNullOrWhiteSpace(search))path+="&search="+E(search);
        var a=A(Resource(await Read(path,ct,true)));
        var fullMediaIds = Items(P(a,"dates")).SelectMany(d=>Items(P(d,"places")))
            .Where(p=>PlaceCover(P(A(Resource(p)),"media")).Length>0).Select(p=>S(Resource(p),"id")).ToHashSet();
        var days = Items(P(a,"dates")).Select(d=>new CinemaDisplayDay(S(d,"date"),S(d,"weekday"),S(d,"jalali_day_month"),
            Items(P(d,"places")).Select(MapPlace).ToArray())).ToArray();
        var places = await Task.WhenAll(days.SelectMany(d=>d.Places).DistinctBy(p=>p.Id).Select(async place =>
        {
            if (fullMediaIds.Contains(place.Id)) return place;
            var detail = await OptionalDetail("places/" + E(place.Id), ct);
            return detail is JsonElement value ? place with { Cover = MapPlace(Resource(value)).Cover ?? place.Cover } : place;
        }));
        var byId = places.ToDictionary(p=>p.Id);
        return days.Select(d=>d with { Places = d.Places.Select(p=>byId[p.Id]).ToArray() }).ToArray();
    }
    public async Task<IReadOnlyList<CinemaSession>> SessionsAsync(string show,string place,string? date,CancellationToken ct)
    {
        var path=$"schedules/shows/{E(show)}/places/{E(place)}/sessions"+(date==null?"":"?date="+E(date));
        return Items(await Read(path,ct,true)).Select(MapSession).ToArray();
    }
    private static CinemaSession MapSession(JsonElement x)
    {
        var a=A(x); var currency=S(a,"currency_code");
        return new(S(x,"id"),S(a,"hall_name"),Date(a,"starts_at")??throw new CinemaException("زمان سانس معتبر نیست",502),
            Date(a,"purchase_end_at"),S(a,"status"),(int)N(a,"available_count"),
            NullableNumber(a,"price") is long price?Money(price,currency):null,
            Items(P(a,"prices")).Select(x=>Money(Number(x),currency)).ToArray(),S(a,"session_label"));
    }
    public async Task<CinemaSeatMap> SeatsAsync(string schedule,CancellationToken ct)
    {
        var a=A(Resource(await Read("schedules/"+E(schedule)+"/seats",ct)));
        var schema=P(a,"schema"); var columns=Items(P(schema,"seat")).Select(x=>x.GetString()??"").ToArray();
        var statuses=Items(P(schema,"status")).Select(x=>x.GetString()??"").ToArray();
        if(new[]{"id","grid_row","grid_column","seat_number","position_x","position_y","is_bookable","price","status"}.Any(name=>!columns.Contains(name)))
            throw new CinemaException("ساختار نقشه صندلی معتبر نیست",502);
        var seats=new List<CinemaSeat>(); var currency=S(a,"currency_code");
        foreach(var block in Items(P(a,"blocks")))
        foreach(var tuple in Items(P(block,"seats")))
        {
            var values=Items(tuple).ToArray();
            JsonElement V(string name) { var i=Array.IndexOf(columns,name);return i>=0&&i<values.Length?values[i]:default; }
            var statusIndex=StatusIndex(V("status")); var status=statusIndex>=0&&statusIndex<statuses.Length?statuses[statusIndex]:"blocked";
            var row=Text(V("grid_row"));
            var label=Items(P(block,"row_labels")).FirstOrDefault(r=>S(r,"grid_row")==row);
            row=S(label,"label",row);
            seats.Add(new(Text(V("id")),S(block,"id"),row,Text(V("seat_number")),Double(V("position_x")),Double(V("position_y")),
                Money(Number(V("price")),currency),status,Number(V("is_bookable"))==1 && status is "available" or "reserved" or "sold" && values.Length==columns.Length && Text(V("id")).Length==26,Text(V("accessibility_flags"))));
        }
        var sessions=Items(P(a,"sessions")).SelectMany(d=>Items(P(d,"schedules"))).ToArray();
        var selected=sessions.FirstOrDefault(s=>S(s,"id")==schedule);
        if(selected.ValueKind==JsonValueKind.Undefined)throw new CinemaException("سانس موردنظر یافت نشد",404);
        var session=MapSession(selected); var show=MapShow(P(a,"show"));
        return new(schedule,show,MapPlace(P(a,"place")),session.Hall,session.StartsAt,session.PurchaseEndAt,seats,S(A(selected),"hall_id",S(a,"hall_id")));
    }
    public async Task<CinemaSeatStatuses> StatusAsync(string schedule,CancellationToken ct)
    {
        var a=A(Resource(await Read("schedules/"+E(schedule)+"/seats/status",ct)));
        var names=Items(P(P(a,"schema"),"status")).Select(Text).ToArray();var result=new Dictionary<string,string>();
        var seats=P(a,"seats");if(seats.ValueKind==JsonValueKind.Object)foreach(var p in seats.EnumerateObject())
        {var i=StatusIndex(p.Value);result[p.Name]=i>=0&&i<names.Length?names[i]:"blocked";}
        return new(result);
    }
    public async Task<CinemaReservation> ReserveAsync(string schedule,IReadOnlyList<string> seats,CinemaCustomer customer,CancellationToken ct)
        => MutationReservation(await Mutate("reseller/orders/reserve",new{data=new{type="reseller-orders",attributes=new{schedule_id=schedule,seat_ids=seats,customer=new{mobile=customer.Mobile,first_name=customer.Name}}}},ct));
    public async Task<CinemaReservation> GetReservationAsync(string id,CancellationToken ct)=>Reservation(await Read("reseller/orders/"+E(id),ct));
    public async Task<CinemaReservation> ConfirmAsync(string id,CancellationToken ct)=>MutationReservation(await Mutate("reseller/orders/"+E(id)+"/confirm",null,ct));
    public async Task CancelAsync(string id,CancellationToken ct)
    {
        var a=A(Resource(await Mutate("reseller/orders/"+E(id)+"/cancel",null,ct)));
        if(!IsCancelled(S(a,"status")))throw new CinemaProviderAmbiguousException(new InvalidOperationException("Unexpected cancel status"));
    }
    private static CinemaReservation MutationReservation(JsonElement root)
    {
        try{return Reservation(root);}
        catch(Exception ex)when(ex is CinemaException or JsonException or OverflowException)
        {throw new CinemaProviderAmbiguousException(ex,S(Resource(root),"id"));}
    }
    private static CinemaReservation Reservation(JsonElement root)
    {
        var x=Resource(root);var a=A(x);var c=S(a,"currency_code");
        return new(S(x,"id"),S(a,"code"),S(a,"status"),Date(a,"expires_at"),Money(N(a,"subtotal_amount"),c),Money(N(a,"discount_amount"),c),
            Money(N(a,"platform_amount"),c),Money(N(a,"tax_amount"),c),Money(N(a,"total_amount"),c),S(a,"samfa_code"),NullableNumber(a,"samfa_id"));
    }
    public static long Money(long amount,string currency)=>currency switch
    { "IRT"=>checked(amount*10),"IRR"=>amount,_=>throw new CinemaException("واحد مبلغ تأمین‌کننده پشتیبانی نمی‌شود",502) };
    private static CinemaPlace MapPlace(JsonElement x)
    {
        x=Resource(x); var a=A(x); var media=P(a,"media");
        var cover=PlaceCover(media);
        if (cover.Length==0) cover=Image(media,"logo");
        return new(S(x,"id"),S(a,"title",S(a,"name")),S(a,"address"),S(P(a,"city"),"name",S(a,"city_name")),cover.Length>0 ? cover : null);
    }
    private static string PlaceCover(JsonElement media)
    {
        var primary=Image(media,"primary_media");
        return primary.Length>0 ? primary : Items(P(media,"gallery")).Select(ImageValue).FirstOrDefault(v=>v.Length>0) ?? "";
    }
    private static CinemaArtist MapArtist(JsonElement x)
    {
        x=Resource(x); var a=A(x); var portrait=Image(a,"portrait");
        return new(S(x,"id"),S(a,"name",S(a,"first_name")+" "+S(a,"last_name")).Trim(),portrait.Length>0 ? portrait : null);
    }
    private static string? SafeLink(JsonElement link)
    {
        var value=S(link,"url");
        if (value.StartsWith('/') && !value.StartsWith("//") && !value.Contains('\\')) return value;
        return Uri.TryCreate(value,UriKind.Absolute,out var uri) && uri.Scheme is "https" or "http" ? value : null;
    }
    private async Task<JsonElement?> OptionalDetail(string path, CancellationToken ct)
    {
        var scope = connection.Value.BaseUrl + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(connection.Value.AccessToken))) + ":" + path;
        var key = "cinema:detail:" + scope;
        var gate=EnrichmentLocks[(StringComparer.Ordinal.GetHashCode(scope) & int.MaxValue) % EnrichmentLocks.Length];
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue<JsonElement?>(key,out var cached)) return cached;
            await EnrichmentConcurrency.WaitAsync(ct);
            try
            {
                JsonElement? detail;
                try { detail=await Read(path,ct,true); }
                catch (Exception ex) when (ex is CinemaException or JsonException) { detail=null; logger.LogWarning("Cinema optional media unavailable for {Resource}",path); }
                cache.Set(key,detail,TimeSpan.FromMinutes(5));
                return detail;
            }
            finally { EnrichmentConcurrency.Release(); }
        }
        finally { gate.Release(); }
    }
    private static string Image(JsonElement root,string field)
    {
        return ImageValue(P(root,field));
    }
    private static string ImageValue(JsonElement v)
    {
        if(v.ValueKind==JsonValueKind.String)return SafeImage(v.GetString());
        if(v.ValueKind==JsonValueKind.Object)foreach(var key in new[]{"full","poster","banner","original","url","thumbnail_card","large","desktop","thumbnail","avatar"}) {var s=S(v,key);if(SafeImage(s).Length>0)return SafeImage(s);}
        return "";
    }
    private static string SafeImage(string? value) => Uri.TryCreate(value,UriKind.Absolute,out var uri) && uri.Scheme is "https" or "http" ? value! : "";
    private static string E(string value)=>Uri.EscapeDataString(value);
    private static JsonElement Resource(JsonElement x)=>x.ValueKind==JsonValueKind.Object&&x.TryGetProperty("data",out var d)?d:x;
    private static JsonElement A(JsonElement x)=>x.ValueKind==JsonValueKind.Object&&x.TryGetProperty("attributes",out var a)?a:x;
    private static JsonElement P(JsonElement x,string name)=>x.ValueKind==JsonValueKind.Object&&x.TryGetProperty(name,out var p)?p:default;
    private static string S(JsonElement x,string name,string fallback="")=>P(x,name).ValueKind is JsonValueKind.Null or JsonValueKind.Undefined?fallback:Text(P(x,name));
    private static IEnumerable<JsonElement> Items(JsonElement x) {x=Resource(x);return x.ValueKind==JsonValueKind.Array?x.EnumerateArray().ToArray():[];}
    private static string Text(JsonElement x)=>x.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined?"":x.ValueKind==JsonValueKind.String?x.GetString()??"":x.ToString();
    private static long Number(JsonElement x)=>long.TryParse(Text(x),NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)?n:0;
    private static int StatusIndex(JsonElement x)=>int.TryParse(Text(x),NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)?n:-1;
    private static double Double(JsonElement x)=>double.TryParse(Text(x),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:0;
    private static long N(JsonElement x,string name,long fallback=0)=>P(x,name).ValueKind is JsonValueKind.Null or JsonValueKind.Undefined?fallback:Number(P(x,name));
    private static long? NullableNumber(JsonElement x,string name)=>P(x,name).ValueKind is JsonValueKind.Null or JsonValueKind.Undefined?null:Number(P(x,name));
    private static DateTimeOffset? Date(JsonElement x,string name)=>DateTimeOffset.TryParse(S(x,name),CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:null;
}
