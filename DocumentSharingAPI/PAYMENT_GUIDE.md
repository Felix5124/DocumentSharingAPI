# 🎯 HỆ THỐNG THANH TOÁN VIETQR - HƯỚNG DẪN SỬ DỤNG

## 📋 Tổng quan
Hệ thống thanh toán VietQR cho phép người dùng nạp tiền để nâng cấp tài khoản VIP thông qua chuyển khoản ngân hàng với mã QR.

**Ưu điểm:**
- ✅ Hoàn toàn miễn phí (không tốn phí tích hợp API)
- ✅ Sử dụng VietQR - chuẩn QR của các ngân hàng VN
- ✅ Đối soát thủ công đơn giản qua Admin Panel
- ✅ Phù hợp cho startup và dự án vừa/nhỏ

---

## 🔧 Cài đặt ban đầu

### Bước 1: Thêm tài khoản ngân hàng nhận tiền

Chạy script SQL trong file `insert_bank_account.sql` để thêm thông tin tài khoản ngân hàng của bạn:

```sql
INSERT INTO BankAccounts (BankName, BankCode, AccountNumber, AccountHolderName, IsActive, IsDefault, CreatedAt)
VALUES 
    (N'Vietcombank', 'VCB', '1234567890', N'NGUYEN VAN A', 1, 1, GETDATE());
```

**Lưu ý:**
- `BankCode`: Mã ngân hàng (VCB, TCB, MB, VPB, ACB, ...)
- `AccountNumber`: Số tài khoản của bạn
- `AccountHolderName`: Tên chủ tài khoản (VIẾT HOA, KHÔNG DẤU)
- `IsDefault`: 1 = tài khoản mặc định sẽ được dùng

### Danh sách mã ngân hàng (BankCode) phổ biến:
- VCB - Vietcombank
- TCB - Techcombank
- MB - MB Bank
- VPB - VPBank
- ACB - ACB Bank
- BIDV - BIDV
- VTB - Vietinbank
- AGR - Agribank
- SCB - Sacombank
- TPB - TPBank

---

## 📱 API Endpoints

### 1. **Tạo đơn thanh toán VIP**
```http
POST /api/payments/create
Content-Type: application/json

{
  "userId": 1,
  "subscriptionType": "Monthly"  // hoặc "Yearly"
}
```

**Response:**
```json
{
  "paymentId": 1,
  "orderCode": "VIP20250115123456",
  "userId": 1,
  "userFullName": "Nguyễn Văn A",
  "userEmail": "user@example.com",
  "subscriptionType": "Monthly",
  "amount": 50000,
  "status": "Pending",
  "transferContent": "VIPPAY VIP20250115123456",
  "bankAccountNumber": "1234567890",
  "bankName": "Vietcombank",
  "accountHolderName": "NGUYEN VAN A",
  "qrCodeUrl": "https://img.vietqr.io/image/VCB-1234567890-compact.jpg?amount=50000&addInfo=VIPPAY%20VIP20250115123456&accountName=NGUYEN%20VAN%20A",
  "createdAt": "2025-01-15T10:30:00",
  "expiredAt": "2025-01-16T10:30:00"
}
```

**Giá gói:**
- Monthly: 50,000 VND
- Yearly: 500,000 VND

---

### 2. **Kiểm tra trạng thái thanh toán**
```http
GET /api/payments/check/{orderCode}
```

**Response:**
```json
{
  "orderCode": "VIP20250115123456",
  "status": "Pending",  // Pending | Completed | Cancelled | Expired
  "amount": 50000,
  ...
}
```

---

### 3. **[ADMIN] Lấy danh sách đơn hàng chờ thanh toán**
```http
GET /api/payments/pending
```

**Response:**
```json
[
  {
    "paymentId": 1,
    "orderCode": "VIP20250115123456",
    "userId": 1,
    "userFullName": "Nguyễn Văn A",
    "userEmail": "user@example.com",
    "amount": 50000,
    "status": "Pending",
    "transferContent": "VIPPAY VIP20250115123456",
    "createdAt": "2025-01-15T10:30:00",
    "expiredAt": "2025-01-16T10:30:00"
  }
]
```

---

### 4. **[ADMIN] Xác nhận thanh toán thành công**
```http
POST /api/payments/confirm
Content-Type: application/json

{
  "paymentId": 1,
  "adminId": 1,
  "note": "Đã kiểm tra sao kê, user đã chuyển khoản đúng"
}
```

**Response:**
```json
{
  "message": "Payment confirmed successfully. VIP activated.",
  "payment": { ... },
  "vipExpiryDate": "2025-02-15T10:30:00"
}
```

