-- Script để thêm tài khoản ngân hàng mẫu vào database
-- Chạy script này trong SQL Server Management Studio hoặc Azure Data Studio

USE [YourDatabaseName]; -- Thay tên database của bạn vào đây
GO

-- Thêm tài khoản ngân hàng mặc định
INSERT INTO BankAccounts (BankName, BankCode, AccountNumber, AccountHolderName, IsActive, IsDefault, CreatedAt)
VALUES 
    (N'Vietcombank', 'VCB', '1234567890', N'NGUYEN VAN A', 1, 1, GETDATE());

-- Hoặc thêm nhiều tài khoản để lựa chọn
-- INSERT INTO BankAccounts (BankName, BankCode, AccountNumber, AccountHolderName, IsActive, IsDefault, CreatedAt)
-- VALUES 
--     (N'Techcombank', 'TCB', '9876543210', N'NGUYEN VAN A', 1, 0, GETDATE()),
--     (N'MB Bank', 'MB', '1122334455', N'NGUYEN VAN A', 1, 0, GETDATE()),
--     (N'VPBank', 'VPB', '5544332211', N'NGUYEN VAN A', 1, 0, GETDATE());

GO

-- Kiểm tra kết quả
SELECT * FROM BankAccounts;
GO
