using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentSharingAPI.Migrations
{
    /// <inheritdoc />
    public partial class OptimizationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDocuments_DocumentId",
                table: "UserDocuments");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Documents",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_DocumentId_UserId_ActionType",
                table: "UserDocuments",
                columns: new[] { "DocumentId", "UserId", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ApprovalStatus_UploadedAt",
                table: "Documents",
                columns: new[] { "ApprovalStatus", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Title",
                table: "Documents",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDocuments_DocumentId_UserId_ActionType",
                table: "UserDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ApprovalStatus_UploadedAt",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_Title",
                table: "Documents");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_DocumentId",
                table: "UserDocuments",
                column: "DocumentId");
        }
    }
}
