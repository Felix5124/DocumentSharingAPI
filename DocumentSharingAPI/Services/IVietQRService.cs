namespace DocumentSharingAPI.Services
{
    public interface IVietQRService
    {
        string GenerateVietQRUrl(string bankCode, string accountNumber, string accountName, decimal amount, string content);
        string GenerateOrderCode();
    }
}
