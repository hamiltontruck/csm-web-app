using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    public partial class AddEquipmentMaintenanceSiteSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "EquipmentMaintenance",
                type: "uuid",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SiteId", table: "EquipmentMaintenance");
        }
    }
}
