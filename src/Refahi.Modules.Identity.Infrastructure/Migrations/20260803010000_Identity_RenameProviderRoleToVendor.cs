using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Identity.Infrastructure.Persistence.Context;

#nullable disable

namespace Refahi.Modules.Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260803010000_Identity_RenameProviderRoleToVendor")]
public sealed class Identity_RenameProviderRoleToVendor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM identity.user_roles AS provider_role
            USING identity.user_roles AS vendor_role
            WHERE provider_role.user_id = vendor_role.user_id
              AND lower(provider_role.role) = 'provider'
              AND lower(vendor_role.role) = 'vendor';

            UPDATE identity.user_roles
            SET role = 'Vendor'
            WHERE lower(role) = 'provider';

            UPDATE identity.authorization_grants
            SET emitted_role = 'Vendor'
            WHERE lower(emitted_role) = 'provider';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM identity.user_roles AS vendor_role
            USING identity.user_roles AS provider_role
            WHERE vendor_role.user_id = provider_role.user_id
              AND lower(vendor_role.role) = 'vendor'
              AND lower(provider_role.role) = 'provider';

            UPDATE identity.user_roles
            SET role = 'Provider'
            WHERE lower(role) = 'vendor';

            UPDATE identity.authorization_grants
            SET emitted_role = 'Provider'
            WHERE lower(emitted_role) = 'vendor';
            """);
    }
}
