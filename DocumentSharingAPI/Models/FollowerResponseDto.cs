namespace DocumentSharingAPI.Models
{
    public class FollowResponseDto
    {
        public int FollowId { get; set; }
        public int UserId { get; set; }
        public int FollowedUserId { get; set; }
        public string FollowedUserFullName { get; set; }
        public string FollowedUserEmail { get; set; }
        public DateTime FollowedAt { get; set; }
    }

    public class FollowerResponseDto
    {
        public int FollowId { get; set; } // Thêm để hỗ trợ unfollow
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
    }
}