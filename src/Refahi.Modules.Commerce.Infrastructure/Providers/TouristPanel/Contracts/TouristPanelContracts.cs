// Generated from TouristPanel-Production-Implementation.md, API 3.7.35.
// Dates and UUIDs remain wire strings; domain mapping validates their semantics.
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel.Contracts;

public enum TpPaymentGatewayType
{
    None = 0,
    Pasargad = 1,
    Melli = 2,
    Saman = 3,
    Mellat = 4,
    Tara = 5,
    PardakhtNovin = 6,
    ParbadVirtual = -1,
}

public sealed record TpValidatedDiscountCodeDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("normalizedCode")]
    public string? NormalizedCode { get; init; }
    [JsonPropertyName("code")]
    public string? Code { get; init; }
    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }
    [JsonPropertyName("isValidDay")]
    public bool IsValidDay { get; init; }
    [JsonPropertyName("isValidTicketType")]
    public bool IsValidTicketType { get; init; }
    [JsonPropertyName("message")]
    public string? Message { get; init; }
    [JsonPropertyName("applyOnCommission")]
    public bool ApplyOnCommission { get; init; }
    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }
    [JsonPropertyName("percent")]
    public int Percent { get; init; }
    [JsonPropertyName("discountAmount")]
    public decimal DiscountAmount { get; init; }
}

public enum TpCommissionPriceCategoryType
{
    Gishe = 1,
    Hub = 2,
    FrontStore = 3,
}

public enum TpCurrencyType
{
    IRR = 1,
    USD = 2,
    EUR = 3,
    AED = 4,
    TRY = 5,
}

public sealed record TpCityLocationData
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("native")]
    public string? Native { get; init; }
    [JsonPropertyName("baseCity")]
    public bool BaseCity { get; init; }
}

public sealed record TpCountryLocationData
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("code")]
    public int Code { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("native")]
    public string? Native { get; init; }
    [JsonPropertyName("timeZone")]
    public string? TimeZone { get; init; }
    [JsonPropertyName("capital")]
    public string? Capital { get; init; }
    [JsonPropertyName("phoneCode")]
    public string? PhoneCode { get; init; }
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
    [JsonPropertyName("iso2")]
    public string? Iso2 { get; init; }
    [JsonPropertyName("iso3")]
    public string? Iso3 { get; init; }
}

public sealed record TpLocationData
{
    [JsonPropertyName("country")]
    public TpCountryLocationData? Country { get; init; }
    [JsonPropertyName("state")]
    public TpStateLocationData? State { get; init; }
    [JsonPropertyName("city")]
    public TpCityLocationData? City { get; init; }
    [JsonPropertyName("native")]
    public string? Native { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("code")]
    public string? Code { get; init; }
}

public sealed record TpLocationDto
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("native")]
    public string? Native { get; init; }
}

public sealed record TpStateLocationData
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("native")]
    public string? Native { get; init; }
}

public enum TpPaymentMethod
{
    Marketplace_Wallets = 100,
    Marketplace_DebitWallet_DirectPayment = 101,
    Marketplace_DebitWallet_PaidByCustomer = 102,
    Marketplace_Debit = 110,
    Marketplace_Credit = 111,
    Supplychain_Gishe_Cash = 200,
    Supplychain_Gishe_PosCard = 201,
    Supplychain_Gishe_Cash_PosCard = 202,
    Supplychain_GisheGuest = 203,
    Supplychain_Gishe_DebitWallet = 210,
    Supplychain_Gishe_DebitWallet_And_Cash = 211,
    Supplychain_Gishe_DebitWallet_And_PosCard = 212,
    Supplychain_Gishe_CreditWallet = 220,
    Supplychain_Gishe_CreditWallet_And_Cash = 221,
    Supplychain_Gishe_CreditWallet_And_PosCard = 222,
    CreditWallet_DirectPayment = 300,
    CreditWallet_PaidByCustomer = 301,
    DebitWallet_DirectPayment = 302,
    DebitWallet_PaidByCustomer = 302,
    WebsiteCredit = 504,
    CustomerClubCredit = 505,
    ShahrzadCredit = 506,
}

public enum TpPurchaseGateway
{
    Hub = 100,
    HubApi = 101,
    HubKiosk = 102,
    HubFrontStore = 103,
    HubSalesRepresentative = 104,
    Gishe = 200,
    GisheApi = 201,
    GisheKiosk = 202,
    GisheFrontStore = 203,
}

public enum TpPurchaseStatus
{
    PaymentWaiting = 0,
    Payed = 1,
    Canceled = 2,
    IPGRedirect = 3,
}

public enum TpTicketSettlementState
{
    None = 0,
    SettlementRequest = 1,
    Settled = 2,
}

public enum TpTicketState
{
    Minted = 100,
    Confirmed = 200,
    SettlementRequest = 201,
    Settled = 202,
    RejectedCancelRequest = 210,
    Revoked = 220,
    CancelRequest = 300,
    Canceled = 301,
}

