using Microsoft.AspNetCore.Mvc;
using DocumentSharingAPI.Repositories;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DebugController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IBlobService _blob;

        public DebugController(IDocumentRepository documentRepository, IBlobService blob)
        {
            _documentRepository = documentRepository;
            _blob = blob;
        }

        [HttpGet("document/{id}/file-info")]
        public async Task<IActionResult> GetDocumentFileInfo(int id)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                    return NotFound("Document not found");

                // Check original FileUrl
                var originalFileUrl = document.FileUrl;
                
                // Check processed blob path
                var blobPath = document.FileUrl.StartsWith("documents/") 
                    ? document.FileUrl.Substring("documents/".Length) 
                    : document.FileUrl;

                // Generate SAS URLs
                string downloadSasUrl = null;
                string previewSasUrl = null;
                
                try
                {
                    downloadSasUrl = _blob.GetReadSasUrl("documents", blobPath, TimeSpan.FromMinutes(10));
                }
                catch (Exception ex)
                {
                    downloadSasUrl = $"ERROR: {ex.Message}";
                }

                try
                {
                    previewSasUrl = _blob.GetReadSasUrl("documents", blobPath, TimeSpan.FromMinutes(5));
                }
                catch (Exception ex)
                {
                    previewSasUrl = $"ERROR: {ex.Message}";
                }

                return Ok(new
                {
                    documentId = id,
                    title = document.Title,
                    fileType = document.FileType,
                    originalFileUrl = originalFileUrl,
                    processedBlobPath = blobPath,
                    containerName = "documents",
                    downloadSasUrl = downloadSasUrl,
                    previewSasUrl = previewSasUrl,
                    approvalStatus = document.ApprovalStatus,
                    isLocked = document.IsLock
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("test-blob-path/{*blobPath}")]
        public IActionResult TestBlobPath(string blobPath)
        {
            try
            {
                var sasUrl = _blob.GetReadSasUrl("documents", blobPath, TimeSpan.FromMinutes(5));
                return Ok(new
                {
                    inputPath = blobPath,
                    sasUrl = sasUrl,
                    message = "SAS URL generated successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    inputPath = blobPath,
                    error = ex.Message,
                    message = "Failed to generate SAS URL"
                });
            }
        }

        [HttpGet("document/{id}/file-analysis")]
        public async Task<IActionResult> AnalyzeDocumentFile(int id)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                    return NotFound("Document not found");

                // Get blob path
                var blobPath = document.FileUrl.StartsWith("documents/") 
                    ? document.FileUrl.Substring("documents/".Length) 
                    : document.FileUrl;

                // Download file để analyze
                var stream = await _blob.DownloadAsync("documents", blobPath);
                
                // Read first 100 bytes để check file signature
                byte[] header = new byte[100];
                await stream.ReadAsync(header, 0, 100);
                
                // Convert to hex để dễ đọc
                var headerHex = Convert.ToHexString(header);
                
                // Check DOCX signature (PK at start - ZIP format)
                bool isZipFormat = header[0] == 0x50 && header[1] == 0x4B; // "PK"
                
                // Get file size
                stream.Position = 0;
                var fileSize = stream.Length;
                
                // Create analysis result
                var analysis = new
                {
                    documentId = id,
                    title = document.Title,
                    fileType = document.FileType,
                    expectedExtension = Path.GetExtension(document.FileUrl.Split('/').Last()),
                    fileSize = fileSize,
                    fileSizeInDb = document.FileSize,
                    isZipFormat = isZipFormat,
                    headerBytes = headerHex.Substring(0, Math.Min(40, headerHex.Length)), // First 20 bytes in hex
                    expectedDocxSignature = "504B0304", // DOCX should start with PK (ZIP)
                    analysis = isZipFormat ? 
                        "File appears to be ZIP format (good for DOCX)" : 
                        "File does NOT appear to be ZIP format (bad for DOCX)",
                    recommendation = !isZipFormat ? 
                        "File may be corrupted or not a real DOCX file" : 
                        "File format looks correct, issue may be elsewhere"
                };

                return Ok(analysis);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}