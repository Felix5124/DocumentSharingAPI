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

        public UsersController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
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
                CreatedAt = DateTime.Now
            };
            await _userRepository.AddAsync(user);

            var verificationLink = await FirebaseAuth.DefaultInstance.GenerateEmailVerificationLinkAsync(model.Email);
            return Ok(new { Message = "User registered successfully. Please verify your email.", UserId = user.UserId });
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
                    Points = user.Points
                }
            });
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userRepository.GetAllAsync();
            return Ok(users);
        }

        [HttpGet("by-uid/{uid}")]
        public async Task<IActionResult> GetByUid(string uid)
        {
            var user = await _userRepository.GetByFirebaseUidAsync(uid);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();
            return Ok(user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserModel model)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            user.FullName = model.FullName ?? user.FullName;
            user.School = model.School ?? user.School;
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

            await _userRepository.DeleteAsync(id);
            await FirebaseAuth.DefaultInstance.DeleteUserAsync(user.Email);
            return NoContent();
        }

        [HttpPut("{userId}/lock")]
        public async Task<IActionResult> LockUnlockUser(int userId, [FromBody] LockUserModel model)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return NotFound("User not found.");

                if (user.IsAdmin)
                    return BadRequest("Cannot lock/unlock an admin account.");

                await _userRepository.UpdateLockStatusAsync(userId, model.IsLocked);

                if (model.IsLocked)
                {
                    try
                    {
                        await FirebaseAuth.DefaultInstance.RevokeRefreshTokensAsync(user.FirebaseUid);
                        Console.WriteLine($"Revoked tokens for user ID {userId} (Firebase UID: {user.FirebaseUid})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to revoke tokens for user ID {userId}: {ex.Message}");
                    }
                }

                return Ok(new { message = $"Account has been {(model.IsLocked ? "locked" : "unlocked")} successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error while {(model.IsLocked ? "locking" : "unlocking")} account: {ex.Message}" });
            }
        }

        [HttpPost("{id}/points")]
        public async Task<IActionResult> AddPoints(int id, [FromBody] PointsModel model)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            await _userRepository.UpdatePointsAsync(id, model.Points);
            return Ok(new { Message = "Points updated", Points = user.Points, Level = user.Level });
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
                    return BadRequest(new { message = "Không có file được tải lên." });

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return BadRequest(new { message = "Định dạng file không được hỗ trợ. Chỉ hỗ trợ .jpg, .jpeg, .png, .gif." });

                var fileName = $"{id}_{DateTime.Now.Ticks}{extension}";
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
                var filePath = Path.Combine(uploadsFolder, fileName);

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                user.AvatarUrl = $"/avatars/{fileName}";
                await _userRepository.UpdateAsync(user);

                return Ok(new
                {
                    message = "Tải avatar lên thành công.",
                    avatarUrl = user.AvatarUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        [HttpGet("ranking")]
        public async Task<IActionResult> GetRanking([FromQuery] int limit = 10)
        {
            var users = await _userRepository.GetTopUsersAsync(limit);
            return Ok(users.Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Points,
                DocumentsUploaded = u.UploadedDocuments?.Count ?? 0
            }));
        }

        // Thêm endpoint mới: Người có nhiều comment nhất
        [HttpGet("top-commenter")]
        public async Task<IActionResult> GetTopCommenter()
        {
            try
            {
                var topCommenter = await _userRepository.GetTopCommenterAsync();
                if (topCommenter == null)
                    return NotFound("Không có người dùng nào có bình luận.");

                return Ok(topCommenter);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Thêm endpoint mới: Người có nhiều điểm nhất
        [HttpGet("top-points")]
        public async Task<IActionResult> GetTopPointsUser()
        {
            try
            {
                var topUser = await _userRepository.GetTopPointsUserAsync();
                if (topUser == null)
                    return NotFound("Không có người dùng nào có điểm.");

                return Ok(topUser);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

    public class RegisterModel
    {
        public string Email { get; set; }
        public string? Password { get; set; }
        public string FullName { get; set; }
    }

    public class LoginModel
    {
        public string Email { get; set; }
        public string? Password { get; set; }
    }

    public class UpdateUserModel
    {
        public string? FullName { get; set; }
        public string? School { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public class PointsModel
    {
        public int Points { get; set; }
    }

    public class LockUserModel
    {
        public bool IsLocked { get; set; }
    }
}