public enum TpTicketType
{
    Service = 1,
    Sans = 2,
    Tour = 3,
}

public sealed record TpPurchaseResultDto
{
    [JsonPropertyName("ticketsInvoice")]
    public TpTicketsInvoiceDto? TicketsInvoice { get; init; }
    [JsonPropertyName("publicTicketsUrl")]
    public string? PublicTicketsUrl { get; init; }
    [JsonPropertyName("ticketTypeGroupUrls")]
    public List<TpTicketTypeGroupUrl>? TicketTypeGroupUrls { get; init; }
    [JsonPropertyName("ticketsUrl")]
    public string? TicketsUrl { get; init; }
    [JsonPropertyName("invoiceUrl")]
    public string? InvoiceUrl { get; init; }
    [JsonPropertyName("purchaseStatus")]
    public TpPurchaseStatus PurchaseStatus { get; init; }
    [JsonPropertyName("paymentUrl")]
    public string? PaymentUrl { get; init; }
    [JsonPropertyName("paymentId")]
    public string? PaymentId { get; init; }
    [JsonPropertyName("message")]
    public string? Message { get; init; }
    [JsonPropertyName("status")]
    public bool Status { get; init; }
    [JsonPropertyName("tempReserveIsExpired")]
    public bool TempReserveIsExpired { get; init; }
}

public sealed record TpAddTempGroupDto
{
    [JsonPropertyName("supplyChainHojreId")]
    public string? SupplyChainHojreId { get; init; }
    [JsonPropertyName("marketplaceHojreId")]
    public string? MarketplaceHojreId { get; init; }
    [JsonPropertyName("eventId")]
    public string? EventId { get; init; }
    [JsonPropertyName("programId")]
    public string? ProgramId { get; init; }
    [JsonPropertyName("tickets")]
    public List<TpAddTempTicketDto>? Tickets { get; init; }
}

public sealed record TpAddTempTicketDto
{
    [JsonPropertyName("ticketId")]
    public string? TicketId { get; init; }
    [JsonPropertyName("programTicketTypeId")]
    public string? ProgramTicketTypeId { get; init; }
    [JsonPropertyName("eventTicketTypeId")]
    public string? EventTicketTypeId { get; init; }
    [JsonPropertyName("referenceTicketTypeId")]
    public string? ReferenceTicketTypeId { get; init; }
    [JsonPropertyName("isNumberEqualsReferenceTicketType")]
    public bool IsNumberEqualsReferenceTicketType { get; init; }
    [JsonPropertyName("manifestUniqueNumber")]
    public string? ManifestUniqueNumber { get; init; }
    [JsonPropertyName("manifestUniqueName")]
    public string? ManifestUniqueName { get; init; }
    [JsonPropertyName("commissionPriceCategoryId")]
    public string? CommissionPriceCategoryId { get; init; }
}

public sealed record TpFinalizingTempShoppingCartDto
{
    [JsonPropertyName("paymentMethod")]
    public TpPaymentMethod PaymentMethod { get; init; }
    [JsonPropertyName("bankGateway")]
    public TpPaymentGatewayType BankGateway { get; init; }
    [JsonPropertyName("customerFullName")]
    public string? CustomerFullName { get; init; }
    [JsonPropertyName("customerMobile")]
    public string? CustomerMobile { get; init; }
    [JsonPropertyName("customerAddress")]
    public string? CustomerAddress { get; init; }
    [JsonPropertyName("internalTrackingCode")]
    public string? InternalTrackingCode { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    [JsonPropertyName("totalDiscount")]
    public decimal TotalDiscount { get; init; }
    [JsonPropertyName("sendTicketSms")]
    public bool SendTicketSms { get; init; }
    [JsonPropertyName("posPaymentDetail")]
    public TpPaymentDetailData? PosPaymentDetail { get; init; }
    [JsonPropertyName("providingManifests")]
    public List<TpProvidingManifestDto>? ProvidingManifests { get; init; }
}

public sealed record TpProvidingManifestDto
{
    [JsonPropertyName("tempGroupId")]
    public string? TempGroupId { get; init; }
    [JsonPropertyName("manifestUniqueNames")]
    public List<string>? ManifestUniqueNames { get; init; }
    [JsonPropertyName("manifestUniqueNumbers")]
    public List<string>? ManifestUniqueNumbers { get; init; }
}

public sealed record TpTempShoppingCartDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("trakingCode")]
    public long TrakingCode { get; init; }
    [JsonPropertyName("expireDateTime")]
    public string? ExpireDateTime { get; init; }
    [JsonPropertyName("groups")]
    public List<TpTempShoppingCartGroupDto>? Groups { get; init; }
    [JsonPropertyName("paymentId")]
    public string? PaymentId { get; init; }
    [JsonPropertyName("paymentDescription")]
    public string? PaymentDescription { get; init; }
    [JsonPropertyName("purchaseStatus")]
    public TpPurchaseStatus PurchaseStatus { get; init; }
    [JsonPropertyName("purchaseGateway")]
    public TpPurchaseGateway PurchaseGateway { get; init; }
    [JsonPropertyName("creatorHojreId")]
    public string? CreatorHojreId { get; init; }
    [JsonPropertyName("creatorHojre")]
    public TpHojreData? CreatorHojre { get; init; }
    [JsonPropertyName("customerFullName")]
    public string? CustomerFullName { get; init; }
    [JsonPropertyName("customerMobile")]
    public string? CustomerMobile { get; init; }
    [JsonPropertyName("customerAddress")]
    public string? CustomerAddress { get; init; }
    [JsonPropertyName("internalTrackingCode")]
    public string? InternalTrackingCode { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }
    [JsonPropertyName("discountAmount")]
    public decimal DiscountAmount { get; init; }
    [JsonPropertyName("creationTime")]
    public string? CreationTime { get; init; }
    [JsonPropertyName("sendTicketSms")]
    public bool SendTicketSms { get; init; }
}

