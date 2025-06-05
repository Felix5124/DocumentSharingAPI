using GenerativeAI;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Linq;
using System;
using DocumentSharingAPI.Repositories;
using DocumentSharingAPI.Models;
using System.Collections.Generic;
// using System.Text.RegularExpressions; // Cân nhắc nếu muốn dùng regex

namespace DocumentSharingAPI.Services
{
    public class GeminiChatService : IGeminiChatService
    {
        private readonly string _apiKey;
        private readonly IUserRepository _userRepository;
        private readonly IUserDocumentRepository _userDocumentRepository;
        private readonly GenerativeModel _geminiProModel;

        public GeminiChatService(IConfiguration configuration, IUserRepository userRepository, IUserDocumentRepository userDocumentRepository, AppDbContext context)
        {
            _apiKey = configuration["GeminiApiKey"];
            _userRepository = userRepository;
            _userDocumentRepository = userDocumentRepository;
            _geminiProModel = new GenerativeModel(apiKey: _apiKey, model: "gemini-1.5-flash");
        }

        public async Task<string> GetChatbotResponseAsync(string userMessage, int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return "I couldn't find your user information.";
            }

            string userInfoContext = $"The user is '{user.FullName}' (ID: {userId}, Email: {user.Email}). Points: {user.Points}.";

            var uploads = await _userDocumentRepository.GetByUserIdAndActionAsync(userId, "Upload");
            userInfoContext += $" Documents uploaded: {uploads.Count()}.";

            var downloads = await _userDocumentRepository.GetByUserIdAndActionAsync(userId, "Download");
            userInfoContext += $" Documents downloaded: {downloads.Count()}.";

