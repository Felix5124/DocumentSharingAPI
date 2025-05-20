using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentSharingAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLockForDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLock",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLock",
                table: "Documents");
        }
    }
}