public sealed record TpTempShoppingCartGroupDto
{
    [JsonPropertyName("groupId")]
    public string? GroupId { get; init; }
    [JsonPropertyName("expireTime")]
    public string? ExpireTime { get; init; }
    [JsonPropertyName("ticketTitle")]
    public string? TicketTitle { get; init; }
    [JsonPropertyName("sansDescription")]
    public string? SansDescription { get; init; }
    [JsonPropertyName("programDescription")]
    public string? ProgramDescription { get; init; }
    [JsonPropertyName("supplyChainHojreId")]
    public string? SupplyChainHojreId { get; init; }
    [JsonPropertyName("supplyChainHojre")]
    public TpHojreData? SupplyChainHojre { get; init; }
    [JsonPropertyName("programId")]
    public string? ProgramId { get; init; }
    [JsonPropertyName("programTicketTypeId")]
    public string? ProgramTicketTypeId { get; init; }
    [JsonPropertyName("referenceTicketTypeId")]
    public string? ReferenceTicketTypeId { get; init; }
    [JsonPropertyName("eventId")]
    public string? EventId { get; init; }
    [JsonPropertyName("eventTicketTypeId")]
    public string? EventTicketTypeId { get; init; }
    [JsonPropertyName("commissionPriceCategoryId")]
    public string? CommissionPriceCategoryId { get; init; }
    [JsonPropertyName("commissionPriceCategoryTitle")]
    public string? CommissionPriceCategoryTitle { get; init; }
    [JsonPropertyName("totalTicketPrice")]
    public decimal TotalTicketPrice { get; init; }
    [JsonPropertyName("totalBuyPrice")]
    public decimal TotalBuyPrice { get; init; }
    [JsonPropertyName("totalCommissionPrice")]
    public decimal TotalCommissionPrice { get; init; }
    [JsonPropertyName("totalSalesRepresentativeCommissionAmount")]
    public decimal TotalSalesRepresentativeCommissionAmount { get; init; }
    [JsonPropertyName("totalSalesRepresentativeBuyPrice")]
    public decimal TotalSalesRepresentativeBuyPrice { get; init; }
    [JsonPropertyName("totalSellerCommissionAmount")]
    public decimal TotalSellerCommissionAmount { get; init; }
    [JsonPropertyName("ticketsCount")]
    public int TicketsCount { get; init; }
    [JsonPropertyName("tickets")]
    public List<TpTempShoppingCartTicketDto>? Tickets { get; init; }
    [JsonPropertyName("isManifestNeeded")]
    public bool IsManifestNeeded { get; init; }
    [JsonPropertyName("isManifestUniqueNumberNeeded")]
    public bool IsManifestUniqueNumberNeeded { get; init; }
    [JsonPropertyName("programTitle")]
    public string? ProgramTitle { get; init; }
    [JsonPropertyName("eventLabel")]
    public string? EventLabel { get; init; }
    [JsonPropertyName("eventFullTime")]
    public string? EventFullTime { get; init; }
}

public sealed record TpTempShoppingCartTicketDto
{
    [JsonPropertyName("ticketId")]
    public string? TicketId { get; init; }
    [JsonPropertyName("ticketPrice")]
    public decimal TicketPrice { get; init; }
    [JsonPropertyName("buyPrice")]
    public decimal BuyPrice { get; init; }
    [JsonPropertyName("commissionPrice")]
    public decimal CommissionPrice { get; init; }
    [JsonPropertyName("salesRepresentativeCommissionAmount")]
    public decimal SalesRepresentativeCommissionAmount { get; init; }
    [JsonPropertyName("salesRepresentativeBuyPrice")]
    public decimal SalesRepresentativeBuyPrice { get; init; }
    [JsonPropertyName("sellerCommissionAmount")]
    public decimal SellerCommissionAmount { get; init; }
    [JsonPropertyName("manifestUniqueNumber")]
    public string? ManifestUniqueNumber { get; init; }
    [JsonPropertyName("manifestUniqueName")]
    public string? ManifestUniqueName { get; init; }
}

