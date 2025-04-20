using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentSharingAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserDocuments",
                table: "UserDocuments");

            migrationBuilder.AddColumn<string>(
                name: "ActionType",
                table: "UserDocuments",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserDocuments",
                table: "UserDocuments",
                columns: new[] { "UserId", "DocumentId", "ActionType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserDocuments",
                table: "UserDocuments");

            migrationBuilder.DropColumn(
                name: "ActionType",
                table: "UserDocuments");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserDocuments",
                table: "UserDocuments",
                columns: new[] { "UserId", "DocumentId" });
        }
    }
}