            string systemInstruction = @"You are an expert assistant for 'DHK', a document sharing website.
Your primary goal is to:
1. Guide users on how to use the website.
2. Provide information about their account (points, number of uploads, number of downloads) based on the 'User Context' provided. WHEN A USER ASKS FOR A SPECIFIC PIECE OF ACCOUNT INFORMATION (E.G., ONLY POINTS, OR ONLY UPLOAD COUNT, OR ONLY DOWNLOAD COUNT), YOU MUST PROVIDE *ONLY* THE INFORMATION THEY ASKED FOR. Do NOT list all other account details from the 'User Context' unless the user explicitly asks for a full summary (e.g., 'thông tin tài khoản của tôi').
3. Be friendly, concise, and answer in Vietnamese.

Interpret user queries related to points, uploads, or downloads flexibly.
- If the user asks about points (e.g., 'điểm số của tôi', 'tôi có mấy điểm rồi', 'xem điểm', 'điểm của tôi là bao nhiêu'), understand they want to know their points. Respond ONLY with their points, and make it clear. For example: 'Chào bạn [User's FullName], số điểm hiện tại của bạn là [User's Points from User Context] điểm.' Do NOT mention upload or download counts in this specific response.
- If the user asks about uploads (e.g., 'tài liệu tôi up', 'đã đăng bao nhiêu file', 'số lượng upload của tôi'), understand they want to know their upload count. Respond ONLY with their upload count. For example: 'Chào bạn [User's FullName], bạn đã tải lên [User's Upload Count from User Context] tài liệu.' Do NOT mention points or download counts in this specific response.
- If the user asks about downloads (e.g., 'file đã lấy về', 'tải bao nhiêu lần', 'số lượng download của tôi'), understand they want to know their download count. Respond ONLY with their download count. For example: 'Chào bạn [User's FullName], bạn đã tải về [User's Download Count from User Context] tài liệu.' Do NOT mention points or upload counts in this specific response.
- If the user asks for a general account summary (e.g., 'thông tin tài khoản của tôi', 'tóm tắt tài khoản'), then you should provide a summary including their points, upload count, and download count.

If asked how to upload or download, provide clear, step-by-step instructions.
- **To upload a document:**
  1. Navigate to the '**Tải lên tài liệu** ' page from the user menu.
  2. Fill in all required information on the form: **Title**, **Description**, **Category** ,**Required Points**, **Tags (optional)**.
  3. **Select the document file** (e.g., PDF, DOCX) and a **cover image** (optional but recommended).
  4. Click '**Tải lên**' and wait for the confirmation. Your document will be pending approval by an admin.

- **To download a document, please follow these steps:**
  1. **Tìm tài liệu:** Đầu tiên, bạn cần tìm tài liệu muốn tải. Hãy sử dụng thanh tìm kiếm ở đầu trang (bạn có thể nhập từ khóa, tiêu đề, hoặc thẻ).
  2. **Xem chi tiết:** Khi tìm thấy tài liệu, nhấp vào tiêu đề của tài liệu đó để xem thông tin chi tiết.
  3. **Kiểm tra yêu cầu và điểm của bạn:**
     - Trên trang chi tiết tài liệu, bạn sẽ thấy nút 'Tải xuống'.
     - **Lưu ý quan trọng:** Một số tài liệu có thể yêu cầu một số điểm nhất định để tải.
     - (Thông tin cho bạn: Hiện tại bạn đang có [User's Points from User Context] điểm. Bạn luôn có thể kiểm tra lại trong hồ sơ cá nhân hoặc hỏi tôi.)
  4. **Thực hiện tải xuống:** Nếu tài liệu không yêu cầu điểm, hoặc nếu bạn có đủ điểm cho tài liệu đó, hãy nhấp vào nút 'Tải xuống' để bắt đầu quá trình.
  5. **Nếu cần thêm điểm:** Trong trường hợp tài liệu yêu cầu nhiều điểm hơn số bạn đang có, bạn có thể tích lũy thêm điểm bằng cách đóng góp tài liệu hữu ích cho cộng đồng hoặc tham gia các hoạt động khác trên DHK.

- **To search for documents:**
  Bạn chỉ cần nhập tên tài liệu vào ô tìm kiếm.
";

            string lowerUserMessage = userMessage.ToLower();
            bool intentProcessed = false;

            // Ưu tiên các câu lệnh rõ ràng, nhưng AI vẫn có thể suy luận từ systemInstruction
            if (IsAskingForPoints(lowerUserMessage))
            {
                // Context đã có điểm, AI sẽ tự sử dụng
                intentProcessed = true; // Đánh dấu là đã có xử lý ý định cơ bản
            }
            else if (IsAskingForUploads(lowerUserMessage))
            {
                // Context đã có số lượng upload
                intentProcessed = true;
            }
            else if (IsAskingForDownloads(lowerUserMessage))
            {
                // Context đã có số lượng download
                intentProcessed = true;
            }
            // Các câu hỏi về hướng dẫn sẽ được AI xử lý dựa vào systemInstruction

            string fullPrompt = $"{systemInstruction}\n\nUser Context: {userInfoContext}\n\nUser Query: {userMessage}";

            try
            {
                var response = await _geminiProModel.GenerateContentAsync(fullPrompt);
                return response.Text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Generative AI API: {ex.Message} for prompt: {fullPrompt}");
                return "Xin lỗi, tôi đang gặp sự cố khi kết nối đến trợ lý AI. Vui lòng thử lại sau.";
            }
        }

        // Helper methods để kiểm tra ý định cơ bản (có thể mở rộng bằng regex hoặc logic phức tạp hơn)
        private bool IsAskingForPoints(string lowerUserMessage)
        {
            string[] keywords = { "điểm", "point" };
            return keywords.Any(kw => lowerUserMessage.Contains(kw));
        }

        private bool IsAskingForUploads(string lowerUserMessage)
        {
            string[] keywords = { "upload", "tải lên", "đăng tài liệu", "up tài liệu" };
            return keywords.Any(kw => lowerUserMessage.Contains(kw));
        }

        private bool IsAskingForDownloads(string lowerUserMessage)
        {
            string[] keywords = { "download", "tải về", "lấy tài liệu" };
            return keywords.Any(kw => lowerUserMessage.Contains(kw));
        }
    }
}