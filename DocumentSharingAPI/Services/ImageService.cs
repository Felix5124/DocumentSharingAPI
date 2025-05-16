using ImageMagick;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace DocumentSharingAPI.Services
{
    public class ImageService : IImageService
    {
        //biến chứa đường dẫn đến thư mục gốc
        private readonly string _baseFileDirectory;
        private readonly IWebHostEnvironment _env; // Để lấy ContentRootPath


        // Inject IWebHostEnvironment để lấy đường dẫn
        public ImageService(IWebHostEnvironment env) 
        {
            _env = env;

            _baseFileDirectory = Path.Combine(env.ContentRootPath, "Files");
            if (!Directory.Exists(_baseFileDirectory))
            {
                Directory.CreateDirectory(_baseFileDirectory);
            }
        }

        public async Task<string> SaveFileAsync(IFormFile file, string subfolder, string currentFilePath = null)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(currentFilePath))
            {
                await DeleteFileAsync(currentFilePath); // currentFilePath là đường dẫn tương đối từ DB
            }

            var targetFolder = Path.Combine(_baseFileDirectory, subfolder);
            Directory.CreateDirectory(targetFolder);

            // Tạo tên file duy nhất để tránh trùng lặp và các vấn đề về ký tự đặc biệt
            var extension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var physicalPath = Path.Combine(targetFolder, uniqueFileName);

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"Files/{subfolder}/{uniqueFileName}".Replace(Path.DirectorySeparatorChar, '/');
        }

        //public Task filePath(string filePath) // imageUrl là đường dẫn tương đối từ DB, ví dụ: "Files/Covers/abc.jpg"
        //{
        //    if (string.IsNullOrEmpty(filePath))
        //    {
        //        return Task.CompletedTask;
        //    }


        //    // Chuyển đổi filePath (ví dụ "Files/Covers/abc.jpg") thành đường dẫn vật lý đầy đủ
        //    var physicalPath = Path.Combine(_env.ContentRootPath, filePath.TrimStart('/'));


        //    if (File.Exists(physicalPath))
        //    {
        //        try
        //        {
        //            File.Delete(physicalPath);
        //        }
        //        catch (IOException ex)
        //        {
        //            // Log lỗi, ví dụ: file đang được sử dụng
        //            Console.WriteLine($"Error deleting file {physicalPath}: {ex.Message}");
        //        }
        //    }
        //    return Task.CompletedTask;
        //}

        public Task DeleteFileAsync(string filePath) // filePath là đường dẫn tương đối từ DB (ví dụ: "Files/Covers/abc.jpg")
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return Task.CompletedTask;
            }

            // Chuyển đổi filePath (ví dụ "Files/Covers/abc.jpg") thành đường dẫn vật lý đầy đủ

            var physicalPath = Path.Combine(_env.ContentRootPath, filePath.TrimStart('/'));


            if (File.Exists(physicalPath))
            {
                try
                {
                    File.Delete(physicalPath);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Error deleting file {physicalPath}: {ex.Message}");
                }
            }
            return Task.CompletedTask;
        }

        //phương thức trích xuất ảnh từ IFormFile(cho upload)
        public async Task<string> ExtractFirstPageAsImageAsync(IFormFile pdfFile, string outputSubfolder, string documentTitle)
        {
            if (pdfFile == null || pdfFile.Length == 0 || !pdfFile.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Lưu file PDF tạm thời để Magick.NET xử lý
            var tempPdfDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPdfDirectory);
            var tempPdfPath = Path.Combine(tempPdfDirectory, pdfFile.FileName);
            try
            {
                using (var stream = new FileStream(tempPdfPath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }
                return await ProcessPdfToImage(tempPdfPath, outputSubfolder, documentTitle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting image from PDF '{tempPdfPath}': {ex.Message}");
                return null; // Hiện tại chỉ in lỗi, có thể cân nhắc trả về ảnh mặc định
            }
            finally
            {
                if (File.Exists(tempPdfPath))
                {
                    File.Delete(tempPdfPath);
                }
                if (Directory.Exists(tempPdfDirectory))
                {
                    Directory.Delete(tempPdfDirectory, true);
                }
            }
        }

        // Phương thức này nhận pdfPhysicalPath là đường dẫn vật lý đầy đủ của file PDF đã được lưu trên server
        public Task<string> ExtractFirstPageAsImageAsync(string pdfRelativePath, string outputSubfolder, string documentTitle)
        {

            if (string.IsNullOrEmpty(pdfRelativePath))
            {
                return Task.FromResult<string>(null);
            }

            // Chuyển đổi đường dẫn tương đối (lưu trong DB, ví dụ: "Files/Documents/abc.pdf") thành đường dẫn vật lý
            var pdfPhysicalPath = Path.Combine(_env.ContentRootPath, pdfRelativePath.TrimStart('/'));

            if (!File.Exists(pdfPhysicalPath) || !pdfPhysicalPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"PDF file for extraction not found or not a PDF: {pdfPhysicalPath}");
                return Task.FromResult<string>(null);
            }
            return ProcessPdfToImage(pdfPhysicalPath, outputSubfolder, documentTitle);

        }


        private async Task<string> ProcessPdfToImage(string pdfPhysicalPath, string outputSubfolder, string documentTitle)
        {
            var outputFolder = Path.Combine(_baseFileDirectory, outputSubfolder); // Ví dụ: Files/Covers/PdfPreviews
            Directory.CreateDirectory(outputFolder);

            var safeTitle = string.Join("_", (documentTitle ?? "document").Split(Path.GetInvalidFileNameChars()));
            var outputImageFileName = $"{Path.GetFileNameWithoutExtension(safeTitle)}_{Guid.NewGuid().ToString().Substring(0, 8)}_preview.png";
            var outputImagePhysicalPath = Path.Combine(outputFolder, outputImageFileName);

            try
            {
                using (var images = new MagickImageCollection())
                {
                    var settings = new MagickReadSettings { FrameIndex = 0, FrameCount = 1, Density = new Density(150) };
                    images.Read(pdfPhysicalPath, settings);

                    if (images.Any())
                    {
                        images[0].Format = MagickFormat.Png;
                        images[0].Quality = 75;
                        await images[0].WriteAsync(outputImagePhysicalPath);
                        // Trả về đường dẫn tương đối: "Files/outputSubfolder/outputImageFileName.png"
                        return $"Files/{outputSubfolder}/{outputImageFileName}".Replace(Path.DirectorySeparatorChar, '/');
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting image from PDF '{pdfPhysicalPath}': {ex.Message}");
                return null;
            }
            return null;
        }


    }
}
