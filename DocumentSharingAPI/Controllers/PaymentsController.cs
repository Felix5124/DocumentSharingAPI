using DocumentSharingAPI.Models;
using DocumentSharingAPI.Models.DTO;
using DocumentSharingAPI.Repositories;
using DocumentSharingAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IBankAccountRepository _bankAccountRepository;
        private readonly IUserRepository _userRepository;
        private readonly IVipSubscriptionRepository _vipSubscriptionRepository;
        private readonly IVietQRService _vietQRService;
        private readonly INotificationRepository _notificationRepository;

        public PaymentsController(
            IPaymentRepository paymentRepository,
            IBankAccountRepository bankAccountRepository,
            IUserRepository userRepository,
            IVipSubscriptionRepository vipSubscriptionRepository,
            IVietQRService vietQRService,
            INotificationRepository notificationRepository)
        {
            _paymentRepository = paymentRepository;
            _bankAccountRepository = bankAccountRepository;
            _userRepository = userRepository;
            _vipSubscriptionRepository = vipSubscriptionRepository;
            _vietQRService = vietQRService;
            _notificationRepository = notificationRepository;
        }

        /// <summary>
        /// Tạo đơn thanh toán VIP mới
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequestDto request)
        {
            // Kiểm tra user
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new { message = "User not found" });

            // Lấy thông tin tài khoản ngân hàng mặc định
            var bankAccount = await _bankAccountRepository.GetDefaultBankAccountAsync();
            if (bankAccount == null)
                return BadRequest(new { message = "No default bank account configured. Please contact admin." });

            // Xác định giá tiền dựa trên loại gói
            decimal amount = request.SubscriptionType.ToLower() switch
            {
                "monthly" => 49000,    // 49,000 VND - 1 tháng
                "quarterly" => 129000, // 129,000 VND - 3 tháng
                "yearly" => 499000,    // 499,000 VND - 12 tháng
                _ => 0
            };

            if (amount == 0)
                return BadRequest(new { message = "Invalid subscription type. Use 'Monthly', 'Quarterly', or 'Yearly'." });

            // Tạo mã đơn hàng
            var orderCode = _vietQRService.GenerateOrderCode();

            // Tạo nội dung chuyển khoản
            var transferContent = $"PREPAY {orderCode}";

            // Tạo QR code URL
            var qrCodeUrl = _vietQRService.GenerateVietQRUrl(
                bankAccount.BankCode,
                bankAccount.AccountNumber,
                bankAccount.AccountHolderName,
                amount,
                transferContent
            );

            // Tạo payment record
            var payment = new Payment
            {
                OrderCode = orderCode,
                UserId = request.UserId,
                SubscriptionType = request.SubscriptionType,
                Amount = amount,
                Status = "Pending",
                TransferContent = transferContent,
                BankAccountNumber = bankAccount.AccountNumber,
                BankName = bankAccount.BankName,
                AccountHolderName = bankAccount.AccountHolderName,
                QRCodeUrl = qrCodeUrl,
                CreatedAt = DateTime.Now,
                ExpiredAt = DateTime.Now.AddHours(24) // Đơn hàng hết hạn sau 24 giờ
            };

            await _paymentRepository.AddAsync(payment);

            // Tạo response
            var response = new PaymentResponseDto
            {
                PaymentId = payment.PaymentId,
                OrderCode = payment.OrderCode,
                UserId = payment.UserId,
                UserFullName = user.FullName,
                UserEmail = user.Email,
                SubscriptionType = payment.SubscriptionType,
                Amount = payment.Amount,
                Status = payment.Status,
                TransferContent = payment.TransferContent,
                BankAccountNumber = payment.BankAccountNumber,
                BankName = payment.BankName,
                AccountHolderName = payment.AccountHolderName,
                QRCodeUrl = payment.QRCodeUrl,
                CreatedAt = payment.CreatedAt,
                ExpiredAt = payment.ExpiredAt
            };

            return Ok(response);
        }

        /// <summary>
        /// Kiểm tra trạng thái thanh toán theo OrderCode
        /// </summary>
        [HttpGet("check/{orderCode}")]
        public async Task<IActionResult> CheckPaymentStatus(string orderCode)
        {
            var payment = await _paymentRepository.GetByOrderCodeAsync(orderCode);
            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            var response = new PaymentResponseDto
            {
                PaymentId = payment.PaymentId,
                OrderCode = payment.OrderCode,
                UserId = payment.UserId,
                UserFullName = payment.User?.FullName ?? "",
                UserEmail = payment.User?.Email ?? "",
                SubscriptionType = payment.SubscriptionType,
                Amount = payment.Amount,
                Status = payment.Status,
                TransferContent = payment.TransferContent ?? "",
                BankAccountNumber = payment.BankAccountNumber ?? "",
                BankName = payment.BankName ?? "",
                AccountHolderName = payment.AccountHolderName ?? "",
                QRCodeUrl = payment.QRCodeUrl,
                CreatedAt = payment.CreatedAt,
                CompletedAt = payment.CompletedAt,
                ExpiredAt = payment.ExpiredAt,
                Note = payment.Note
            };

            return Ok(response);
        }

        /// <summary>
        /// Admin: Lấy danh sách tất cả đơn hàng chờ thanh toán
        /// </summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingPayments()
        {
            var payments = await _paymentRepository.GetPendingPaymentsAsync();

            var response = payments.Select(p => new PaymentResponseDto
            {
                PaymentId = p.PaymentId,
                OrderCode = p.OrderCode,
                UserId = p.UserId,
                UserFullName = p.User?.FullName ?? "",
                UserEmail = p.User?.Email ?? "",
                SubscriptionType = p.SubscriptionType,
                Amount = p.Amount,
                Status = p.Status,
                TransferContent = p.TransferContent ?? "",
                BankAccountNumber = p.BankAccountNumber ?? "",
                BankName = p.BankName ?? "",
                AccountHolderName = p.AccountHolderName ?? "",
                QRCodeUrl = p.QRCodeUrl,
                CreatedAt = p.CreatedAt,
                ExpiredAt = p.ExpiredAt,
                Note = p.Note
            }).ToList();

            return Ok(response);
        }

        /// <summary>
        /// Lấy lịch sử thanh toán của user
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserPayments(int userId)
        {
            var payments = await _paymentRepository.GetPaymentsByUserIdAsync(userId);

            var response = payments.Select(p => new PaymentResponseDto
            {
                PaymentId = p.PaymentId,
                OrderCode = p.OrderCode,
                UserId = p.UserId,
                UserFullName = "",
                UserEmail = "",
                SubscriptionType = p.SubscriptionType,
                Amount = p.Amount,
                Status = p.Status,
                TransferContent = p.TransferContent ?? "",
                BankAccountNumber = p.BankAccountNumber ?? "",
                BankName = p.BankName ?? "",
                AccountHolderName = p.AccountHolderName ?? "",
                QRCodeUrl = p.QRCodeUrl,
                CreatedAt = p.CreatedAt,
                CompletedAt = p.CompletedAt,
                ExpiredAt = p.ExpiredAt,
                Note = p.Note
            }).ToList();

            return Ok(response);
        }

        /// <summary>
        /// Admin: Xác nhận thanh toán thành công
        /// </summary>
        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentDto request)
        {
            // Kiểm tra admin
            var admin = await _userRepository.GetByIdAsync(request.AdminId);
            if (admin == null || !admin.IsAdmin)
                return Unauthorized(new { message = "Only admin can confirm payments" });

            // Lấy payment
            var payment = await _paymentRepository.GetPaymentWithUserAsync(request.PaymentId);
            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            if (payment.Status != "Pending")
                return BadRequest(new { message = $"Payment is already {payment.Status}" });

            // Cập nhật payment
            payment.Status = "Completed";
            payment.CompletedAt = DateTime.Now;
            payment.ConfirmedByAdminId = request.AdminId;
            payment.Note = request.Note;
            await _paymentRepository.UpdateAsync(payment);

            // Kích hoạt VIP cho user
            var user = payment.User;
            if (user == null)
                return BadRequest(new { message = "User not found in payment record" });

            DateTime startDate = DateTime.Now;
            DateTime endDate = payment.SubscriptionType.ToLower() switch
            {
                "monthly" => startDate.AddMonths(1),
                "quarterly" => startDate.AddMonths(3),
                "yearly" => startDate.AddYears(1),
                _ => startDate.AddMonths(1)
            };

            // Nếu user đã có VIP active, gia hạn thêm
            if (user.IsVip && user.VipExpiryDate.HasValue && user.VipExpiryDate > DateTime.Now)
            {
                startDate = user.VipExpiryDate.Value;
                endDate = payment.SubscriptionType.ToLower() switch
                {
                    "monthly" => startDate.AddMonths(1),
                    "quarterly" => startDate.AddMonths(3),
                    "yearly" => startDate.AddYears(1),
                    _ => startDate.AddMonths(1)
                };
            }

            // Tạo VIP subscription
            var subscription = new VipSubscription
            {
                UserId = user.UserId,
                StartDate = startDate,
                EndDate = endDate,
                SubscriptionType = payment.SubscriptionType,
                Price = payment.Amount,
                PaymentMethod = "Bank Transfer - VietQR",
                TransactionId = payment.OrderCode,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            await _vipSubscriptionRepository.AddAsync(subscription);

            // Cập nhật user status
            user.IsVip = true;
            user.VipExpiryDate = endDate;
            await _userRepository.UpdateAsync(user);

            // Tạo thông báo cho user
            var subscriptionTypeText = payment.SubscriptionType.ToLower() switch
            {
                "monthly" => "1 tháng",
                "quarterly" => "3 tháng",
                "yearly" => "12 tháng",
                _ => payment.SubscriptionType
            };
            
            var notification = new Notification
            {
                UserId = user.UserId,
                Message = $"Bạn đã đăng ký thành công gói VIP {subscriptionTypeText}! Tài khoản VIP của bạn có hiệu lực đến {endDate.ToString("dd/MM/yyyy")}.",
                IsRead = false,
                SentAt = DateTime.Now
            };
            await _notificationRepository.AddAsync(notification);

            return Ok(new
            {
                message = "Payment confirmed successfully. VIP activated.",
                payment = new PaymentResponseDto
                {
                    PaymentId = payment.PaymentId,
                    OrderCode = payment.OrderCode,
                    UserId = payment.UserId,
                    UserFullName = user.FullName,
                    UserEmail = user.Email,
                    SubscriptionType = payment.SubscriptionType,
                    Amount = payment.Amount,
                    Status = payment.Status,
                    TransferContent = payment.TransferContent ?? "",
                    BankAccountNumber = payment.BankAccountNumber ?? "",
                    BankName = payment.BankName ?? "",
                    AccountHolderName = payment.AccountHolderName ?? "",
                    CreatedAt = payment.CreatedAt,
                    CompletedAt = payment.CompletedAt,
                    Note = payment.Note
                },
                vipExpiryDate = endDate
            });
        }

        /// <summary>
        /// Admin: Hủy đơn thanh toán
        /// </summary>
        [HttpPost("cancel/{paymentId}")]
        public async Task<IActionResult> CancelPayment(int paymentId, [FromBody] ConfirmPaymentDto request)
        {
            // Kiểm tra admin
            var admin = await _userRepository.GetByIdAsync(request.AdminId);
            if (admin == null || !admin.IsAdmin)
                return Unauthorized(new { message = "Only admin can cancel payments" });

            // Lấy payment
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            if (payment.Status != "Pending")
                return BadRequest(new { message = $"Cannot cancel payment with status: {payment.Status}" });

            // Hủy payment
            payment.Status = "Cancelled";
            payment.Note = request.Note;
            await _paymentRepository.UpdateAsync(payment);

            return Ok(new { message = "Payment cancelled successfully", payment });
        }

        /// <summary>
        /// Cron job: Tự động hủy các đơn hàng quá hạn
        /// </summary>
        [HttpPost("expire-old-payments")]
        public async Task<IActionResult> ExpireOldPayments()
        {
            var expiredPayments = await _paymentRepository.GetExpiredPaymentsAsync();

            foreach (var payment in expiredPayments)
            {
                payment.Status = "Expired";
                await _paymentRepository.UpdateAsync(payment);
            }

            return Ok(new
            {
                message = $"Expired {expiredPayments.Count} payments",
                count = expiredPayments.Count
            });
        }

        /// <summary>
        /// Admin: Lấy tất cả đơn hàng (có phân trang)
        /// </summary>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var allPayments = await _paymentRepository.GetAllAsync();
            var totalCount = allPayments.Count();
            
            var payments = allPayments
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = payments.Select(p => new PaymentResponseDto
            {
                PaymentId = p.PaymentId,
                OrderCode = p.OrderCode,
                UserId = p.UserId,
                UserFullName = "",
                UserEmail = "",
                SubscriptionType = p.SubscriptionType,
                Amount = p.Amount,
                Status = p.Status,
                TransferContent = p.TransferContent ?? "",
                BankAccountNumber = p.BankAccountNumber ?? "",
                BankName = p.BankName ?? "",
                AccountHolderName = p.AccountHolderName ?? "",
                CreatedAt = p.CreatedAt,
                CompletedAt = p.CompletedAt,
                ExpiredAt = p.ExpiredAt,
                Note = p.Note
            }).ToList();

            return Ok(new
            {
                payments = response,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
    }
}
