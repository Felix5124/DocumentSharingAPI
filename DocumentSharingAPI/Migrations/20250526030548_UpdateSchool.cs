using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentSharingAPI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_School_SchoolId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_School_SchoolId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_School",
                table: "School");

            migrationBuilder.RenameTable(
                name: "School",
                newName: "Schools");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Schools",
                table: "Schools",
                column: "SchoolId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Schools_SchoolId",
                table: "Documents",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "SchoolId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Schools_SchoolId",
                table: "Users",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "SchoolId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Schools_SchoolId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Schools_SchoolId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Schools",
                table: "Schools");

            migrationBuilder.RenameTable(
                name: "Schools",
                newName: "School");

            migrationBuilder.AddPrimaryKey(
                name: "PK_School",
                table: "School",
                column: "SchoolId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_School_SchoolId",
                table: "Documents",
                column: "SchoolId",
                principalTable: "School",
                principalColumn: "SchoolId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_School_SchoolId",
                table: "Users",
                column: "SchoolId",
                principalTable: "School",
                principalColumn: "SchoolId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
