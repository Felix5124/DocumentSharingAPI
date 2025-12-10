using System.Text;
using System.Web;

namespace DocumentSharingAPI.Services
{
    public class VietQRService : IVietQRService
    {
        /// <summary>
        /// Tạo URL VietQR sử dụng API miễn phí từ vietqr.io
        /// </summary>
        public string GenerateVietQRUrl(string bankCode, string accountNumber, string accountName, decimal amount, string content)
        {
            // Sử dụng API VietQR.io (miễn phí, không cần đăng ký)
            // https://api.vietqr.io/v2/generate
            
            var baseUrl = "https://img.vietqr.io/image";
            
            // Chuẩn hóa tên (bỏ dấu, viết hoa)
            var normalizedName = RemoveDiacritics(accountName).ToUpper();
            
            // Encode content
            var encodedContent = Uri.EscapeDataString(content);
            
            // Format: https://img.vietqr.io/image/{BANK_ID}-{ACCOUNT_NO}-{TEMPLATE}.jpg?amount={AMOUNT}&addInfo={CONTENT}&accountName={ACCOUNT_NAME}
            var qrUrl = $"{baseUrl}/{bankCode}-{accountNumber}-compact.jpg?amount={amount}&addInfo={encodedContent}&accountName={normalizedName}";
            
            return qrUrl;
        }

        /// <summary>
        /// Tạo mã đơn hàng unique
        /// Format: PRE + YYYYMMDD + 6 số random
        /// VD: PRE20250115123456
        /// </summary>
        public string GenerateOrderCode()
        {
            var datePrefix = DateTime.Now.ToString("yyyyMMdd");
            var randomSuffix = new Random().Next(100000, 999999);
            return $"PRE{datePrefix}{randomSuffix}";
        }

        /// <summary>
        /// Bỏ dấu tiếng Việt
        /// </summary>
        private string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