public sealed record TpTicketTypeGroupUrl
{
    [JsonPropertyName("programTitle")]
    public string? ProgramTitle { get; init; }
    [JsonPropertyName("ticketType")]
    public string? TicketType { get; init; }
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

public sealed record TpPaymentDetailData
{
    [JsonPropertyName("cashReceivedAmount")]
    public decimal CashReceivedAmount { get; init; }
    [JsonPropertyName("posCardReceivedAmount")]
    public decimal PosCardReceivedAmount { get; init; }
    [JsonPropertyName("creditReceivedAmount")]
    public decimal CreditReceivedAmount { get; init; }
    [JsonPropertyName("walletReceivedAmount")]
    public decimal WalletReceivedAmount { get; init; }
    [JsonPropertyName("transactionDateTime")]
    public string? TransactionDateTime { get; init; }
    [JsonPropertyName("cardNumber")]
    public string? CardNumber { get; init; }
    [JsonPropertyName("traceNumber")]
    public string? TraceNumber { get; init; }
    [JsonPropertyName("terminalNo")]
    public string? TerminalNo { get; init; }
    [JsonPropertyName("serialTransaction")]
    public string? SerialTransaction { get; init; }
}

public sealed record TpRevokingByStationData
{
    [JsonPropertyName("stationIndex")]
    public int StationIndex { get; init; }
    [JsonPropertyName("stationId")]
    public string? StationId { get; init; }
    [JsonPropertyName("pathId")]
    public string? PathId { get; init; }
    [JsonPropertyName("checkinRequired")]
    public bool CheckinRequired { get; init; }
    [JsonPropertyName("checkin")]
    public bool Checkin { get; init; }
    [JsonPropertyName("checkinDateTime")]
    public string? CheckinDateTime { get; init; }
    [JsonPropertyName("checkoutRequired")]
    public bool CheckoutRequired { get; init; }
    [JsonPropertyName("checkout")]
    public bool Checkout { get; init; }
    [JsonPropertyName("checkoutDateTime")]
    public string? CheckoutDateTime { get; init; }
    [JsonPropertyName("revoked")]
    public bool Revoked { get; init; }
}

public sealed record TpCommissionPriceBeneficiaryDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("walletId")]
    public string? WalletId { get; init; }
    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }
}

public sealed record TpCommissionPriceCategoryDto
{
    [JsonPropertyName("commissionPriceCategoryId")]
    public string? CommissionPriceCategoryId { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("currency")]
    public TpCurrencyType Currency { get; init; }
    [JsonPropertyName("currencyRate")]
    public decimal CurrencyRate { get; init; }
    [JsonPropertyName("type")]
    public TpCommissionPriceCategoryType Type { get; init; }
}

public sealed record TpEventDto
{
    [JsonPropertyName("excuteTime")]
    public string? ExcuteTime { get; init; }
    [JsonPropertyName("eventId")]
    public string? EventId { get; init; }
    [JsonPropertyName("start")]
    public string? Start { get; init; }
    [JsonPropertyName("end")]
    public string? End { get; init; }
    [JsonPropertyName("endReservation")]
    public string? EndReservation { get; init; }
    [JsonPropertyName("label")]
    public string? Label { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    [JsonPropertyName("capacity")]
    public int Capacity { get; init; }
    [JsonPropertyName("ticketTypes")]
    public List<TpTicketTypeDto>? TicketTypes { get; init; }
}

public sealed record TpEventSansDto
{
    [JsonPropertyName("dateTime")]
    public string? DateTime { get; init; }
    [JsonPropertyName("persianDate")]
    public string? PersianDate { get; init; }
    [JsonPropertyName("events")]
    public List<TpEventDto>? Events { get; init; }
}

public sealed record TpProgramDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("type")]
    public TpProgramType Type { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    [JsonPropertyName("rules")]
    public string? Rules { get; init; }
    [JsonPropertyName("indexImage")]
    public string? IndexImage { get; init; }
    [JsonPropertyName("galleryImages")]
    public List<string>? GalleryImages { get; init; }
    [JsonPropertyName("programCategory")]
    public TpProgramCategoryDto? ProgramCategory { get; init; }
    [JsonPropertyName("tag")]
    public TpProgramTagDto? Tag { get; init; }
    [JsonPropertyName("stationBasedRevoking")]
    public bool StationBasedRevoking { get; init; }
    [JsonPropertyName("programReference")]
    public string? ProgramReference { get; init; }
    [JsonPropertyName("programAddress")]
    public string? ProgramAddress { get; init; }
    [JsonPropertyName("programPhone")]
    public string? ProgramPhone { get; init; }
    [JsonPropertyName("location")]
    public TpLocationDto? Location { get; init; }
    [JsonPropertyName("ticketTypes")]
    public List<TpTicketTypeDto>? TicketTypes { get; init; }
    [JsonPropertyName("eventSans")]
    public List<TpEventSansDto>? EventSans { get; init; }
    [JsonPropertyName("isManifestNeeded")]
    public bool IsManifestNeeded { get; init; }
    [JsonPropertyName("isManifestUniqueNumberNeeded")]
    public bool IsManifestUniqueNumberNeeded { get; init; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
    [JsonPropertyName("supplyChainHojreId")]
    public string? SupplyChainHojreId { get; init; }
    [JsonPropertyName("supplyChainHojre")]
    public TpHojreData? SupplyChainHojre { get; init; }
}

public sealed record TpProgramListDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("supplyChainHojreId")]
    public string? SupplyChainHojreId { get; init; }
    [JsonPropertyName("supplyChainHojre")]
    public TpHojreData? SupplyChainHojre { get; init; }
    [JsonPropertyName("type")]
    public TpProgramType Type { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("indexImage")]
    public string? IndexImage { get; init; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
    [JsonPropertyName("lastActiveEventTime")]
    public string? LastActiveEventTime { get; init; }
    [JsonPropertyName("nearestActiveEventTime")]
    public string? NearestActiveEventTime { get; init; }
    [JsonPropertyName("activeEventsCount")]
    public int? ActiveEventsCount { get; init; }
    [JsonPropertyName("programCategory")]
    public TpProgramCategoryData? ProgramCategory { get; init; }
    [JsonPropertyName("locationData")]
    public TpLocationData? LocationData { get; init; }
    [JsonPropertyName("tag")]
    public TpProgramTagDto? Tag { get; init; }
}

