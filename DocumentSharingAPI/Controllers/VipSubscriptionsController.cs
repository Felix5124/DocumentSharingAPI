using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VipSubscriptionsController : ControllerBase
    {
        private readonly IVipSubscriptionRepository _vipSubscriptionRepository;
        private readonly IUserRepository _userRepository;

        public VipSubscriptionsController(IVipSubscriptionRepository vipSubscriptionRepository, IUserRepository userRepository)
        {
            _vipSubscriptionRepository = vipSubscriptionRepository;
            _userRepository = userRepository;
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] VipSubscriptionModel model)
        {
            var user = await _userRepository.GetByIdAsync(model.UserId);
            if (user == null)
                return NotFound("User not found");

            // Tính toán ngày hết hạn
            DateTime endDate = model.SubscriptionType.ToLower() == "monthly" 
                ? DateTime.Now.AddMonths(1) 
                : DateTime.Now.AddYears(1);

            var subscription = new VipSubscription
            {
                UserId = model.UserId,
                StartDate = DateTime.Now,
                EndDate = endDate,
                SubscriptionType = model.SubscriptionType,
                Price = model.Price,
                PaymentMethod = model.PaymentMethod,
                TransactionId = model.TransactionId,
                IsActive = true
            };

            await _vipSubscriptionRepository.AddAsync(subscription);

            // Cập nhật trạng thái VIP của user
            user.IsVip = true;
            user.VipExpiryDate = endDate;
            await _userRepository.UpdateAsync(user);

            return Ok(subscription);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserSubscriptions(int userId)
        {
            var subscriptions = await _vipSubscriptionRepository.GetSubscriptionsByUserIdAsync(userId);
            return Ok(subscriptions);
        }

        [HttpGet("user/{userId}/active")]
        public async Task<IActionResult> GetActiveSubscription(int userId)
        {
            var subscription = await _vipSubscriptionRepository.GetActiveSubscriptionByUserIdAsync(userId);
            if (subscription == null)
                return NotFound("No active VIP subscription found");
            
            return Ok(subscription);
        }

        [HttpPost("check-expiry")]
        public async Task<IActionResult> CheckAndUpdateExpiredSubscriptions()
        {
            await _vipSubscriptionRepository.DeactivateExpiredSubscriptionsAsync();

            // Cập nhật trạng thái VIP của users
            var expiredUsers = await _userRepository.GetAllAsync();
            foreach (var user in expiredUsers.Where(u => u.IsVip && u.VipExpiryDate <= DateTime.Now))
            {
                user.IsVip = false;
                await _userRepository.UpdateAsync(user);
            }

            return Ok("Expired subscriptions updated");
        }
    }

    public class VipSubscriptionModel
    {
        public int UserId { get; set; }
        public string SubscriptionType { get; set; } // "Monthly" or "Yearly"
        public decimal Price { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }
    }
}