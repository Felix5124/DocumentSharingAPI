using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentSharingAPI.Migrations
{
    /// <inheritdoc />
    public partial class FixModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kiểm tra và đổi tên cột UserId thành ReporterUserId nếu cần
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'UserId' AND Object_ID = Object_ID(N'Reports'))
                AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ReporterUserId' AND Object_ID = Object_ID(N'Reports'))
                BEGIN
                    EXEC sp_rename 'Reports.UserId', 'ReporterUserId', 'COLUMN';
                END
            ");

            // Thêm cột Details nếu chưa tồn tại
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Details' AND Object_ID = Object_ID(N'Reports'))
                BEGIN
                    ALTER TABLE [Reports] ADD [Details] nvarchar(max) NULL;
                END
            ");

            // Thêm cột Status nếu chưa tồn tại
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Status' AND Object_ID = Object_ID(N'Reports'))
                BEGIN
                    ALTER TABLE [Reports] ADD [Status] nvarchar(20) NOT NULL DEFAULT 'Pending';
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không cần rollback vì đây là migration để đồng bộ với database hiện có
        }
    }
}