public sealed record TpProgramTagDto
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("start")]
    public string? Start { get; init; }
    [JsonPropertyName("end")]
    public string? End { get; init; }
}

public sealed record TpTicketPriceDto
{
    [JsonPropertyName("ticketPrice")]
    public decimal TicketPrice { get; init; }
    [JsonPropertyName("commissionPrice")]
    public decimal CommissionPrice { get; init; }
    [JsonPropertyName("discountPrice")]
    public decimal DiscountPrice { get; init; }
    [JsonPropertyName("buyPrice")]
    public decimal BuyPrice { get; init; }
    [JsonPropertyName("salesRepresentativeCommissionPrice")]
    public decimal SalesRepresentativeCommissionPrice { get; init; }
    [JsonPropertyName("commissionPriceGroupId")]
    public string? CommissionPriceGroupId { get; init; }
    [JsonPropertyName("commissionPriceCategory")]
    public TpCommissionPriceCategoryDto? CommissionPriceCategory { get; init; }
    [JsonPropertyName("beneficiaries")]
    public List<TpCommissionPriceBeneficiaryDto>? Beneficiaries { get; init; }
}

public sealed record TpTicketTypeDto
{
    [JsonPropertyName("eventTicketTypeId")]
    public string? EventTicketTypeId { get; init; }
    [JsonPropertyName("programTicketTypeId")]
    public string? ProgramTicketTypeId { get; init; }
    [JsonPropertyName("referenceTicketTypeId")]
    public string? ReferenceTicketTypeId { get; init; }
    [JsonPropertyName("isNumberEqualsReferenceTicketType")]
    public bool IsNumberEqualsReferenceTicketType { get; init; }
    [JsonPropertyName("eventTitle")]
    public string? EventTitle { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    [JsonPropertyName("totalCapacity")]
    public int TotalCapacity { get; init; }
    [JsonPropertyName("remainingCapacity")]
    public int RemainingCapacity { get; init; }
    [JsonPropertyName("totalQuotaCapacity")]
    public int TotalQuotaCapacity { get; init; }
    [JsonPropertyName("totalCharterdCapacity")]
    public int TotalCharterdCapacity { get; init; }
    [JsonPropertyName("isQoutaCapacity")]
    public bool IsQoutaCapacity { get; init; }
    [JsonPropertyName("groupTitle")]
    public string? GroupTitle { get; init; }
    [JsonPropertyName("index")]
    public int Index { get; init; }
    [JsonPropertyName("maximumDiscountPercent")]
    public decimal MaximumDiscountPercent { get; init; }
    [JsonPropertyName("maximumDiscountCodeUse")]
    public int MaximumDiscountCodeUse { get; init; }
    [JsonPropertyName("maximumDiscountCodeUseDays")]
    public int MaximumDiscountCodeUseDays { get; init; }
    [JsonPropertyName("isCharter")]
    public bool IsCharter { get; init; }
    [JsonPropertyName("originSupplyChainHojreId")]
    public string? OriginSupplyChainHojreId { get; init; }
    [JsonPropertyName("stationBasedRevoking")]
    public bool StationBasedRevoking { get; init; }
    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; init; }
    [JsonPropertyName("ticketPrices")]
    public List<TpTicketPriceDto>? TicketPrices { get; init; }
}

