using DocumentSharingAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks; // Thêm using này


namespace DocumentSharingAPI.Helpers
{
    public static class ControllerBaseExtensions
    {
        // Lấy UserId (int, PK của bảng User) từ FirebaseUid (string, từ token)
        public static async Task<int?> GetCurrentUserIdAsync(this ControllerBase controller, IUserRepository userRepository)
        {
            var firebaseUid = controller.User.FindFirst("sub")?.Value ?? controller.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(firebaseUid))
            {
                return null;
            }

            var user = await userRepository.GetByFirebaseUidAsync(firebaseUid);
            return user?.UserId;
        }

        // Lấy vai trò của User (ví dụ: kiểm tra IsAdmin)
        public static async Task<bool> IsCurrentUserAdminAsync(this ControllerBase controller, IUserRepository userRepository)
        {
            var firebaseUid = controller.User.FindFirst("sub")?.Value ?? controller.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(firebaseUid))
            {
                return false;
            }
            var user = await userRepository.GetByFirebaseUidAsync(firebaseUid);
            return user?.IsAdmin ?? false;
        }

        // Hoặc nếu bạn đã thêm claim "IsAdmin" vào token
        public static bool IsAdmin(this ClaimsPrincipal user)
        {
            return user.HasClaim("IsAdmin", "true");
        }
    }
}
