namespace DocumentSharingAPI.Services
{
    public interface IImageService
    {
        Task<string> SaveFileAsync(IFormFile imageFile, string subfolder, string currentFilePath = null);
        Task DeleteFileAsync(string imageUrl);
        Task<string> ExtractFirstPageAsImageAsync(IFormFile pdfFile, string outputSubfolder, string documentTitle);
        //Lấy đường dẫn file pdf vật lý
        Task<string> ExtractFirstPageAsImageAsync(string pdfFilePath, string outputSubfolder, string documentTitle);
    }
}
