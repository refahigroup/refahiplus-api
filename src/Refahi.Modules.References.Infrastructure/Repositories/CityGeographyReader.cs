using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Infrastructure.Persistence.Context;

namespace Refahi.Modules.References.Infrastructure.Repositories;

// V4 geography is owned by the versioned reference SQL release, not EF migrations.
public sealed class CityGeographyReader(ReferencesDbContext db) : ICityGeographyReader
{
    public async Task<IReadOnlyList<CityGeography>> ReadAsync(IReadOnlyCollection<int> cityIds, CancellationToken ct)
    {
        if (cityIds.Count == 0) return [];
        return await db.Database.SqlQueryRaw<CityGeography>("""
            SELECT c."Id" AS "CityId", c."Name" AS "CityName", c."NameEn" AS "CityNameEn",
                p."Id" AS "ProvinceId", p."Name" AS "ProvinceName", p."CountryCode",
                co."Name" AS "CountryName", co."NameEn" AS "CountryNameEn",
                (c."IsActive" AND p."IsActive" AND co."IsActive") AS "IsActive"
            FROM "references".cities c
            JOIN "references".provinces p ON p."Id"=c."ProvinceId"
            JOIN "references".countries co ON co."Code"=p."CountryCode"
            WHERE c."Id"=ANY(@ids)
            """, new NpgsqlParameter("ids", cityIds.Distinct().ToArray())).ToListAsync(ct);
    }
}