public sealed record TpTicketAction
{
    [JsonPropertyName("dateTime")]
    public string? DateTime { get; init; }
    [JsonPropertyName("userId")]
    public string? UserId { get; init; }
    [JsonPropertyName("userName")]
    public string? UserName { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed record TpTicketCancelAction
{
    [JsonPropertyName("dateTime")]
    public string? DateTime { get; init; }
    [JsonPropertyName("userId")]
    public string? UserId { get; init; }
    [JsonPropertyName("userName")]
    public string? UserName { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }
}

public sealed record TpTicketDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; init; }
    [JsonPropertyName("ticketNo")]
    public string? TicketNo { get; init; }
    [JsonPropertyName("ticketType")]
    public TpTicketType TicketType { get; init; }
    [JsonPropertyName("ticketState")]
    public TpTicketState TicketState { get; init; }
    [JsonPropertyName("paymentMethod")]
    public TpPaymentMethod PaymentMethod { get; init; }
    [JsonPropertyName("walletTransactionId")]
    public string? WalletTransactionId { get; init; }
    [JsonPropertyName("price")]
    public decimal Price { get; init; }
    [JsonPropertyName("discountAmount")]
    public decimal DiscountAmount { get; init; }
    [JsonPropertyName("receivedAmount")]
    public decimal ReceivedAmount { get; init; }
    [JsonPropertyName("ticketReceivedAmountDetailData")]
    public TpTicketReceivedAmountDetailData? TicketReceivedAmountDetailData { get; init; }
    [JsonPropertyName("sellerBuyAmount")]
    public decimal SellerBuyAmount { get; init; }
    [JsonPropertyName("sellerCommissionAmount")]
    public decimal SellerCommissionAmount { get; init; }
    [JsonPropertyName("salesRepresentativeCommissionAmount")]
    public decimal SalesRepresentativeCommissionAmount { get; init; }
    [JsonPropertyName("salesRepresentativeBuyPrice")]
    public decimal SalesRepresentativeBuyPrice { get; init; }
    [JsonPropertyName("touristPanelCommissionAmount")]
    public decimal TouristPanelCommissionAmount { get; init; }
    [JsonPropertyName("settllmentId")]
    public string? SettllmentId { get; init; }
    [JsonPropertyName("ticketSettlementState")]
    public TpTicketSettlementState TicketSettlementState { get; init; }
    [JsonPropertyName("isSettled")]
    public bool IsSettled { get; init; }
    [JsonPropertyName("isRevoked")]
    public bool IsRevoked { get; init; }
    [JsonPropertyName("isCanceled")]
    public bool IsCanceled { get; init; }
    [JsonPropertyName("isGishe")]
    public bool IsGishe { get; init; }
    [JsonPropertyName("isCharter")]
    public bool IsCharter { get; init; }
    [JsonPropertyName("isQuota")]
    public bool IsQuota { get; init; }
    [JsonPropertyName("cancelationRequest")]
    public TpTicketAction? CancelationRequest { get; init; }
    [JsonPropertyName("cancelation")]
    public TpTicketCancelAction? Cancelation { get; init; }
    [JsonPropertyName("revoking")]
    public TpTicketAction? Revoking { get; init; }
    [JsonPropertyName("stations")]
    public List<TpRevokingByStationData>? Stations { get; init; }
    [JsonPropertyName("manifestUniqueNumber")]
    public string? ManifestUniqueNumber { get; init; }
    [JsonPropertyName("manifestUniqueName")]
    public string? ManifestUniqueName { get; init; }
    [JsonPropertyName("commissionPriceGroupId")]
    public string? CommissionPriceGroupId { get; init; }
    [JsonPropertyName("commissionPriceCategoryId")]
    public string? CommissionPriceCategoryId { get; init; }
}

