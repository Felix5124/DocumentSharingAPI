using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using DocumentSharingAPI.Services;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly AppDbContext _context;
        private readonly IBlobService _blob;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public UsersController(IUserRepository userRepository, AppDbContext context, IBlobService blob, IEmailService emailService, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _context = context;
            _blob = blob;
            _emailService = emailService;
            _configuration = configuration;
        }

        // Helper: chuẩn hóa tên file avatar (bỏ prefix “avatars/” nếu có)
        private string NormalizeAvatar(string avatar)
        {
            if (string.IsNullOrEmpty(avatar))
                return "default-avatar.png";

            return avatar.StartsWith("avatars/", StringComparison.OrdinalIgnoreCase)
                ? avatar.Substring("avatars/".Length)
                : avatar;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            var existingUser = await _userRepository.GetByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest("Email already exists.");

            var userArgs = new UserRecordArgs
            {
                Email = model.Email,
                Password = model.Password,
                DisplayName = model.FullName
            };
            var firebaseUser = await FirebaseAuth.DefaultInstance.CreateUserAsync(userArgs);

            // Tạo token xác thực email
            var verificationToken = Guid.NewGuid().ToString();
            var tokenExpiry = DateTime.UtcNow.AddHours(24);

            var user = new User
            {
                Email = model.Email,
                FullName = model.FullName,
                FirebaseUid = firebaseUser.Uid,
                AvatarUrl = "default-avatar.png",
                CreatedAt = DateTime.Now,
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpiry = tokenExpiry,
                IsEmailVerified = false
            };
            await _userRepository.AddAsync(user);

            // Tạo link xác thực
            var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:5173";
            var verificationLink = $"{frontendUrl}/verify-email?token={verificationToken}&email={Uri.EscapeDataString(model.Email)}";

            // Gửi email xác thực
            try
            {
                await _emailService.SendVerificationEmailAsync(model.Email, verificationLink);
            }
            catch (Exception ex)
            {
                // Log lỗi nhưng vẫn cho phép đăng ký thành công
                Console.WriteLine($"Failed to send verification email: {ex.Message}");
            }

            return Ok(new { Message = "User registered successfully. Please check your email to verify your account.", UserId = user.UserId });
        }

        [HttpPost("authprovider-register")]
        public async Task<IActionResult> AuthProviderRegister([FromBody] AuthProviderRegisterModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.FirebaseUid) || string.IsNullOrWhiteSpace(model.Email))
                return BadRequest("Thông tin đăng ký không hợp lệ.");

            // Kiểm tra user đã tồn tại theo FirebaseUid
            var existingUserByUid = await _userRepository.GetByFirebaseUidAsync(model.FirebaseUid);
            if (existingUserByUid != null)
            {
                Console.WriteLine($"User với FirebaseUid {model.FirebaseUid} đã tồn tại, trả về user hiện có.");
                return Ok(existingUserByUid);
            }

            // Kiểm tra user đã tồn tại theo Email
            var existingUserByEmail = await _userRepository.GetByEmailAsync(model.Email);
            if (existingUserByEmail != null)
                return Conflict(new { message = "Email này đã được đăng ký trong hệ thống với một tài khoản khác." });

            try
            {
                var user = new User
                {
                    FirebaseUid = model.FirebaseUid,
                    Email = model.Email,
                    FullName = model.FullName,
                    IsVip = false,
                    IsAdmin = false,
                    IsLocked = false,
                    IsEmailVerified = true, // OAuth providers (Google/Facebook) have verified email
                    AvatarUrl = "default-avatar.png",
                    CreatedAt = DateTime.UtcNow
                };

                await _userRepository.AddAsync(user);

                var createdUser = await _userRepository.GetByFirebaseUidAsync(user.FirebaseUid);
                if (createdUser == null)
                    return StatusCode(StatusCodes.Status500InternalServerError, "Không thể tạo người dùng mới trong cơ sở dữ liệu.");

                Console.WriteLine($"User mới được tạo thành công: {createdUser.UserId}, FirebaseUid: {createdUser.FirebaseUid}");
                return Ok(createdUser);
            }
            catch (Exception ex)
            {
                // Xử lý race condition: nếu user vừa được tạo bởi request khác
                Console.WriteLine($"Lỗi khi tạo user (có thể do race condition): {ex.Message}");

                // Thử lấy lại user đã tồn tại
                var retryUser = await _userRepository.GetByFirebaseUidAsync(model.FirebaseUid);
                if (retryUser != null)
                {
                    Console.WriteLine($"User được tạo bởi request song song, trả về user hiện có: {retryUser.UserId}");
                    return Ok(retryUser);
                }

                return StatusCode(StatusCodes.Status500InternalServerError, "Lỗi khi tạo người dùng mới: " + ex.Message);
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _userRepository.GetByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized("Email hoặc mật khẩu không hợp lệ.");
            if (user.IsLocked)
                return Unauthorized("Tài khoản của bạn đã bị khóa.");

            string firebaseToken = await FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(user.FirebaseUid);

            return Ok(new
            {
                token = firebaseToken,
                user = new
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    FullName = user.FullName,
                    CheckAdmin = user.IsAdmin,
                    IsVip = user.IsVip,
                    VipExpiryDate = user.VipExpiryDate
                }
            });
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userRepository.GetAllAsync();
            return Ok(users.Select(u => new
            {
                u.UserId,
                u.Email,
                u.FullName,
                avatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(u.AvatarUrl), TimeSpan.FromHours(1)),
                u.IsVip,
                u.VipExpiryDate,
                u.IsAdmin,
                u.IsLocked,
                u.CreatedAt,
                u.CommentCount
            }));
        }

        [HttpGet("by-uid/{uid}")]
        public async Task<IActionResult> GetByUid(string uid)
        {
            var user = await _userRepository.GetByFirebaseUidAsync(uid);
            if (user == null)
                return NotFound();

            var avatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(user.AvatarUrl), TimeSpan.FromHours(1));

            return Ok(new
            {
                user.UserId,
                user.Email,
                user.FullName,
                user.FirebaseUid,
                avatarUrl,
                user.IsVip,
                user.VipExpiryDate,
                user.IsAdmin,
                user.IsLocked,
                user.IsEmailVerified,
                user.CreatedAt,
                user.CommentCount,
                user.VipDownloadsUsedToday,
                user.RegularDownloadsUsedToday,
                user.VipBonusDownloads,
                user.RegularBonusDownloads,
                user.LastDownloadResetDate
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            var avatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(user.AvatarUrl), TimeSpan.FromHours(1));

            return Ok(new
            {
                user.UserId,
                user.Email,
                user.FullName,
                avatarUrl,
                user.IsVip,
                user.VipExpiryDate,
                user.IsAdmin,
                user.IsLocked,
                user.IsEmailVerified,
                user.CreatedAt,
                user.CommentCount,
                user.VipDownloadsUsedToday,
                user.RegularDownloadsUsedToday,
                user.VipBonusDownloads,
                user.RegularBonusDownloads,
                user.LastDownloadResetDate
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserModel model)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            user.FullName = model.FullName ?? user.FullName;
            user.AvatarUrl = model.AvatarUrl ?? user.AvatarUrl;
            // Bio field - nullable, only update if provided (backward compatible)
            if (model.Bio != null)
                user.Bio = model.Bio;

            await _userRepository.UpdateAsync(user);
            return Ok(user);
        }

        [HttpPut("{id}/settings")]
        public async Task<IActionResult> UpdateSettings(int id, [FromBody] UserSettingsModel model)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                // Serialize settings to JSON string
                var json = System.Text.Json.JsonSerializer.Serialize(new {
                    notifications = new {
                        email = model.EmailNotifications,
                        push = model.PushNotifications,
                        sound = model.SoundEnabled
                    },
                    display = new {
                        language = model.Language ?? "vi",
                        darkMode = model.DarkMode,
                        gridColumns = model.GridColumns
                    },
                    privacy = new {
                        profileVisibility = model.ProfileVisibility ?? "public",
                        showEmail = model.ShowEmail,
                        allowFollow = model.AllowFollow
                    }
                });

                user.Settings = json;
                await _userRepository.UpdateAsync(user);
                
                return Ok(new { message = "Settings updated successfully", settings = json });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating settings: {ex.Message}");
                return StatusCode(500, new { message = "Error updating settings" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            if (!string.IsNullOrEmpty(user.AvatarUrl) &&
                !user.AvatarUrl.Equals("default-avatar.png", StringComparison.OrdinalIgnoreCase))
            {
                await _blob.DeleteAsync("avatars", NormalizeAvatar(user.AvatarUrl));
            }

            await _userRepository.DeleteAsync(id);

            if (!string.IsNullOrEmpty(user.FirebaseUid))
                await FirebaseAuth.DefaultInstance.DeleteUserAsync(user.FirebaseUid);

            return NoContent();
        }

        [HttpPost("{id}/avatar")]
        public async Task<IActionResult> UploadAvatar(int id, IFormFile file)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);
                if (user == null)
                    return NotFound(new { message = "Người dùng không tồn tại." });

                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "Không có file tải lên." });

                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                    return BadRequest(new { message = "Chỉ hỗ trợ .jpg, .jpeg, .png, .gif." });

                // Xóa avatar cũ nếu không phải mặc định
                if (!string.IsNullOrEmpty(user.AvatarUrl) &&
                    !user.AvatarUrl.Equals("default-avatar.png", StringComparison.OrdinalIgnoreCase))
                {
                    await _blob.DeleteAsync("avatars", NormalizeAvatar(user.AvatarUrl));
                }

                var blobName = $"{id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
                await using var s = file.OpenReadStream();
                await _blob.UploadAsync("avatars", blobName, s, file.ContentType);

                user.AvatarUrl = blobName;
                await _userRepository.UpdateAsync(user);

                var sas = _blob.GetReadSasUrl("avatars", user.AvatarUrl, TimeSpan.FromMinutes(10));
                return Ok(new { message = "OK", avatarUrl = sas, blob = user.AvatarUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        [HttpGet("ranking")]
        public async Task<IActionResult> GetRanking([FromQuery] int limit = 10)
        {
            var users = await _userRepository.GetAllAsync();
            var topUsers = users.Where(u => u.IsVip || (u.UploadedDocuments?.Count ?? 0) > 0)
                               .OrderByDescending(u => u.IsVip)
                               .ThenByDescending(u => u.UploadedDocuments?.Count ?? 0)
                               .Take(limit);

            return Ok(topUsers.Select(u => new
            {
                u.UserId,
                u.FullName,
                IsVip = u.IsVip,
                VipStatus = u.IsVip ? "VIP" : "Regular",
                DocumentsUploaded = u.UploadedDocuments?.Count ?? 0
            }));
        }

        [HttpGet("top-commenter")]
        public async Task<IActionResult> GetTopCommenter()
        {
            try
            {
                var topCommenter = await _userRepository.GetTopCommenterAsync();
                if (topCommenter == null)
                    return NotFound("Không có người dùng nào có bình luận.");

                var avatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(topCommenter.AvatarUrl), TimeSpan.FromHours(1));

                return Ok(new
                {
                    topCommenter.UserId,
                    topCommenter.FullName,
                    topCommenter.Email,
                    avatarUrl,
                    topCommenter.CommentCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("top-vip")]
        public async Task<IActionResult> GetTopVipUser()
        {
            try
            {
                var users = await _userRepository.GetAllAsync();
                var topVipUser = users.Where(u => u.IsVip && u.VipExpiryDate > DateTime.Now)
                                     .OrderByDescending(u => u.VipExpiryDate)
                                     .FirstOrDefault();

                if (topVipUser == null)
                    return NotFound("Không có người dùng VIP nào.");

                var avatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(topVipUser.AvatarUrl), TimeSpan.FromHours(1));

                return Ok(new
                {
                    topVipUser.UserId,
                    topVipUser.FullName,
                    topVipUser.Email,
                    avatarUrl,
                    VipStatus = "VIP",
                    topVipUser.VipExpiryDate
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Rankings: Top users by uploads
        [HttpGet("rankings/uploads")]
        public async Task<IActionResult> GetUserRankingsByUploads([FromQuery] int limit = 10)
        {
            try
            {
                var items = await _userRepository.GetTopUsersByUploadsAsync(limit);
                var result = items.Select(u => new
                {
                    userId = u.UserId,
                    fullName = u.FullName,
                    email = u.Email,
                    avatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(u.AvatarUrl), TimeSpan.FromHours(1)),
                    value = u.Value
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Rankings: Top users by comments
        [HttpGet("rankings/comments")]
        public async Task<IActionResult> GetUserRankingsByComments([FromQuery] int limit = 10)
        {
            try
            {
                var items = await _userRepository.GetTopUsersByCommentsAsync(limit);
                var result = items.Select(u => new
                {
                    userId = u.UserId,
                    fullName = u.FullName,
                    email = u.Email,
                    avatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(u.AvatarUrl), TimeSpan.FromHours(1)),
                    value = u.Value
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Rankings: Top users by total document downloads of their uploads
        [HttpGet("rankings/document-downloads")]
        public async Task<IActionResult> GetUserRankingsByDocumentDownloads([FromQuery] int limit = 10)
        {
            try
            {
                var items = await _userRepository.GetTopUsersByDocumentDownloadsAsync(limit);
                var result = items.Select(u => new
                {
                    userId = u.UserId,
                    fullName = u.FullName,
                    email = u.Email,
                    avatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(u.AvatarUrl), TimeSpan.FromHours(1)),
                    totalDownloads = u.Value
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Lock/Unlock user account
        [HttpPut("{id}/lock")]
        public async Task<IActionResult> SetLockStatus(int id, [FromBody] LockUserModel model)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);
                if (user == null)
                    return NotFound(new { message = "User not found." });

                user.IsLocked = model.IsLocked;
                await _userRepository.UpdateAsync(user);

                return Ok(new
                {
                    message = user.IsLocked ? "Tài khoản đã bị khóa." : "Tài khoản đã được mở khóa.",
                    userId = user.UserId,
                    isLocked = user.IsLocked
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        [HttpGet("admin/list")]
        // [Authorize(Roles = "Admin")] // Uncomment nếu đã cấu hình Role chuẩn
        public async Task<IActionResult> GetUsersForAdmin(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string keyword = "",
        [FromQuery] bool? isLocked = null,
        [FromQuery] string? role = null) // role: "admin", "vip", "regular"
        {
            try
            {
                var (users, total) = await _userRepository.GetAdminUsersAsync(page, pageSize, keyword, isLocked, role);

                // Map sang DTO hoặc Anonymous object để trả về
                var result = users.Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.FullName,
                    AvatarUrl = _blob.GetReadSasUrl("avatars", NormalizeAvatar(u.AvatarUrl), TimeSpan.FromHours(1)),
                    u.IsVip,
                    u.VipExpiryDate,
                    u.IsAdmin,
                    u.IsLocked,
                    u.CreatedAt,
                    u.CommentCount
                });

                return Ok(new
                {
                    data = result,
                    total,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)total / pageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi máy chủ: " + ex.Message });
            }
        }

        // API gửi lại email xác thực
        [HttpPost("resend-verification-email")]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendEmailModel model)
        {
            var user = await _userRepository.GetByEmailAsync(model.Email);
            if (user == null)
                return NotFound("Email không tồn tại trong hệ thống.");

            if (user.IsEmailVerified)
                return BadRequest("Email đã được xác thực.");

            // Tạo token mới
            var verificationToken = Guid.NewGuid().ToString();
            var tokenExpiry = DateTime.UtcNow.AddHours(24);

            user.EmailVerificationToken = verificationToken;
            user.EmailVerificationTokenExpiry = tokenExpiry;

            await _userRepository.UpdateAsync(user);

            // Tạo link xác thực
            var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:5173";
            var verificationLink = $"{frontendUrl}/verify-email?token={verificationToken}&email={Uri.EscapeDataString(model.Email)}";

            // Gửi email xác thực
            try
            {
                await _emailService.SendVerificationEmailAsync(model.Email, verificationLink);
                return Ok(new { Message = "Email xác thực đã được gửi lại. Vui lòng kiểm tra hộp thư của bạn." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send verification email: {ex.Message}");
                return StatusCode(500, "Không thể gửi email xác thực. Vui lòng thử lại sau.");
            }
        }

        // API xác thực email bằng token
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailModel model)
        {
            var user = await _userRepository.GetByEmailAsync(model.Email);
            if (user == null)
                return NotFound("Email không tồn tại trong hệ thống.");

            if (user.IsEmailVerified)
                return BadRequest("Email đã được xác thực trước đó.");

            if (string.IsNullOrEmpty(user.EmailVerificationToken))
                return BadRequest("Không tìm thấy token xác thực. Vui lòng yêu cầu gửi lại email xác thực.");

            if (user.EmailVerificationToken != model.Token)
                return BadRequest("Token xác thực không hợp lệ.");

            if (user.EmailVerificationTokenExpiry == null || user.EmailVerificationTokenExpiry < DateTime.UtcNow)
                return BadRequest("Token xác thực đã hết hạn. Vui lòng yêu cầu gửi lại email xác thực.");

            // Xác thực thành công
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;

            await _userRepository.UpdateAsync(user);

            // Cập nhật emailVerified trong Firebase
            try
            {
                if (!string.IsNullOrEmpty(user.FirebaseUid))
                {
                    var userRecordArgs = new UserRecordArgs
                    {
                        Uid = user.FirebaseUid,
                        EmailVerified = true
                    };
                    await FirebaseAuth.DefaultInstance.UpdateUserAsync(userRecordArgs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi cập nhật emailVerified trong Firebase: {ex.Message}");
                // Không return error vì backend đã verify thành công
            }

            return Ok(new { Message = "Email đã được xác thực thành công!" });
        }

        // API kiểm tra trạng thái xác thực email
        [HttpGet("check-email-verification/{email}")]
        public async Task<IActionResult> CheckEmailVerification(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                return NotFound("Email không tồn tại trong hệ thống.");

            return Ok(new { IsEmailVerified = user.IsEmailVerified });
        }

        // API admin: Sync Firebase emailVerified cho user đã verify backend
        [HttpPost("admin/sync-firebase-email-verified/{email}")]
        public async Task<IActionResult> SyncFirebaseEmailVerified(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                return NotFound("Email không tồn tại trong hệ thống.");

            if (!user.IsEmailVerified)
                return BadRequest("User chưa verify email trong backend.");

            if (string.IsNullOrEmpty(user.FirebaseUid))
                return BadRequest("User không có FirebaseUid.");

            try
            {
                var userRecordArgs = new UserRecordArgs
                {
                    Uid = user.FirebaseUid,
                    EmailVerified = true
                };
                await FirebaseAuth.DefaultInstance.UpdateUserAsync(userRecordArgs);
                return Ok(new { Message = $"Đã sync emailVerified cho Firebase user {user.FirebaseUid}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Lỗi khi update Firebase: {ex.Message}" });
            }
        }

        // API admin: Update IsEmailVerified cho các user cũ (chỉ dùng 1 lần)
        [HttpPost("admin/verify-all-existing-users")]
        public async Task<IActionResult> VerifyAllExistingUsers()
        {
            var allUsers = await _context.Users
                .Where(u => !u.IsEmailVerified && u.CreatedAt < DateTime.Parse("2025-12-09"))
                .ToListAsync();

            foreach (var user in allUsers)
            {
                user.IsEmailVerified = true;
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenExpiry = null;
            }

            await _context.SaveChangesAsync();

            return Ok(new { 
                Message = $"Đã update {allUsers.Count} users thành IsEmailVerified = true",
                Count = allUsers.Count
            });
        }

        // API: Đổi mật khẩu
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.CurrentPassword) || string.IsNullOrEmpty(model.NewPassword))
            {
                return BadRequest(new { message = "Email, mật khẩu hiện tại và mật khẩu mới không được để trống." });
            }

            // Kiểm tra độ dài mật khẩu mới
            if (model.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự." });
            }

            try
            {
                // Lấy user từ database
                var user = await _userRepository.GetByEmailAsync(model.Email);
                if (user == null)
                {
                    return NotFound(new { message = "Không tìm thấy người dùng." });
                }

                // Debug log
                Console.WriteLine($"[ChangePassword] User found: {user.Email}");
                Console.WriteLine($"[ChangePassword] User FirebaseUid: {user.FirebaseUid}");
                Console.WriteLine($"[ChangePassword] Has FirebaseUid: {!string.IsNullOrEmpty(user.FirebaseUid)}");

                // Kiểm tra nếu user không có FirebaseUid (không nên xảy ra)
                if (string.IsNullOrEmpty(user.FirebaseUid))
                {
                    Console.WriteLine($"[ChangePassword] ERROR: User {user.Email} has no FirebaseUid!");
                    return BadRequest(new { message = "Tài khoản không hợp lệ. Vui lòng liên hệ hỗ trợ." });
                }

                // Xác thực mật khẩu hiện tại với Firebase
                try
                {
                    Console.WriteLine($"[ChangePassword] Getting Firebase user for UID: {user.FirebaseUid}");
                    var authLink = await FirebaseAuth.DefaultInstance.GetUserAsync(user.FirebaseUid);
                    Console.WriteLine($"[ChangePassword] Firebase user found. Providers: {string.Join(", ", authLink.ProviderData.Select(p => p.ProviderId))}");
                    
                    // Kiểm tra xem user có đăng ký bằng provider (Google/Facebook) không
                    var hasPasswordProvider = authLink.ProviderData.Any(p => p.ProviderId == "password");
                    Console.WriteLine($"[ChangePassword] Has password provider: {hasPasswordProvider}");
                    
                    if (!hasPasswordProvider)
                    {
                        Console.WriteLine($"[ChangePassword] User {user.Email} registered with OAuth provider, cannot change password");
                        return BadRequest(new { message = "Tài khoản này đăng ký bằng Google/Facebook nên không có mật khẩu. Không thể đổi mật khẩu." });
                    }

                    // Thử đăng nhập với mật khẩu hiện tại để xác thực
                    // Note: Firebase Admin SDK không có phương thức verify password trực tiếp
                    // Nên ta cần dùng Firebase REST API
                    Console.WriteLine($"[ChangePassword] Verifying current password for {model.Email}");
                    var firebaseApiKey = _configuration["Firebase:ApiKey"];
                    var verifyUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={firebaseApiKey}";
                    
                    using var httpClient = new HttpClient();
                    var verifyContent = new
                    {
                        email = model.Email,
                        password = model.CurrentPassword,
                        returnSecureToken = true
                    };
                    
                    var verifyJson = System.Text.Json.JsonSerializer.Serialize(verifyContent);
                    var verifyResponse = await httpClient.PostAsync(verifyUrl, new StringContent(verifyJson, System.Text.Encoding.UTF8, "application/json"));
                    
                    if (!verifyResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await verifyResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[ChangePassword] Password verification failed: {errorContent}");
                        return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
                    }
                    
                    Console.WriteLine($"[ChangePassword] Current password verified successfully");

                    // Đổi mật khẩu trong Firebase
                    Console.WriteLine($"[ChangePassword] Updating password in Firebase for {user.Email}");
                    var userRecordArgs = new UserRecordArgs
                    {
                        Uid = user.FirebaseUid,
                        Password = model.NewPassword
                    };

                    await FirebaseAuth.DefaultInstance.UpdateUserAsync(userRecordArgs);
                    Console.WriteLine($"[ChangePassword] Password updated successfully for {user.Email}");

                    return Ok(new { message = "Đổi mật khẩu thành công." });
                }
                catch (FirebaseAuthException ex)
                {
                    Console.WriteLine($"[ChangePassword] FirebaseAuthException: {ex.Message}");
                    return BadRequest(new { message = $"Lỗi Firebase: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChangePassword] Unexpected error: {ex.Message}");
                Console.WriteLine($"[ChangePassword] StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
                return BadRequest(new { message = "Email không được để trống." });

            try
            {
                var user = await _userRepository.GetByEmailAsync(model.Email);
                if (user == null)
                {
                    // Không tiết lộ email có tồn tại hay không (security best practice)
                    return Ok(new { message = "Nếu email tồn tại, chúng tôi đã gửi link đặt lại mật khẩu." });
                }

                // Chỉ user đăng ký bằng email/password mới được reset password
                if (!string.IsNullOrEmpty(user.FirebaseUid))
                {
                    var firebaseUser = await FirebaseAuth.DefaultInstance.GetUserAsync(user.FirebaseUid);
                    var hasPasswordProvider = firebaseUser.ProviderData.Any(p => p.ProviderId == "password");
                    
                    if (!hasPasswordProvider)
                    {
                        return BadRequest(new { message = "Tài khoản của bạn đăng nhập bằng Google/Facebook, không thể đặt lại mật khẩu." });
                    }
                }

                // Generate reset token
                var resetToken = Guid.NewGuid().ToString();
                user.PasswordResetToken = resetToken;
                user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1); // Token expires in 1 hour

                await _userRepository.UpdateAsync(user);

                // Create reset link
                var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:5173";
                var resetLink = $"{frontendUrl}/reset-password?token={resetToken}&email={Uri.EscapeDataString(user.Email)}";

                // Send email
                await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);

                Console.WriteLine($"[ForgotPassword] Reset email sent to {user.Email}, token expires at {user.PasswordResetTokenExpiry}");

                return Ok(new { message = "Email đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra hộp thư." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ForgotPassword] Error: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi xử lý yêu cầu: " + ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Token) || string.IsNullOrWhiteSpace(model.NewPassword))
                return BadRequest(new { message = "Thông tin không hợp lệ." });

            if (model.NewPassword.Length < 6)
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự." });

            try
            {
                var user = await _userRepository.GetByEmailAsync(model.Email);
                if (user == null)
                    return BadRequest(new { message = "Token không hợp lệ hoặc đã hết hạn." });

                // Verify token
                if (user.PasswordResetToken != model.Token)
                {
                    Console.WriteLine($"[ResetPassword] Token mismatch for {model.Email}");
                    return BadRequest(new { message = "Token không hợp lệ hoặc đã hết hạn." });
                }

                if (user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
                {
                    Console.WriteLine($"[ResetPassword] Token expired for {model.Email}");
                    return BadRequest(new { message = "Token đã hết hạn. Vui lòng yêu cầu đặt lại mật khẩu mới." });
                }

                // Update password in Firebase
                if (string.IsNullOrEmpty(user.FirebaseUid))
                    return BadRequest(new { message = "Không thể đặt lại mật khẩu cho tài khoản này." });

                var userUpdate = new UserRecordArgs
                {
                    Uid = user.FirebaseUid,
                    Password = model.NewPassword
                };

                await FirebaseAuth.DefaultInstance.UpdateUserAsync(userUpdate);

                // Clear reset token
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpiry = null;
                await _userRepository.UpdateAsync(user);

                Console.WriteLine($"[ResetPassword] Password reset successfully for {model.Email}");

                return Ok(new { message = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResetPassword] Error: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi đặt lại mật khẩu: " + ex.Message });
            }
        }
    }

    // Models
    public class RegisterModel
    {
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public class LoginModel
    {
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
    }

    public class UpdateUserModel
    {
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }  // Nullable - backward compatible với web
    }

    public class VipStatusModel
    {
        public bool IsVip { get; set; }
    }

    public class LockUserModel
    {
        public bool IsLocked { get; set; }
    }

    public class UserSettingsModel
    {
        // Notification settings
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        public bool SoundEnabled { get; set; } = true;

        // Display settings
        public string? Language { get; set; } = "vi";
        public bool DarkMode { get; set; } = false;
        public int GridColumns { get; set; } = 2;

        // Privacy settings
        public string? ProfileVisibility { get; set; } = "public";  // public, friends, private
        public bool ShowEmail { get; set; } = false;
        public bool AllowFollow { get; set; } = true;
    }

    public class AuthProviderRegisterModel
    {
        public string FirebaseUid { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public class ResendEmailModel
    {
        public string Email { get; set; } = string.Empty;
    }

    public class VerifyEmailModel
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    public class ChangePasswordModel
    {
        public string Email { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ForgotPasswordModel
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordModel
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
