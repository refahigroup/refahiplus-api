using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;
namespace Refahi.Modules.Wallets.Infrastructure.Migrations;

[DbContext(typeof(WalletsDbContext))]
[Migration("20261017143000_Wallets_AdditionalFunding")]
public sealed class Wallets_AdditionalFunding : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        foreach (var table in new[] { "payment_intent_allocations", "payment_allocations", "refund_allocations" })
            b.AddColumn<bool>(name: "is_additional", table: table, schema: "wallets", type: "boolean", nullable: false, defaultValue: false);
    }
    protected override void Down(MigrationBuilder b)
    {
        b.Sql("""
            DO $$ BEGIN
            IF EXISTS(SELECT 1 FROM wallets.payment_intent_allocations WHERE is_additional)
               OR EXISTS(SELECT 1 FROM wallets.payment_allocations WHERE is_additional)
               OR EXISTS(SELECT 1 FROM wallets.refund_allocations WHERE is_additional)
            THEN RAISE EXCEPTION 'Cannot remove additional funding metadata while funded payments exist';
            END IF; END $$;
            """);
        foreach (var table in new[] { "payment_intent_allocations", "payment_allocations", "refund_allocations" })
            b.DropColumn("is_additional", table, "wallets");
    }
}