public sealed record TpTicketGroupDto
{
    [JsonPropertyName("walletTransactionId")]
    public string? WalletTransactionId { get; init; }
    [JsonPropertyName("qrCode")]
    public string? QrCode { get; init; }
    [JsonPropertyName("ticketTypeTitle")]
    public string? TicketTypeTitle { get; init; }
    [JsonPropertyName("paymentMethodTitle")]
    public string? PaymentMethodTitle { get; init; }
    [JsonPropertyName("reservationReference")]
    public string? ReservationReference { get; init; }
    [JsonPropertyName("groupReference")]
    public string? GroupReference { get; init; }
    [JsonPropertyName("groupId")]
    public string? GroupId { get; init; }
    [JsonPropertyName("programTicketTypeTitle")]
    public string? ProgramTicketTypeTitle { get; init; }
    [JsonPropertyName("programTitle")]
    public string? ProgramTitle { get; init; }
    [JsonPropertyName("supplyChainHojreId")]
    public string? SupplyChainHojreId { get; init; }
    [JsonPropertyName("supplyChainHojre")]
    public TpHojreData? SupplyChainHojre { get; init; }
    [JsonPropertyName("supplyChainHojreTitle")]
    public string? SupplyChainHojreTitle { get; init; }
    [JsonPropertyName("supplyChainHojreLogo")]
    public string? SupplyChainHojreLogo { get; init; }
    [JsonPropertyName("marketplaceHojreId")]
    public string? MarketplaceHojreId { get; init; }
    [JsonPropertyName("marketplaceHojre")]
    public TpHojreData? MarketplaceHojre { get; init; }
    [JsonPropertyName("salesRepresentative")]
    public string? SalesRepresentative { get; init; }
    [JsonPropertyName("salesRepresentativeAddress")]
    public string? SalesRepresentativeAddress { get; init; }
    [JsonPropertyName("salesRepresentativePhone")]
    public string? SalesRepresentativePhone { get; init; }
    [JsonPropertyName("marketplaceHojreTitle")]
    public string? MarketplaceHojreTitle { get; init; }
    [JsonPropertyName("marketplaceHojreLogo")]
    public string? MarketplaceHojreLogo { get; init; }
    [JsonPropertyName("marketplaceHojreAddress")]
    public string? MarketplaceHojreAddress { get; init; }
    [JsonPropertyName("marketplaceHojrePhone")]
    public string? MarketplaceHojrePhone { get; init; }
    [JsonPropertyName("programId")]
    public string? ProgramId { get; init; }
    [JsonPropertyName("programTicketTypeId")]
    public string? ProgramTicketTypeId { get; init; }
    [JsonPropertyName("eventId")]
    public string? EventId { get; init; }
    [JsonPropertyName("eventTicketTypeId")]
    public string? EventTicketTypeId { get; init; }
    [JsonPropertyName("eventTitle")]
    public string? EventTitle { get; init; }
    [JsonPropertyName("eventTicketTypeTitle")]
    public string? EventTicketTypeTitle { get; init; }
    [JsonPropertyName("eventStartDateTime")]
    public string? EventStartDateTime { get; init; }
    [JsonPropertyName("eventEndDateTime")]
    public string? EventEndDateTime { get; init; }
    [JsonPropertyName("programAddress")]
    public string? ProgramAddress { get; init; }
    [JsonPropertyName("programRules")]
    public string? ProgramRules { get; init; }
    [JsonPropertyName("programIndexImage")]
    public string? ProgramIndexImage { get; init; }
    [JsonPropertyName("programDescription")]
    public string? ProgramDescription { get; init; }
    [JsonPropertyName("ticketTypeDescription")]
    public string? TicketTypeDescription { get; init; }
    [JsonPropertyName("turn")]
    public string? Turn { get; init; }
    [JsonPropertyName("tickets")]
    public List<TpTicketDto>? Tickets { get; init; }
}

public sealed record TpTicketsInvoiceDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("creationTime")]
    public string? CreationTime { get; init; }
    [JsonPropertyName("creatorId")]
    public string? CreatorId { get; init; }
    [JsonPropertyName("reservationReference")]
    public string? ReservationReference { get; init; }
    [JsonPropertyName("purchaseStatus")]
    public TpPurchaseStatus PurchaseStatus { get; init; }
    [JsonPropertyName("paymentMethod")]
    public TpPaymentMethod PaymentMethod { get; init; }
    [JsonPropertyName("purchaseGateway")]
    public TpPurchaseGateway PurchaseGateway { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; init; }
    [JsonPropertyName("internalTrackingCode")]
    public string? InternalTrackingCode { get; init; }
    [JsonPropertyName("discountCodeId")]
    public string? DiscountCodeId { get; init; }
    [JsonPropertyName("cashDeskId")]
    public string? CashDeskId { get; init; }
    [JsonPropertyName("creatorHojreId")]
    public string? CreatorHojreId { get; init; }
    [JsonPropertyName("creatorHojre")]
    public TpHojreData? CreatorHojre { get; init; }
    [JsonPropertyName("issuerId")]
    public string? IssuerId { get; init; }
    [JsonPropertyName("issuerUserName")]
    public string? IssuerUserName { get; init; }
    [JsonPropertyName("customerId")]
    public string? CustomerId { get; init; }
    [JsonPropertyName("customerFullName")]
    public string? CustomerFullName { get; init; }
    [JsonPropertyName("customerMobile")]
    public string? CustomerMobile { get; init; }
    [JsonPropertyName("customerAddress")]
    public string? CustomerAddress { get; init; }
    [JsonPropertyName("ticketGroups")]
    public List<TpTicketGroupDto>? TicketGroups { get; init; }
    [JsonPropertyName("paymentDetail")]
    public TpPaymentDetailData? PaymentDetail { get; init; }
    [JsonPropertyName("vatPaymentDetail")]
    public TpVatPaymentDetail? VatPaymentDetail { get; init; }
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }
    [JsonPropertyName("totalDiscountAmount")]
    public decimal TotalDiscountAmount { get; init; }
    [JsonPropertyName("totalVatAmount")]
    public decimal TotalVatAmount { get; init; }
    [JsonPropertyName("totalReceivedAmount")]
    public decimal TotalReceivedAmount { get; init; }
    [JsonPropertyName("totalSellerBuyAmount")]
    public decimal TotalSellerBuyAmount { get; init; }
    [JsonPropertyName("totalSellerCommissionAmount")]
    public decimal TotalSellerCommissionAmount { get; init; }
    [JsonPropertyName("touristPanelTotalCommissionAmount")]
    public decimal TouristPanelTotalCommissionAmount { get; init; }
    [JsonPropertyName("transactionIds")]
    public List<string>? TransactionIds { get; init; }
    [JsonPropertyName("isCashBackSettled")]
    public bool IsCashBackSettled { get; init; }
}

