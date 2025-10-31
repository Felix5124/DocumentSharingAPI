using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentSharingAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDownloadLimitsAndBonusSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegularBonusDownloads",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegularBonusDownloads",
                table: "Users");
        }
    }
}
