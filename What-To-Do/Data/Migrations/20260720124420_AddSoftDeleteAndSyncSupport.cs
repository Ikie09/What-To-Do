using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace What_To_Do.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteAndSyncSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Todos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Categories",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsDeleted", "UpdatedAt" },
                values: new object[] { false, new DateTime(2026, 7, 20, 12, 44, 20, 237, DateTimeKind.Utc).AddTicks(5101) });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsDeleted", "UpdatedAt" },
                values: new object[] { false, new DateTime(2026, 7, 20, 12, 44, 20, 237, DateTimeKind.Utc).AddTicks(5108) });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "IsDeleted", "UpdatedAt" },
                values: new object[] { false, new DateTime(2026, 7, 20, 12, 44, 20, 237, DateTimeKind.Utc).AddTicks(5110) });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "IsDeleted", "UpdatedAt" },
                values: new object[] { false, new DateTime(2026, 7, 20, 12, 44, 20, 237, DateTimeKind.Utc).AddTicks(5111) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Categories");
        }
    }
}
