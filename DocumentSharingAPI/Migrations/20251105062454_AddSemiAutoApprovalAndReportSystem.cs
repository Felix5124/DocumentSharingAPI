using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentSharingAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddSemiAutoApprovalAndReportSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if IsApproved column exists before dropping it
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsApproved' AND Object_ID = Object_ID(N'Documents'))
                BEGIN
                    ALTER TABLE [Documents] DROP COLUMN [IsApproved];
                END
            ");

            // Check if ApprovalStatus column doesn't exist before adding it
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ApprovalStatus' AND Object_ID = Object_ID(N'Documents'))
                BEGIN
                    ALTER TABLE [Documents] ADD [ApprovalStatus] nvarchar(20) NOT NULL DEFAULT 'Pending';
                END
                ELSE
                BEGIN
                    -- Nếu cột tồn tại nhưng là kiểu int, chuyển đổi sang string
                    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ApprovalStatus' AND Object_ID = Object_ID(N'Documents') AND system_type_id = 56) -- 56 = int
                    BEGIN
                        -- Cập nhật giá trị trực tiếp
                        UPDATE [Documents] SET [ApprovalStatus] =
                            CASE
                                WHEN [ApprovalStatus] = 0 THEN 'Pending'
                                WHEN [ApprovalStatus] = 1 THEN 'SemiApproved'
                                WHEN [ApprovalStatus] = 2 THEN 'Approved'
                                WHEN [ApprovalStatus] = 3 THEN 'Rejected'
                                ELSE 'Pending'
                            END;
                        
                        -- Thay đổi kiểu dữ liệu của cột
                        ALTER TABLE [Documents] ALTER COLUMN [ApprovalStatus] nvarchar(20) NOT NULL;
                        ALTER TABLE [Documents] ADD CONSTRAINT DF_Documents_ApprovalStatus DEFAULT 'Pending' FOR [ApprovalStatus];
                    END
                END
            ");

            // Check if ReportCount column doesn't exist before adding it
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ReportCount' AND Object_ID = Object_ID(N'Documents'))
                BEGIN
                    ALTER TABLE [Documents] ADD [ReportCount] int NOT NULL DEFAULT 0;
                END
            ");

            // Bảng Reports đã tồn tại trên Azure, không cần tạo lại
            // Chỉ cần đảm bảo các cột cần thiết tồn tại
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Details' AND Object_ID = Object_ID(N'Reports'))
                BEGIN
                    ALTER TABLE [Reports] ADD [Details] nvarchar(max) NULL;
                END
                
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Status' AND Object_ID = Object_ID(N'Reports'))
                BEGIN
                    ALTER TABLE [Reports] ADD [Status] nvarchar(20) NOT NULL DEFAULT 'Pending';
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ReportCount",
                table: "Documents");

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
