using GenerativeAI;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Linq;
using System;
using DocumentSharingAPI.Repositories;
using DocumentSharingAPI.Models;
using DocumentSharingAPI.Models.DTO;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Services
{
    public class GeminiChatService : IGeminiChatService
    {
        private readonly string _apiKey;
        private readonly IUserRepository _userRepository;
        private readonly IUserDocumentRepository _userDocumentRepository;
        private readonly AppDbContext _context;
        private readonly GenerativeModel _geminiProModel;

        public GeminiChatService(IConfiguration configuration, IUserRepository userRepository, IUserDocumentRepository userDocumentRepository, AppDbContext context)
        {
            _apiKey = configuration["GeminiApiKey"];
            _userRepository = userRepository;
            _userDocumentRepository = userDocumentRepository;
            _context = context;
            _geminiProModel = new GenerativeModel(apiKey: _apiKey, model: "gemini-2.5-flash-lite");
        }

        public async Task<string> GetChatbotResponseAsync(string userMessage, int userId, List<ChatMessageDto>? history)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return "Không tìm thấy thông tin người dùng. Vui lòng đăng nhập lại.";
            }

            // === BẮT ĐẦU ĐOẠN SỬA LỖI ===
            // Kiểm tra xem đã qua ngày mới chưa. Nếu qua rồi thì coi như chưa dùng lượt nào (Reset tạm thời để tính toán)
            if (user.LastDownloadResetDate.Date < DateTime.Now.Date)
            {
                user.RegularDownloadsUsedToday = 0;
                user.VipDownloadsUsedToday = 0;
                // Chúng ta chỉ cập nhật biến user trong bộ nhớ để Chatbot trả lời đúng.
                // Việc cập nhật vào Database sẽ do hàm Download thực hiện khi người dùng thực sự tải file.
            }
            // === KẾT THÚC ĐOẠN SỬA LỖI ===

            // Lấy thông tin upload/download
            var uploads = await _userDocumentRepository.GetByUserIdAndActionAsync(userId, "Upload");
            // var downloads = await _userDocumentRepository.GetByUserIdAndActionAsync(userId, "Download"); // Dòng này chưa dùng, có thể comment lại
            
            // Lấy danh sách Top 5 tài liệu của user
            var topDocs = await _context.Documents
                .Where(d => d.UploadedBy == userId)
                .OrderByDescending(d => d.DownloadCount)
                .Take(5)
                .Select(d => $"{d.Title} ({d.DownloadCount} lượt tải)")
                .ToListAsync();

            string topDocsString = topDocs.Any() ? string.Join("\n", topDocs) : "Chưa có tài liệu nào";

            // Xây dựng ngữ cảnh động về User với tính toán số liệu chính xác
            StringBuilder userContextBuilder = new StringBuilder();
            userContextBuilder.AppendLine($" - Tên: {user.FullName}");
            userContextBuilder.AppendLine($" - Email: {user.Email}");
            
            // Xác định trạng thái VIP thực tế
            bool isVipValid = user.IsVip && user.VipExpiryDate.HasValue && user.VipExpiryDate.Value > DateTime.Now;
            
            // Thiết lập giới hạn dựa trên loại tài khoản
            int dailyRegularLimit = isVipValid ? 10 : 2;
            int dailyVipLimit = isVipValid ? 10 : 0;
            
            // Tính toán số lượt còn lại (không âm)
            int remainingRegularToday = Math.Max(dailyRegularLimit - user.RegularDownloadsUsedToday, 0);
            int remainingVipToday = Math.Max(dailyVipLimit - user.VipDownloadsUsedToday, 0);
            
            // Hiển thị loại tài khoản
            userContextBuilder.AppendLine($" - Loại tài khoản: {(isVipValid ? "VIP" : "Tài khoản Thường")}");
            if (isVipValid && user.VipExpiryDate.HasValue)
            {
                userContextBuilder.AppendLine($" - Hết hạn VIP: {user.VipExpiryDate.Value:dd/MM/yyyy}");
            }
            
            // Hiển thị số lượt tải còn lại rõ ràng
            userContextBuilder.AppendLine($" - TỔNG Lượt tải THƯỜNG còn lại: {remainingRegularToday} (Hàng ngày) + {user.RegularBonusDownloads} (Kho dư)");
            userContextBuilder.AppendLine($" - TỔNG Lượt tải VIP còn lại: {remainingVipToday} (Hàng ngày) + {user.VipBonusDownloads} (Kho dư)");
            
            // Thêm chú thích quan trọng cho user thường
            if (!isVipValid)
            {
                userContextBuilder.AppendLine($" - LƯU Ý QUAN TRỌNG: Người dùng này KHÔNG PHẢI VIP. Họ không có lượt tải VIP hàng ngày, chỉ có thể tải VIP nếu có điểm Bonus VIP.");
            }
            
            userContextBuilder.AppendLine($" - Tổng tài liệu đã upload: {uploads.Count()}");
            userContextBuilder.AppendLine($" - Top 5 tài liệu hot nhất của họ:");
            userContextBuilder.AppendLine($"{topDocsString}");

            // --- CẬP NHẬT SYSTEM INSTRUCTION VỚI DỮ LIỆU FAQ ---
            string systemInstruction = $@"
Vai trò: Bạn là DocShare AI Assistant, trợ lý ảo chuyên nghiệp của hệ thống chia sẻ tài liệu DocShare.
Phong cách: Thân thiện, ngắn gọn, hỗ trợ nhiệt tình, trả lời bằng tiếng Việt.
Quy tắc: CHỈ trả lời các câu hỏi liên quan đến hệ thống DocShare (Tài khoản, Tài liệu, VIP, Lỗi kỹ thuật). Từ chối khéo léo các câu hỏi không liên quan.

Dữ liệu người dùng hiện tại:
{userContextBuilder.ToString()}

Cơ sở dữ liệu kiến thức (FAQ):

1. Tài khoản & Đăng nhập:
- Đăng ký: Vào trang Đăng ký > Nhập thông tin > Xác thực qua email.
- Login Google: Hỗ trợ đăng nhập nhanh bằng nút 'Google'.
- Quên mật khẩu: Dùng chức năng 'Quên mật khẩu' tại trang Login để nhận mail reset.
- Đổi Avatar: Vào Hồ sơ (Profile) > Nhấn icon Camera ở ảnh đại diện.
- Khóa tài khoản: Có thể do vi phạm chính sách hoặc admin khóa. Cần liên hệ Admin.
- Đổi tên: Vào Hồ sơ > Nhập tên mới > Lưu.

2. Upload tài liệu:
- Cách tải: Menu Upload > Điền form (Tiêu đề, Mô tả, Danh mục) > Chọn file > Submit.
- Định dạng: PDF, DOCX, TXT, PPTX, ZIP. Max 50MB.
- Trạng thái: Mới tải lên là 'Chưa kiểm duyệt' hoặc 'Đang chờ'. Chỉ hiện công khai khi 'Đã duyệt'.
- Chỉnh sửa: Vào Profile > Danh sách tài liệu > Icon sửa.
- Quyền lợi: Upload được duyệt sẽ nhận thêm lượt tải (Bonus Download). Đủ 5 file nhận badge 'Uploader'.

3. Tải xuống (Download):
- Cách tải: Trang chi tiết > Nút Download.
- Lỗi tải: Chưa đăng nhập, file bị khóa/chờ duyệt, hoặc hết lượt tải.
- Giới hạn Free: 2 lượt/ngày. Reset lúc 00:00.
- Tăng lượt: Upload tài liệu để nhận Bonus hoặc mua VIP.
- Tài liệu VIP Only: Chỉ dành cho tài khoản VIP hoặc dùng điểm VIP Bonus.
- QUY TẮC QUAN TRỌNG VỀ LƯỢT TẢI:
  * Khi người dùng hỏi 'còn bao nhiêu lượt tải', hãy ưu tiên trả lời số lượng lượt tải Thường trước.
  * Nếu họ không phải VIP và không có Bonus VIP, hãy nhắc họ nâng cấp VIP để tải tài liệu cao cấp.
  * Đừng cộng gộp lượt tải Thường và VIP làm một trừ khi giải thích rõ.
  * Nếu người dùng là tài khoản Thường, TUYỆT ĐỐI KHÔNG cộng gộp lượt tải VIP vào tổng số lượt tải, trừ khi họ có điểm Bonus VIP. Hãy nói rõ họ chỉ được tải tài liệu thường.

4. Gói VIP & Thanh toán:
- Quyền lợi thực tế: Tổng 20 file/ngày (Chia làm: 10 file Thường + 10 file VIP), Xem trước 10 trang PDF, Không quảng cáo, Duyệt bài ưu tiên.
- Các gói: Tháng (49k), 3 Tháng (129k), Năm (399k).
- Cách mua: Menu 'Nâng cấp tài khoản' > Chọn gói > Thanh toán (Test Mode).
- Kiểm tra hạn: Xem trong Profile hoặc hỏi trực tiếp tôi.

5. Tương tác & Cộng đồng:
- Bình luận: Cuối trang chi tiết (chỉ khi tài liệu đã duyệt).
- Báo cáo: Nút cờ 'Báo cáo vi phạm' nếu thấy nội dung xấu.
- Theo dõi: Nút 'Theo dõi' ở trang chi tiết hoặc profile người khác.
- Lịch sử tải: Vào Profile > Tài liệu đã tải xuống.
- Badge: Biểu tượng cạnh tên (ví dụ: Top Uploader, Top Commenter).

6. Sự cố thường gặp:
- Lỗi Preview: Do file lỗi hoặc vượt quá giới hạn trang xem thử (Free: 2 trang, VIP: 10 trang) -> Hãy tải về để xem full.
- Tài liệu 'Tạm ngưng' (Suspended): Do bị báo cáo nhiều lần -> Chờ Admin xử lý.
- Lỗi Upload: Kiểm tra lại định dạng và dung lượng file (<50MB).

Hãy trả lời câu hỏi sau của người dùng dựa trên thông tin trên: '{userMessage}'
";
            
            string lowerUserMessage = userMessage.ToLower();

            // Ưu tiên các câu lệnh rõ ràng, nhưng AI vẫn có thể suy luận từ systemInstruction
            if (IsAskingForStats(lowerUserMessage))
            {
                 // Để AI tự trả lời dựa trên Context đã nạp ở trên
            }

            try
            {
                // Xây dựng lịch sử hội thoại để đưa vào prompt
                string conversationHistory = "";
                if (history != null && history.Any())
                {
                    // Chỉ lấy 5-10 tin gần nhất để tiết kiệm token
                    var recentHistory = history.TakeLast(10).ToList();
                    
                    var historyBuilder = new StringBuilder();
                    historyBuilder.AppendLine("\n\nLịch sử hội thoại gần đây:");
                    foreach (var msg in recentHistory)
                    {
                        // Chỉ lấy tin nhắn có nội dung không rỗng
                        if (!string.IsNullOrWhiteSpace(msg.Text))
                        {
                            var rolePrefix = msg.Role == "user" ? "User" : "Assistant";
                            historyBuilder.AppendLine($"{rolePrefix}: {msg.Text}");
                        }
                    }
                    conversationHistory = historyBuilder.ToString();
                }

                // Kết hợp system instruction với lịch sử hội thoại và câu hỏi hiện tại
                string finalPrompt = $"{systemInstruction}{conversationHistory}\n\nUser Question: {userMessage}";

                var response = await _geminiProModel.GenerateContentAsync(finalPrompt);
                return response.Text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Generative AI API: {ex.Message}");
                return "Hiện tại hệ thống AI đang bận. Vui lòng thử lại sau giây lát.";
            }
        }

        // Helper methods
        private bool IsAskingForStats(string msg)
        {
            string[] keywords = { "điểm", "point", "upload", "tải lên", "download", "tải về", "bao nhiêu", "lượt tải", "số liệu", "hết hạn", "vip" };
            return keywords.Any(kw => msg.Contains(kw));
        }
    }
}