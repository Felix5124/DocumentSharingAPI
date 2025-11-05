using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
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

        public UsersController(IUserRepository userRepository, AppDbContext context, IBlobService blob)
        {
            _userRepository = userRepository;
            _context = context;
            _blob = blob;
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

            var user = new User
            {
                Email = model.Email,
                FullName = model.FullName,
                FirebaseUid = firebaseUser.Uid,
                AvatarUrl = "default-avatar.png",
                CreatedAt = DateTime.Now
            };
            await _userRepository.AddAsync(user);

            var verificationLink = await FirebaseAuth.DefaultInstance.GenerateEmailVerificationLinkAsync(model.Email);
            return Ok(new { Message = "User registered successfully. Please verify your email.", UserId = user.UserId });
        }

        [HttpPost("authprovider-register")]
        public async Task<IActionResult> AuthProviderRegister([FromBody] AuthProviderRegisterModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.FirebaseUid) || string.IsNullOrWhiteSpace(model.Email))
                return BadRequest("Thông tin đăng ký không hợp lệ.");

            var existingUserByUid = await _userRepository.GetByFirebaseUidAsync(model.FirebaseUid);
            if (existingUserByUid != null)
                return Ok(existingUserByUid);

            var existingUserByEmail = await _userRepository.GetByEmailAsync(model.Email);
            if (existingUserByEmail != null)
                return Conflict(new { message = "Email này đã được đăng ký trong hệ thống với một tài khoản khác." });

            var user = new User
            {
                FirebaseUid = model.FirebaseUid,
                Email = model.Email,
                FullName = model.FullName,
                IsVip = false,
                IsAdmin = false,
                IsLocked = false,
                AvatarUrl = "default-avatar.png",
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);

            var createdUser = await _userRepository.GetByFirebaseUidAsync(user.FirebaseUid);
            if (createdUser == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Không thể tạo người dùng mới trong cơ sở dữ liệu.");

            return Ok(createdUser);
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
                user.CreatedAt,
                user.CommentCount
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
                user.CreatedAt,
                user.CommentCount
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

            await _userRepository.UpdateAsync(user);
            return Ok(user);
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
    }

    public class VipStatusModel
    {
        public bool IsVip { get; set; }
    }

    public class LockUserModel
    {
        public bool IsLocked { get; set; }
    }

    public class AuthProviderRegisterModel
    {
        public string FirebaseUid { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }
}
