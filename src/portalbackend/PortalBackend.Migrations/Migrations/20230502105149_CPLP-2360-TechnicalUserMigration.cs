using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.Migrations.Migrations
{
    public partial class CPLP2360TechnicalUserMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("INSERT INTO portal.technical_user_profiles (id, offer_id) SELECT gen_random_uuid(), id FROM portal.offers WHERE offer_type_id = 1;");
            migrationBuilder.Sql("INSERT INTO portal.technical_user_profiles (id, offer_id) SELECT gen_random_uuid(), id FROM portal.offers JOIN portal.service_details AS sd ON id = sd.service_id WHERE offer_type_id = 3 AND sd.service_type_id = 1;");
            migrationBuilder.Sql("INSERT INTO portal.technical_user_profile_assigned_user_roles (technical_user_profile_id, user_role_id) SELECT tup.id, '607818be-4978-41f4-bf63-fa8d2de51169' FROM portal.technical_user_profiles AS tup JOIN portal.offers AS o ON tup.offer_id = o.id WHERE o.offer_type_id = 1;");
            migrationBuilder.Sql("INSERT INTO portal.technical_user_profile_assigned_user_roles (technical_user_profile_id, user_role_id) SELECT tup.id, '607818be-4978-41f4-bf63-fa8d2de51155' FROM portal.technical_user_profiles AS tup JOIN portal.offers AS o ON tup.offer_id = o.id WHERE o.offer_type_id = 3;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
