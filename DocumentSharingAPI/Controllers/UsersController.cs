using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using FirebaseAdmin.Auth.Hash;
using DocumentSharingAPI.Helpers;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using FirebaseAdmin.Auth;

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

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var existingUser = await _userRepository.GetByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest("Email đã tồn tại."); // 409 Conflict

            UserRecordArgs userArgs;
            try
            {
                userArgs = new UserRecordArgs
                {
                    Email = model.Email,
                    Password = model.Password, // Firebase sẽ hash password này
                    DisplayName = model.FullName,
                    EmailVerified = false // Mặc định là chưa xác thực
                };
            }
            catch (ArgumentException ex) // Bắt lỗi nếu password không hợp lệ
            {
                return BadRequest($"Lỗi tạo thông tin người dùng Firebase: {ex.Message}");
            }

            UserRecord firebaseUser;
            try
            {
                firebaseUser = await FirebaseAuth.DefaultInstance.CreateUserAsync(userArgs);
            }
            catch (FirebaseAuthException ex)
            {
                return BadRequest($"Lỗi khi tạo người dùng trên Firebase: {ex.Message}");
            }

            var user = new User
            {
                Email = model.Email,
                FullName = model.FullName,
                FirebaseUid = firebaseUser.Uid, // Lưu Firebase UID
                CreatedAt = DateTime.UtcNow // Sử dụng UtcNow cho thời gian server
            };
            await _userRepository.AddAsync(user); // Repository sẽ SaveChanges

            try
            {
                // Gửi email xác thực (tùy chọn, Firebase có thể tự động làm điều này nếu cấu hình)
                // var verificationLink = await FirebaseAuth.DefaultInstance.GenerateEmailVerificationLinkAsync(model.Email);
                // TODO: Gửi link này qua email cho người dùng
            }
            catch (FirebaseAuthException ex)
            {
                // Log lỗi gửi email nhưng vẫn coi như đăng ký thành công ở bước này
                Console.WriteLine($"Lỗi khi tạo link xác thực email cho {model.Email}: {ex.Message}");
            }

            return Ok(new { Message = "User registered successfully. Please verify your email.", UserId = user.UserId });
        }

        //[HttpPost("login")]
        //public async Task<IActionResult> Login([FromBody] LoginModel model)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(ModelState);
        //    }

        //    var user = await _userRepository.GetByEmailAsync(model.Email);
        //    if (user == null)
        //        return Unauthorized("Email hoặc mật khẩu không đúng.");

        //    if (user.IsLocked)
        //        return Unauthorized("Tài khoản của bạn đã bị khóa.");

        //    var claims = new Dictionary<string, object>
        //    {
        //        // Thêm UserId nội bộ vào claims để helper có thể lấy trực tiếp từ token
        //        // Điều này hữu ích nếu bạn không muốn query DB mỗi lần lấy UserId từ FirebaseUid
        //        { "internal_user_id", user.UserId.ToString() }
        //    };

        //    if (user.IsAdmin)
        //    {
        //        claims.Add("admin", true); // Claim để policy "Admin" hoạt động
        //                                   // Hoặc claims.Add("IsAdmin", "true"); tùy theo định nghĩa policy
        //    }

        //    string firebaseCustomToken;
        //    try
        //    {
        //        firebaseCustomToken = await FirebaseAuth.DefaultInstance
        //            .CreateCustomTokenAsync(user.FirebaseUid, claims);
        //    }
        //    catch (FirebaseAuthException ex)
        //    {
        //        return StatusCode(StatusCodes.Status500InternalServerError, $"Lỗi tạo Firebase custom token: {ex.Message}");
        //    }

        //    return Ok(new
        //    {
        //        token = firebaseCustomToken, // Client sẽ dùng token này để signInWithCustomToken và lấy ID Token
        //        user = new { UserId = user.UserId, user.Email, user.FullName, IsAdmin = user.IsAdmin }
        //    });
        //}


        
        [HttpPost("login")] //Xử lý Firebase ID Token từ client 
        public async Task<IActionResult> Login([FromBody] FirebaseSignInModel model)
        {
            if (!ModelState.IsValid) 
            {
                return BadRequest(ModelState);
            }

            FirebaseToken decodedToken;
            try
            {
                // Xác minh Firebase ID Token nhận từ client
                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(model.IdToken);
            }
            catch (FirebaseAuthException ex)
            {
                // Token không hợp lệ, hết hạn,...
                Console.WriteLine($"Firebase ID Token verification failed: {ex.Message}, Firebase Error: {ex.Message}");
                return Unauthorized(new { Message = "Firebase ID Token không hợp lệ hoặc đã hết hạn.", Details = ex.Message });
            }
            catch (Exception ex) // Các lỗi khác không lường trước
            {
                Console.WriteLine($"An unexpected error occurred during ID Token verification: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Lỗi không xác định trong quá trình xác minh token." });
            }


            string firebaseUid = decodedToken.Uid;

            // BƯỚC 2: Lấy thông tin user từ DB cục bộ bằng Firebase UID đã xác thực
            var user = await _userRepository.GetByFirebaseUidAsync(firebaseUid);

            if (user == null)
            {
                // User đã xác thực với Firebase nhưng không tồn tại trong DB cục bộ của bạn.
                // Điều này có thể xảy ra nếu user đăng ký qua Firebase trực tiếp (nếu bạn cho phép)
                // mà chưa hoàn tất hồ sơ trên hệ thống của bạn, hoặc do thiếu đồng bộ dữ liệu.
                // Tùy vào logic nghiệp vụ, bạn có thể:
                // 1. Trả lỗi Unauthorized.
                // 2. Tự động tạo một bản ghi user mới trong DB cục bộ (nếu có đủ thông tin từ decodedToken).
                Console.WriteLine($"User authenticated with Firebase (UID: {firebaseUid}) but not found in local DB. Email from token: {decodedToken.Claims.GetValueOrDefault("email")}");
                return Unauthorized(new { Message = "Người dùng Firebase hợp lệ nhưng không tìm thấy trong hệ thống cục bộ. Vui lòng hoàn tất đăng ký hoặc liên hệ hỗ trợ." });
            }

            if (user.IsLocked)
            {
                return Unauthorized(new { Message = "Tài khoản của bạn đã bị khóa." });
            }

            // BƯỚC 3: Tạo Firebase Custom Token mới với các claims từ backend
            var claims = new Dictionary<string, object>
            {
                { "internal_user_id", user.UserId.ToString() }
                // Bạn có thể thêm các claims khác từ decodedToken nếu muốn chúng được "refresh" vào custom token mới
                // Ví dụ: { "email_verified", decodedToken.Claims.GetValueOrDefault("email_verified", false) }
            };
            if (user.IsAdmin)
            {
                claims.Add("admin", true);
            }

            // (Tùy chọn) Nếu muốn các claims này (ví dụ: admin, internal_user_id) được cập nhật
            // vĩnh viễn vào Firebase User record để các ID token sau này tự có, bạn có thể gọi:
            // try
            // {
            //     await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(firebaseUid, claims);
            // }
            // catch(FirebaseAuthException ex)
            // {
            //    Console.WriteLine($"Error setting custom claims on Firebase user {firebaseUid}: {ex.Message}");
            //    // Không nên chặn luồng đăng nhập vì lỗi này, nhưng cần log lại
            // }


            string backendCustomToken;
            try
            {
                backendCustomToken = await FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(firebaseUid, claims);
            }
            catch (FirebaseAuthException ex)
            {
                Console.WriteLine($"Error creating backend custom token for UID {firebaseUid}: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Lỗi tạo custom token từ backend.", Details = ex.Message });
            }

            return Ok(new
            {
                token = backendCustomToken, // Client sẽ dùng token này để signInWithCustomToken
                user = new { user.UserId, user.Email, user.FullName, IsAdmin = user.IsAdmin, user.AvatarUrl, user.School, user.Points, user.Level } // Trả về thông tin user đầy đủ hơn
            });
        }

        [HttpGet("all")]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userRepository.GetAllAsync();
            var userDtos = users.Select(u => new { u.UserId, u.FirebaseUid, u.Email, u.FullName, u.IsAdmin, u.IsLocked, u.CreatedAt, u.Points, u.Level });
            return Ok(userDtos);

        }

        [HttpGet("by-uid/{uid}")]
        public async Task<IActionResult> GetByUid(string uid)
        {
            var user = await _userRepository.GetByFirebaseUidAsync(uid);
            if (user == null) return NotFound("Người dùng không tồn tại.");
            // Cân nhắc DTO
            return Ok(new { user.UserId, user.FirebaseUid, user.Email, user.FullName, user.IsAdmin, user.IsLocked, user.CreatedAt, user.Points, user.Level });

        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);

            if (!currentUserId.HasValue) 
                return Unauthorized();

            bool isCurrentUserAdmin = await this.IsCurrentUserAdminAsync(_userRepository);

            if (currentUserId.Value != id && !isCurrentUserAdmin)
            {
                return Forbid("Bạn không có quyền xem thông tin người dùng này.");
            }

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound("Người dùng không tồn tại.");
            // Cân nhắc DTO
            return Ok(new { user.UserId, user.FirebaseUid, user.Email, user.FullName, user.AvatarUrl, user.School, user.Points, user.Level, user.IsAdmin, user.IsLocked, user.CreatedAt });
        }

        [HttpGet("me")] // Endpoint để user hiện tại lấy thông tin của chính mình
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue)
            {
                return Unauthorized("Không thể xác định người dùng.");
            }
            var user = await _userRepository.GetByIdAsync(currentUserId.Value);
            if (user == null)
                return NotFound("Người dùng không tồn tại.");
            // Cân nhắc DTO
            return Ok(new { user.UserId, user.FirebaseUid, user.Email, user.FullName, user.AvatarUrl, user.School, user.Points, user.Level, user.IsAdmin, user.IsLocked, user.CreatedAt });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserModel model)
        {

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue)
                return Unauthorized();


            bool isCurrentUserAdmin = await this.IsCurrentUserAdminAsync(_userRepository);

            if (currentUserId.Value != id && !isCurrentUserAdmin)
            {
                return Forbid("Bạn không có quyền cập nhật thông tin người dùng này.");
            }

            var userToUpdate = await _userRepository.GetByIdAsync(id);
            if (userToUpdate == null)
                return NotFound("Người dùng không tồn tại.");

            // Cập nhật thông tin trên Firebase Auth (nếu cần, ví dụ DisplayName)
            // Hiện tại model chỉ có FullName, School, AvatarUrl - những thứ này thường lưu ở DB local
            // Nếu muốn cập nhật DisplayName trên Firebase:
            if (!string.IsNullOrEmpty(model.FullName) && model.FullName != userToUpdate.FullName)
            {
                try
                {
                    await FirebaseAuth.DefaultInstance.UpdateUserAsync(new UserRecordArgs
                    {
                        Uid = userToUpdate.FirebaseUid,
                        DisplayName = model.FullName
                    });
                }
                catch (FirebaseAuthException ex)
                {
                    // Log lỗi nhưng có thể vẫn tiếp tục cập nhật DB local
                    Console.WriteLine($"Lỗi cập nhật DisplayName trên Firebase cho UID {userToUpdate.FirebaseUid}: {ex.Message}");
                }
            }

            userToUpdate.FullName = model.FullName ?? userToUpdate.FullName;
            userToUpdate.School = model.School ?? userToUpdate.School;
            userToUpdate.AvatarUrl = model.AvatarUrl ?? userToUpdate.AvatarUrl;

            await _userRepository.UpdateAsync(userToUpdate);
            // Cân nhắc DTO
            return Ok(new { userToUpdate.UserId, userToUpdate.FirebaseUid, userToUpdate.Email, userToUpdate.FullName, userToUpdate.AvatarUrl, userToUpdate.School, userToUpdate.Points, userToUpdate.Level, userToUpdate.IsAdmin, userToUpdate.IsLocked });

        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = await this.GetCurrentUserIdAsync(_userRepository);
            if (!currentUserId.HasValue) return Unauthorized();

            bool isCurrentUserAdmin = await this.IsCurrentUserAdminAsync(_userRepository);

            if (currentUserId.Value == id)
            {
                return BadRequest("Bạn không thể tự xóa tài khoản của mình qua API này. Vui lòng liên hệ quản trị viên.");
            }

            if (!isCurrentUserAdmin)
            {
                return Forbid("Bạn không có quyền xóa người dùng này.");
            }

            var userToDelete = await _userRepository.GetByIdAsync(id);
            if (userToDelete == null)
                return NotFound("Người dùng không tồn tại.");

            if (userToDelete.IsAdmin && currentUserId.Value != userToDelete.UserId)
            {
                // Hoặc có một super admin role
                // return Forbid("Không thể xóa tài khoản quản trị viên khác.");
            }


            try
            {
                await FirebaseAuth.DefaultInstance.DeleteUserAsync(userToDelete.FirebaseUid); // Sử dụng UID


                await _userRepository.DeleteAsync(id);
            }
            catch (FirebaseAuthException ex)
            {
                Console.WriteLine($"Lỗi khi xóa người dùng {userToDelete.FirebaseUid} khỏi Firebase: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Lỗi khi xóa người dùng khỏi Firebase. Dữ liệu có thể không nhất quán.");
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"Lỗi DB khi xóa người dùng {id}: {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Lỗi DB khi xóa người dùng. Có thể do còn dữ liệu liên quan.");
            }


            return NoContent();
        }

        [HttpPost("{id}/points")]
        [Authorize(Policy = "Admin")]
        public async Task<IActionResult> AddPoints(int id, [FromBody] PointsModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound("Người dùng không tồn tại.");

            await _userRepository.UpdatePointsAsync(id, model.Points);
            // Lấy lại user sau khi cập nhật để có Points và Level mới nhất
            var updatedUser = await _userRepository.GetByIdAsync(id);
            return Ok(new { Message = "Cập nhật điểm thành công.", Points = updatedUser.Points, Level = updatedUser.Level });
        }

        [HttpGet("ranking")]
        public async Task<IActionResult> GetRanking([FromQuery] int limit = 10)
        {
            var users = await _userRepository.GetTopUsersAsync(limit);
            // DTO cho ranking
            var rankingDtos = users.Select(u => new
            {
                u.UserId,
                u.FullName,
                u.AvatarUrl,
                u.Points,
                u.Level,
                DocumentsUploaded = u.UploadedDocuments?.Count ?? 0
            });
            return Ok(rankingDtos);
        }

    }
}

public class RegisterModel
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email không được để trống")]
    [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
    public string Email { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Mật khẩu không được để trống")]
    [System.ComponentModel.DataAnnotations.MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Họ tên không được để trống")]
    public string FullName { get; set; }
}

public class LoginModel
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email không được để trống")]
    [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
    public string Email { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Mật khẩu không được để trống")]
    public string Password { get; set; }
}

public class UpdateUserModel
{
    public string? FullName { get; set; }
    public string? School { get; set; }
    public string? AvatarUrl { get; set; }
}

public class PointsModel
{
    [System.ComponentModel.DataAnnotations.Required]
    public int Points { get; set; }
}
