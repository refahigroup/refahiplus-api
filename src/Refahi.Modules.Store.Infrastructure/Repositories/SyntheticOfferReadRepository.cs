using Dapper;
using Npgsql;
using Refahi.Modules.Store.Domain.Repositories;
using System.Globalization;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public sealed class SyntheticOfferReadRepository : ISyntheticOfferReadRepository
{
    private readonly string _connectionString;

    public SyntheticOfferReadRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<(IReadOnlyList<SyntheticOfferReadModel> Items, int Total)> GetOffersAsync(
        SyntheticOfferQuerySpec spec,
        CancellationToken ct = default)
    {
        if (!HasAgreementProducts(spec))
            return ([], 0);

        var cte = BuildFilteredOffersCte();
        var orderBy = GetOfferOrderBy(spec.Sort);
        var sql = $"""
            {cte}
            SELECT COUNT(*) FROM filtered_offers;

            {cte}
            SELECT {OfferProjection}
            FROM filtered_offers
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, BuildParameters(spec), cancellationToken: ct));
        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<SyntheticOfferReadModel>()).AsList();
        return (items, total);
    }

    public async Task<(IReadOnlyList<SyntheticProductCatalogReadModel> Items, int Total)> GetProductCatalogAsync(
        SyntheticOfferQuerySpec spec,
        CancellationToken ct = default)
    {
        if (!HasAgreementProducts(spec))
            return ([], 0);

        var cte = BuildCatalogCte();
        var orderBy = GetCatalogOrderBy(spec.Sort);
        var sql = $"""
            {cte}
            SELECT COUNT(*) FROM catalog_rows;

            {cte}
            SELECT
                "ProductId",
                "AgreementProductId",
                "ProductTitle",
                "ProductSlug",
                "ProductCreatedAt",
                "ImageUrl",
                "MinEffectivePriceMinor",
                "MaxEffectivePriceMinor",
                "DefaultOriginalPriceMinor",
                "DefaultDiscountedPriceMinor",
                "DefaultEffectivePriceMinor",
                "OfferCount",
                "HasVariants",
                "HasSessions",
                "DefaultOfferKey",
                "DefaultShopId",
                "DefaultShopSlug"
            FROM catalog_rows
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, BuildParameters(spec), cancellationToken: ct));
        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<SyntheticProductCatalogReadModel>()).AsList();
        return (items, total);
    }

    public async Task<IReadOnlyList<SyntheticOfferReadModel>> GetProductOffersAsync(
        SyntheticOfferQuerySpec spec,
        CancellationToken ct = default)
    {
        if (!HasAgreementProducts(spec))
            return [];

        var sql = $"""
            {BuildFilteredOffersCte()}
            SELECT {OfferProjection}
            FROM filtered_offers
            ORDER BY "EffectivePriceMinor", "OfferKey";
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        var rows = await connection.QueryAsync<SyntheticOfferReadModel>(
            new CommandDefinition(sql, BuildParameters(spec), cancellationToken: ct));
        return rows.AsList();
    }

    private static bool HasAgreementProducts(SyntheticOfferQuerySpec spec)
        => spec.StockBasedAgreementProductIds.Count > 0 || spec.SessionBasedAgreementProductIds.Count > 0;

    private static object BuildParameters(SyntheticOfferQuerySpec spec)
        => new
        {
            StockAgreementProductIds = spec.StockBasedAgreementProductIds.ToArray(),
            SessionAgreementProductIds = spec.SessionBasedAgreementProductIds.ToArray(),
            Today = spec.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CurrentTime = (spec.CurrentTime ?? TimeOnly.MinValue)
                .ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            HasSearch = !string.IsNullOrWhiteSpace(spec.SearchQuery),
            SearchPattern = string.IsNullOrWhiteSpace(spec.SearchQuery) ? null : $"%{spec.SearchQuery}%",
            spec.ShopId,
            spec.ProductId,
            ProductSlug = string.IsNullOrWhiteSpace(spec.ProductSlug) ? null : spec.ProductSlug,
            spec.OfferKind,
            UsageFrom = spec.UsageFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            UsageTo = spec.UsageTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            spec.MinPriceMinor,
            spec.MaxPriceMinor,
            Offset = (spec.PageNumber - 1) * spec.PageSize,
            spec.PageSize
        };

    private static string GetOfferOrderBy(string sort) => sort switch
    {
        "price-asc" => "\"EffectivePriceMinor\", \"OfferKey\"",
        "price-desc" => "\"EffectivePriceMinor\" DESC, \"OfferKey\"",
        _ => "\"ProductCreatedAt\" DESC, \"OfferKey\""
    };

    private static string GetCatalogOrderBy(string sort) => sort switch
    {
        "price-asc" => "\"MinEffectivePriceMinor\", \"ProductId\"",
        "price-desc" => "\"MaxEffectivePriceMinor\" DESC, \"ProductId\"",
        _ => "\"ProductCreatedAt\" DESC, \"ProductId\""
    };

    private static string BuildFilteredOffersCte() => $"""
        {EligibleOffersCte},
        filtered_offers AS
        (
            SELECT *
            FROM eligible_offers
            WHERE (@ShopId IS NULL OR "ShopId" = @ShopId)
              AND (@ProductId IS NULL OR "ProductId" = @ProductId)
              AND (@ProductSlug IS NULL OR "ProductSlug" = @ProductSlug)
              AND (@OfferKind IS NULL OR "OfferKind" = @OfferKind)
              AND (@MinPriceMinor IS NULL OR "EffectivePriceMinor" >= @MinPriceMinor)
              AND (@MaxPriceMinor IS NULL OR "EffectivePriceMinor" <= @MaxPriceMinor)
              AND (
                    @HasSearch = FALSE
                    OR "ProductTitle" ILIKE @SearchPattern
                    OR "ShopName" ILIKE @SearchPattern
                    OR COALESCE("VariantLabel", '') ILIKE @SearchPattern
                  )
              AND (
                    @UsageFrom IS NULL
                    OR ("OfferKind" = 'ProductSession' AND "SessionDate" >= CAST(@UsageFrom AS date))
                    OR ("OfferKind" = 'SessionVariant' AND ("ToDate" IS NULL OR "ToDate" >= CAST(@UsageFrom AS date)))
                  )
              AND (
                    @UsageTo IS NULL
                    OR ("OfferKind" = 'ProductSession' AND "SessionDate" <= CAST(@UsageTo AS date))
                    OR ("OfferKind" = 'SessionVariant' AND ("FromDate" IS NULL OR "FromDate" <= CAST(@UsageTo AS date)))
                  )
        )
        """;

    private static string BuildCatalogCte() => $"""
        {BuildFilteredOffersCte()},
        ranked_offers AS
        (
            SELECT
                f.*,
                ROW_NUMBER() OVER (
                    PARTITION BY "ProductId"
                    ORDER BY "EffectivePriceMinor", "OfferKey") AS offer_rank
            FROM filtered_offers f
        ),
        product_groups AS
        (
            SELECT
                "ProductId",
                MIN("EffectivePriceMinor") AS "MinEffectivePriceMinor",
                MAX("EffectivePriceMinor") AS "MaxEffectivePriceMinor",
                COUNT(*)::int AS "OfferCount",
                BOOL_OR("VariantId" IS NOT NULL) AS "HasVariants",
                BOOL_OR("OfferKind" IN ('ProductSession', 'SessionVariant')) AS "HasSessions"
            FROM filtered_offers
            GROUP BY "ProductId"
        ),
        catalog_rows AS
        (
            SELECT
                d."ProductId",
                d."AgreementProductId",
                d."ProductTitle",
                d."ProductSlug",
                d."ProductCreatedAt",
                d."ProductImageUrl" AS "ImageUrl",
                g."MinEffectivePriceMinor",
                g."MaxEffectivePriceMinor",
                d."OriginalPriceMinor" AS "DefaultOriginalPriceMinor",
                d."DiscountedPriceMinor" AS "DefaultDiscountedPriceMinor",
                d."EffectivePriceMinor" AS "DefaultEffectivePriceMinor",
                g."OfferCount",
                g."HasVariants",
                g."HasSessions",
                d."OfferKey" AS "DefaultOfferKey",
                d."ShopId" AS "DefaultShopId",
                d."ShopSlug" AS "DefaultShopSlug"
            FROM ranked_offers d
            INNER JOIN product_groups g ON g."ProductId" = d."ProductId"
            WHERE d.offer_rank = 1
        )
        """;

    private const string OfferProjection = """
        "OfferKey",
        "OfferKind",
        "ProductId",
        "AgreementProductId",
        "ProductTitle",
        "ProductSlug",
        "ProductCreatedAt",
        "ShopId",
        "ShopName",
        "ShopSlug",
        "VariantId",
        "VariantLabel",
        "SessionId",
        "SessionDate",
        "SessionStartTime",
        "SessionEndTime",
        "SessionTitle",
        "OriginalPriceMinor",
        "DiscountedPriceMinor",
        "EffectivePriceMinor",
        "AvailableStock",
        "ConfiguredCapacity",
        "RequiresUsageDate",
        "FromDate",
        "ToDate",
        "FixedUsageDate",
        "ImageUrl",
        "HasVariants",
        "HasSessions"
        """;

    private const string EligibleOffersCte = """
        WITH variant_labels AS
        (
            SELECT
                pvc."ProductVariantId",
                STRING_AGG(
                    va."Name" || ': ' || vav."Value",
                    '، ' ORDER BY va."SortOrder", vav."SortOrder") AS "VariantLabel"
            FROM store.product_variant_combinations pvc
            INNER JOIN store.variant_attributes va ON va."Id" = pvc."VariantAttributeId"
            INNER JOIN store.variant_attribute_values vav ON vav."Id" = pvc."VariantAttributeValueId"
            GROUP BY pvc."ProductVariantId"
        ),
        main_images AS
        (
            SELECT DISTINCT ON (pi."ProductId")
                pi."ProductId",
                pi."ImageUrl"
            FROM store.product_images pi
            ORDER BY pi."ProductId", pi."IsMain" DESC, pi."SortOrder", pi."Id"
        ),
        eligible_offers AS
        (
            SELECT
                'sp:' || REPLACE(sp."Id"::text, '-', '') AS "OfferKey",
                'StockProduct'::text AS "OfferKind",
                p."Id" AS "ProductId",
                p."AgreementProductId",
                p."Title" AS "ProductTitle",
                p."Slug" AS "ProductSlug",
                p."CreatedAt" AS "ProductCreatedAt",
                s."Id" AS "ShopId",
                s."Name" AS "ShopName",
                s."Slug" AS "ShopSlug",
                NULL::uuid AS "VariantId",
                NULL::text AS "VariantLabel",
                NULL::uuid AS "SessionId",
                NULL::date AS "SessionDate",
                NULL::time AS "SessionStartTime",
                NULL::time AS "SessionEndTime",
                NULL::text AS "SessionTitle",
                sp."Price" AS "OriginalPriceMinor",
                CASE
                    WHEN sp."DiscountedPrice" > 0 AND sp."DiscountedPrice" < sp."Price"
                    THEN sp."DiscountedPrice"
                    ELSE NULL
                END AS "DiscountedPriceMinor",
                CASE
                    WHEN sp."DiscountedPrice" > 0 AND sp."DiscountedPrice" < sp."Price"
                    THEN sp."DiscountedPrice"
                    ELSE sp."Price"
                END AS "EffectivePriceMinor",
                p."StockCount" AS "AvailableStock",
                NULL::int AS "ConfiguredCapacity",
                FALSE AS "RequiresUsageDate",
                NULL::date AS "FromDate",
                NULL::date AS "ToDate",
                NULL::date AS "FixedUsageDate",
                mi."ImageUrl" AS "ImageUrl",
                mi."ImageUrl" AS "ProductImageUrl",
                FALSE AS "HasVariants",
                EXISTS (
                    SELECT 1 FROM store.product_sessions eps WHERE eps."ProductId" = p."Id") AS "HasSessions"
            FROM store.shop_products sp
            INNER JOIN store.products p ON p."Id" = sp."ProductId"
            INNER JOIN store.shops s ON s."Id" = sp."ShopId"
            LEFT JOIN main_images mi ON mi."ProductId" = p."Id"
            WHERE p."AgreementProductId" = ANY(@StockAgreementProductIds)
              AND p."IsAvailable" = TRUE
              AND p."IsDeleted" = FALSE
              AND p."StockCount" > 0
              AND s."Status" = 2
              AND sp."IsActive" = TRUE
              AND sp."IsDeleted" = FALSE
              AND sp."Price" > 0
              AND (sp."DiscountedPrice" = 0 OR (sp."DiscountedPrice" > 0 AND sp."DiscountedPrice" < sp."Price"))
              AND NOT EXISTS (
                    SELECT 1 FROM store.product_variants epv WHERE epv."ProductId" = p."Id")

            UNION ALL

            SELECT
                'spv:' || REPLACE(spv."Id"::text, '-', '') AS "OfferKey",
                'StockVariant'::text AS "OfferKind",
                p."Id" AS "ProductId",
                p."AgreementProductId",
                p."Title" AS "ProductTitle",
                p."Slug" AS "ProductSlug",
                p."CreatedAt" AS "ProductCreatedAt",
                s."Id" AS "ShopId",
                s."Name" AS "ShopName",
                s."Slug" AS "ShopSlug",
                pv."Id" AS "VariantId",
                COALESCE(vl."VariantLabel", pv."SKU") AS "VariantLabel",
                NULL::uuid AS "SessionId",
                NULL::date AS "SessionDate",
                NULL::time AS "SessionStartTime",
                NULL::time AS "SessionEndTime",
                NULL::text AS "SessionTitle",
                spv."PriceMinor" AS "OriginalPriceMinor",
                spv."DiscountedPriceMinor",
                COALESCE(spv."DiscountedPriceMinor", spv."PriceMinor") AS "EffectivePriceMinor",
                pv."StockCount" AS "AvailableStock",
                NULL::int AS "ConfiguredCapacity",
                FALSE AS "RequiresUsageDate",
                NULL::date AS "FromDate",
                NULL::date AS "ToDate",
                NULL::date AS "FixedUsageDate",
                COALESCE(pv."ImageUrl", mi."ImageUrl") AS "ImageUrl",
                mi."ImageUrl" AS "ProductImageUrl",
                TRUE AS "HasVariants",
                EXISTS (
                    SELECT 1 FROM store.product_sessions eps WHERE eps."ProductId" = p."Id") AS "HasSessions"
            FROM store.shop_product_variants spv
            INNER JOIN store.shop_products sp ON sp."Id" = spv."ShopProductId"
            INNER JOIN store.products p ON p."Id" = sp."ProductId"
            INNER JOIN store.product_variants pv ON pv."Id" = spv."ProductVariantId"
            INNER JOIN store.shops s ON s."Id" = sp."ShopId"
            LEFT JOIN variant_labels vl ON vl."ProductVariantId" = pv."Id"
            LEFT JOIN main_images mi ON mi."ProductId" = p."Id"
            WHERE p."AgreementProductId" = ANY(@StockAgreementProductIds)
              AND p."IsAvailable" = TRUE
              AND p."IsDeleted" = FALSE
              AND pv."IsAvailable" = TRUE
              AND pv."StockCount" > 0
              AND s."Status" = 2
              AND sp."IsActive" = TRUE
              AND sp."IsDeleted" = FALSE
              AND spv."IsActive" = TRUE
              AND spv."IsDeleted" = FALSE
              AND spv."PriceMinor" > 0
              AND (spv."DiscountedPriceMinor" IS NULL
                   OR (spv."DiscountedPriceMinor" > 0 AND spv."DiscountedPriceMinor" < spv."PriceMinor"))

            UNION ALL

            SELECT
                'ps:' || REPLACE(sp."Id"::text, '-', '') || ':' || REPLACE(ps."Id"::text, '-', '') AS "OfferKey",
                'ProductSession'::text AS "OfferKind",
                p."Id" AS "ProductId",
                p."AgreementProductId",
                p."Title" AS "ProductTitle",
                p."Slug" AS "ProductSlug",
                p."CreatedAt" AS "ProductCreatedAt",
                s."Id" AS "ShopId",
                s."Name" AS "ShopName",
                s."Slug" AS "ShopSlug",
                NULL::uuid AS "VariantId",
                NULL::text AS "VariantLabel",
                ps."Id" AS "SessionId",
                ps."Date" AS "SessionDate",
                ps."StartTime" AS "SessionStartTime",
                ps."EndTime" AS "SessionEndTime",
                ps."Title" AS "SessionTitle",
                sp."Price" + ps."PriceAdjustment" AS "OriginalPriceMinor",
                CASE
                    WHEN sp."DiscountedPrice" > 0 AND sp."DiscountedPrice" < sp."Price"
                    THEN sp."DiscountedPrice" + ps."PriceAdjustment"
                    ELSE NULL
                END AS "DiscountedPriceMinor",
                CASE
                    WHEN sp."DiscountedPrice" > 0 AND sp."DiscountedPrice" < sp."Price"
                    THEN sp."DiscountedPrice" + ps."PriceAdjustment"
                    ELSE sp."Price" + ps."PriceAdjustment"
                END AS "EffectivePriceMinor",
                NULL::int AS "AvailableStock",
                ps."Capacity" AS "ConfiguredCapacity",
                FALSE AS "RequiresUsageDate",
                ps."Date" AS "FromDate",
                ps."Date" AS "ToDate",
                ps."Date" AS "FixedUsageDate",
                mi."ImageUrl" AS "ImageUrl",
                mi."ImageUrl" AS "ProductImageUrl",
                EXISTS (
                    SELECT 1 FROM store.product_variants epv WHERE epv."ProductId" = p."Id") AS "HasVariants",
                TRUE AS "HasSessions"
            FROM store.product_sessions ps
            INNER JOIN store.products p ON p."Id" = ps."ProductId"
            INNER JOIN store.shop_products sp ON sp."ProductId" = p."Id"
            INNER JOIN store.shops s ON s."Id" = sp."ShopId"
            LEFT JOIN main_images mi ON mi."ProductId" = p."Id"
            WHERE p."AgreementProductId" = ANY(@SessionAgreementProductIds)
              AND p."IsAvailable" = TRUE
              AND p."IsDeleted" = FALSE
              AND s."Status" = 2
              AND sp."IsActive" = TRUE
              AND sp."IsDeleted" = FALSE
              AND sp."Price" > 0
              AND (sp."DiscountedPrice" = 0 OR (sp."DiscountedPrice" > 0 AND sp."DiscountedPrice" < sp."Price"))
              AND (ps."Date" > CAST(@Today AS date)
                   OR (ps."Date" = CAST(@Today AS date)
                       AND ps."EndTime" > CAST(@CurrentTime AS time without time zone)))
              AND ps."Capacity" > 0
              AND ps."IsActive" = TRUE
              AND ps."IsCancelled" = FALSE

            UNION ALL

            SELECT
                'spv:' || REPLACE(spv."Id"::text, '-', '') AS "OfferKey",
                'SessionVariant'::text AS "OfferKind",
                p."Id" AS "ProductId",
                p."AgreementProductId",
                p."Title" AS "ProductTitle",
                p."Slug" AS "ProductSlug",
                p."CreatedAt" AS "ProductCreatedAt",
                s."Id" AS "ShopId",
                s."Name" AS "ShopName",
                s."Slug" AS "ShopSlug",
                pv."Id" AS "VariantId",
                COALESCE(vl."VariantLabel", pv."SKU") AS "VariantLabel",
                NULL::uuid AS "SessionId",
                NULL::date AS "SessionDate",
                NULL::time AS "SessionStartTime",
                NULL::time AS "SessionEndTime",
                NULL::text AS "SessionTitle",
                spv."PriceMinor" AS "OriginalPriceMinor",
                spv."DiscountedPriceMinor",
                COALESCE(spv."DiscountedPriceMinor", spv."PriceMinor") AS "EffectivePriceMinor",
                NULL::int AS "AvailableStock",
                pv."Capacity" AS "ConfiguredCapacity",
                (pv."CapacityType" = 2
                 AND pv."FromDate" IS NOT NULL
                 AND pv."ToDate" IS NOT NULL
                 AND pv."FromDate" <> pv."ToDate") AS "RequiresUsageDate",
                pv."FromDate" AS "FromDate",
                pv."ToDate" AS "ToDate",
                CASE
                    WHEN pv."FromDate" IS NOT NULL AND pv."FromDate" = pv."ToDate" THEN pv."FromDate"
                    ELSE NULL
                END AS "FixedUsageDate",
                COALESCE(pv."ImageUrl", mi."ImageUrl") AS "ImageUrl",
                mi."ImageUrl" AS "ProductImageUrl",
                TRUE AS "HasVariants",
                TRUE AS "HasSessions"
            FROM store.shop_product_variants spv
            INNER JOIN store.shop_products sp ON sp."Id" = spv."ShopProductId"
            INNER JOIN store.products p ON p."Id" = sp."ProductId"
            INNER JOIN store.product_variants pv ON pv."Id" = spv."ProductVariantId"
            INNER JOIN store.shops s ON s."Id" = sp."ShopId"
            LEFT JOIN variant_labels vl ON vl."ProductVariantId" = pv."Id"
            LEFT JOIN main_images mi ON mi."ProductId" = p."Id"
            WHERE p."AgreementProductId" = ANY(@SessionAgreementProductIds)
              AND p."IsAvailable" = TRUE
              AND p."IsDeleted" = FALSE
              AND pv."IsAvailable" = TRUE
              AND s."Status" = 2
              AND sp."IsActive" = TRUE
              AND sp."IsDeleted" = FALSE
              AND spv."IsActive" = TRUE
              AND spv."IsDeleted" = FALSE
              AND spv."PriceMinor" > 0
              AND (spv."DiscountedPriceMinor" IS NULL
                   OR (spv."DiscountedPriceMinor" > 0 AND spv."DiscountedPriceMinor" < spv."PriceMinor"))
              AND (pv."CapacityType" = 0 OR (pv."CapacityType" IN (1, 2) AND pv."Capacity" > 0))
              AND (pv."CapacityType" <> 2 OR (pv."FromDate" IS NOT NULL AND pv."ToDate" IS NOT NULL))
              AND (pv."ToDate" IS NULL OR pv."ToDate" >= CAST(@Today AS date))
        )
        """;
}