**Sau khi xác nhận:**
- ✅ Payment status → `Completed`
- ✅ Tạo VipSubscription cho user
- ✅ User.IsVip = true
- ✅ User.VipExpiryDate được set

---

### 5. **[ADMIN] Hủy đơn thanh toán**
```http
POST /api/payments/cancel/{paymentId}
Content-Type: application/json

{
  "adminId": 1,
  "note": "User chuyển sai số tiền"
}
```

---

### 6. **Lấy lịch sử thanh toán của user**
```http
GET /api/payments/user/{userId}
```

---

### 7. **[ADMIN] Lấy tất cả đơn hàng (có phân trang)**
```http
GET /api/payments/all?page=1&pageSize=20
```

---

### 8. **[CRON JOB] Tự động hủy đơn hàng quá hạn**
```http
POST /api/payments/expire-old-payments
```

**Lưu ý:** Nên chạy endpoint này mỗi 1 giờ bằng cron job hoặc background service.

---

## 🔄 Quy trình thanh toán

### **Phía User:**

1. User chọn gói VIP (Monthly/Yearly) → Frontend gọi API `POST /api/payments/create`
2. Frontend nhận về thông tin:
   - Mã QR (hiển thị bằng thẻ `<img>`)
   - Thông tin chuyển khoản (số TK, tên ngân hàng, số tiền, nội dung)
3. User quét QR bằng app ngân hàng → Chuyển khoản
4. User có thể kiểm tra trạng thái bằng API `GET /api/payments/check/{orderCode}`

### **Phía Admin:**

1. Vào trang Admin → Gọi API `GET /api/payments/pending` để xem danh sách đơn chờ
2. Mở app ngân hàng → Kiểm tra sao kê giao dịch
3. Tìm giao dịch có nội dung khớp với `transferContent` (VD: `VIPPAY VIP20250115123456`)
4. Click "Xác nhận" → Gọi API `POST /api/payments/confirm`
5. Hệ thống tự động kích hoạt VIP cho user

---

## 💡 Ví dụ Frontend (React/Vue)

### Hiển thị mã QR thanh toán:

```jsx
// React Example
function PaymentPage({ orderCode, qrCodeUrl, transferContent, amount, bankInfo }) {
  return (
    <div className="payment-container">
      <h2>Thanh toán gói VIP</h2>
      
      {/* Hiển thị QR Code */}
      <div className="qr-section">
        <img src={qrCodeUrl} alt="VietQR Code" style={{ width: 300 }} />
        <p>Quét mã QR bằng app ngân hàng để thanh toán</p>
      </div>

      {/* Thông tin chuyển khoản */}
      <div className="bank-info">
        <h3>Hoặc chuyển khoản thủ công:</h3>
        <p><strong>Ngân hàng:</strong> {bankInfo.bankName}</p>
        <p><strong>Số tài khoản:</strong> {bankInfo.accountNumber}</p>
        <p><strong>Tên chủ TK:</strong> {bankInfo.accountHolderName}</p>
        <p><strong>Số tiền:</strong> {amount.toLocaleString()} VND</p>
        <p><strong>Nội dung:</strong> <code>{transferContent}</code></p>
      </div>

      <div className="warning">
        ⚠️ Lưu ý: Nhập chính xác nội dung chuyển khoản để được duyệt tự động
      </div>
    </div>
  );
}
```

---

## 🔐 Bảo mật

- ✅ Chỉ Admin mới được xác nhận thanh toán
- ✅ Mỗi OrderCode là unique
- ✅ Đơn hàng tự động hết hạn sau 24h
- ✅ Không thể xác nhận đơn đã Completed/Cancelled

---

## 🚀 Tối ưu hóa

### Tự động đối soát (Nâng cao - Có phí):

Nếu sau này bạn muốn tự động hóa hoàn toàn, có thể tích hợp:
- **Casso.vn**: Webhook tự động khi có tiền về
- **VNPay/MoMo**: Cổng thanh toán chính thức

### Background Job để hủy đơn quá hạn:

Trong `Program.cs`, có thể thêm Background Service:

```csharp
// TODO: Add Hangfire or BackgroundService to run ExpireOldPayments every hour
```

---

## 📞 Support

Nếu có vấn đề, liên hệ:
- Email: support@yourapp.com
- Admin Panel: /admin/payments

---

## 🎉 Hoàn thành!

Bạn đã có hệ thống thanh toán VietQR hoàn chỉnh và miễn phí! 🚀
