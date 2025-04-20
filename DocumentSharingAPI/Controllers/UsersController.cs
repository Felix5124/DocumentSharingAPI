using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authorization;
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
                return Unauthorized("Invalid email or password.");

            return Ok(new { UserId = user.UserId, Email = user.Email, FullName = user.FullName });
        }

        [HttpGet("all")]
        //[Authorize(Roles = "Admin")]
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
        //[Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();
            return Ok(user);
        }

        [HttpPut("{id}")]
        //[Authorize]
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
        //[Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            await _userRepository.DeleteAsync(id);
            await FirebaseAuth.DefaultInstance.DeleteUserAsync(user.Email);
            return NoContent();
        }

        [HttpPost("{id}/points")]
        //[Authorize]
        public async Task<IActionResult> AddPoints(int id, [FromBody] PointsModel model)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            await _userRepository.UpdatePointsAsync(id, model.Points);
            return Ok(new { Message = "Points updated", Points = user.Points, Level = user.Level });
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
}