public sealed record TpVatPaymentDetail
{
    [JsonPropertyName("vatPercent")]
    public int VatPercent { get; init; }
    [JsonPropertyName("vatTotalAmount")]
    public decimal VatTotalAmount { get; init; }
    [JsonPropertyName("vatShare")]
    public List<TpVatShare>? VatShare { get; init; }
}

public sealed record TpVatShare
{
    [JsonPropertyName("vatAmount")]
    public decimal VatAmount { get; init; }
    [JsonPropertyName("vatPayer")]
    public string? VatPayer { get; init; }
}

public sealed record TpDiscountValidationResult
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("message")]
    public string? Message { get; init; }
    [JsonPropertyName("status")]
    public bool Status { get; init; }
    [JsonPropertyName("data")]
    public TpValidatedDiscountCodeDto? Data { get; init; }
}

public sealed record TpGisheSettingsDto
{
    [JsonPropertyName("serverTime")]
    public string? ServerTime { get; init; }
    [JsonPropertyName("enableMyCueApi")]
    public bool EnableMyCueApi { get; init; }
    [JsonPropertyName("myCueApiKey")]
    public string? MyCueApiKey { get; init; }
    [JsonPropertyName("myCueTenantName")]
    public string? MyCueTenantName { get; init; }
    [JsonPropertyName("ticketHeaderText")]
    public string? TicketHeaderText { get; init; }
    [JsonPropertyName("ticketFooterText")]
    public string? TicketFooterText { get; init; }
    [JsonPropertyName("allowTicketReprint")]
    public bool AllowTicketReprint { get; init; }
    [JsonPropertyName("requireCustomerFullName")]
    public bool RequireCustomerFullName { get; init; }
    [JsonPropertyName("requireCustomerPhoneNumber")]
    public bool RequireCustomerPhoneNumber { get; init; }
    [JsonPropertyName("eventGetDays")]
    public int EventGetDays { get; init; }
    [JsonPropertyName("myCueSelectedQueue")]
    public List<string>? MyCueSelectedQueue { get; init; }
}

public sealed record TpTicketReceivedAmountDetailData
{
    [JsonPropertyName("cashReceivedAmount")]
    public decimal CashReceivedAmount { get; init; }
    [JsonPropertyName("posCardReceivedAmount")]
    public decimal PosCardReceivedAmount { get; init; }
    [JsonPropertyName("creditReceivedAmount")]
    public decimal CreditReceivedAmount { get; init; }
    [JsonPropertyName("walletReceivedAmount")]
    public decimal WalletReceivedAmount { get; init; }
}

public sealed record TpProgramCategoryData
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

public sealed record TpProgramCategoryDto
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    [JsonPropertyName("index")]
    public int Index { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

public enum TpProgramType
{
    ProgramAsService = 1,
    ProgramAsSans = 2,
    ProgramAsTour = 3,
}

public sealed record TpHojreData
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("tenantId")]
    public string? TenantId { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("logo")]
    public string? Logo { get; init; }
    [JsonPropertyName("hojreCode")]
    public string? HojreCode { get; init; }
    [JsonPropertyName("type")]
    public TpHojreType Type { get; init; }
}

public enum TpHojreType
{
    none = 0,
    SupplyChain = 100,
    AgencyMarketplace = 200,
    OrganizationMarketplace = 201,
}

public sealed record TpRemoteServiceErrorInfo
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }
    [JsonPropertyName("message")]
    public string? Message { get; init; }
    [JsonPropertyName("details")]
    public string? Details { get; init; }
    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }
    [JsonPropertyName("validationErrors")]
    public List<TpRemoteServiceValidationErrorInfo>? ValidationErrors { get; init; }
}

public sealed record TpRemoteServiceErrorResponse
{
    [JsonPropertyName("error")]
    public TpRemoteServiceErrorInfo? Error { get; init; }
}

public sealed record TpRemoteServiceValidationErrorInfo
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
    [JsonPropertyName("members")]
    public List<string>? Members { get; init; }
}
