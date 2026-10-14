using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixDailySiteDiaryTemperaturePrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Temperature",
                table: "DailySiteDiaries",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DailyProgressPhotos",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "DailyProgressPhotos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "DailyProgressPhotos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "DailyProgressPhotos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "DailyProgressPhotos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "DailyProgressPhotos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "DailyProgressPhotos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "DailyProgressPhotos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "DailyProgressPhotos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByUserId",
                table: "DailyProgressPhotos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "DailyProgressPhotos");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "DailyProgressPhotos");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "DailyProgressPhotos");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "DailyProgressPhotos");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "DailyProgressPhotos");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "DailyProgressPhotos");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "DailyProgressPhotos");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "DailyProgressPhotos");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "DailyProgressPhotos");

            migrationBuilder.AlterColumn<decimal>(
                name: "Temperature",
                table: "DailySiteDiaries",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DailyProgressPhotos",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(260)",
                oldMaxLength: 260);
        }
    }
}
