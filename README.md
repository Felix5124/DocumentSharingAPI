# DocumentSharingAPI

Backend API cho nền tảng chia sẻ tài liệu học tập giữa sinh viên các trường đại học.

Mục tiêu của dự án là xây dựng một hệ thống nơi người dùng có thể đăng tải, tìm kiếm, đánh giá và tương tác với tài liệu học thuật, đồng thời hỗ trợ cơ chế phân quyền, kiểm duyệt nội dung, thông báo và các tính năng mở rộng như VIP, thanh toán và chatbot.

## Tổng quan

DocumentSharingAPI được phát triển bằng ASP.NET Core, thiết kế theo mô hình Controller -> Service -> Repository -> Database.
Hệ thống tích hợp nhiều dịch vụ bên thứ ba để đáp ứng các nhu cầu thực tế:

- Xác thực và phân quyền bằng Firebase JWT
- Lưu trữ tệp trên Azure Blob Storage
- Thanh toán và mã QR qua VietQR
- Gửi email thông báo qua SendGrid
- Hỗ trợ chatbot bằng Gemini

## Tính năng chính

- Quản lý người dùng, hồ sơ và thiết lập cá nhân
- Quản lý tài liệu học tập: đăng tải, phân loại, gắn thẻ, theo dõi trạng thái
- Tương tác cộng đồng: bình luận, bài viết, theo dõi người dùng, thông báo
- Hệ thống huy hiệu và xếp hạng người dùng
- Cơ chế báo cáo nội dung và kiểm duyệt bán tự động
- Gói VIP và các chức năng liên quan thanh toán

## Công nghệ sử dụng

- .NET 8 (ASP.NET Core Web API)
- Entity Framework Core + SQL Server
- Firebase Admin SDK (xác thực token)
- Azure Storage Blobs
- SendGrid
- Swagger (OpenAPI)

## Cấu trúc dự án (rút gọn)

```text
DocumentSharingAPI/
	Controllers/      # API endpoints
	Services/         # Business logic và tích hợp dịch vụ ngoài
	Repositories/     # Data access layer
	Models/           # Entity models và DbContext
	Migrations/       # Lịch sử migration EF Core
	Program.cs        # Startup, DI, middleware, auth, CORS
```