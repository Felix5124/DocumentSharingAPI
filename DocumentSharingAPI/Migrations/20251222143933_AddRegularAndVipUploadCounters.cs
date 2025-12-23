using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentSharingAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRegularAndVipUploadCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegularUploadsUsedToday",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VipUploadsUsedToday",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegularUploadsUsedToday",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VipUploadsUsedToday",
                table: "Users");
        }
    }
}
