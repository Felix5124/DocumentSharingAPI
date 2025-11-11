namespace DocumentSharingAPI.Services
{
    public interface IFileValidationService
    {
        Task<bool> ValidateFileSignatureAsync(Stream fileStream, string expectedExtension);
        string GetActualFileType(byte[] header);
    }

    public class FileValidationService : IFileValidationService
    {
        private readonly Dictionary<string, byte[][]> _fileSignatures = new()
        {
            {".pdf", new[] {new byte[] {0x25, 0x50, 0x44, 0x46}}}, // %PDF
            {".docx", new[] {new byte[] {0x50, 0x4B, 0x03, 0x04}}}, // PK (ZIP format)
            {".pptx", new[] {new byte[] {0x50, 0x4B, 0x03, 0x04}}}, // PK (ZIP format)
            {".zip", new[] {new byte[] {0x50, 0x4B, 0x03, 0x04}}},  // PK (ZIP format)
            {".txt", new byte[0][]} // Text files don't have specific signature
        };

        public async Task<bool> ValidateFileSignatureAsync(Stream fileStream, string expectedExtension)
        {
            if (!_fileSignatures.ContainsKey(expectedExtension.ToLowerInvariant()))
                return true; // If we don't have signature for this type, allow it

            var signatures = _fileSignatures[expectedExtension.ToLowerInvariant()];
            if (signatures.Length == 0) return true; // No signature check needed (like .txt)

            // Read file header
            byte[] header = new byte[16];
            var originalPosition = fileStream.Position;
            fileStream.Position = 0;
            await fileStream.ReadAsync(header, 0, 16);
            fileStream.Position = originalPosition; // Reset position

            // Check if any signature matches
            foreach (var signature in signatures)
            {
                if (header.Take(signature.Length).SequenceEqual(signature))
                    return true;
            }

            return false;
        }

        public string GetActualFileType(byte[] header)
        {
            foreach (var kvp in _fileSignatures)
            {
                if (kvp.Value.Length == 0) continue;
                
                foreach (var signature in kvp.Value)
                {
                    if (header.Take(signature.Length).SequenceEqual(signature))
                        return kvp.Key;
                }
            }

            // Check some common types
            if (header.Take(4).SequenceEqual(new byte[] {0x89, 0x50, 0x4E, 0x47}))
                return ".png";
            if (header.Take(3).SequenceEqual(new byte[] {0xFF, 0xD8, 0xFF}))
                return ".jpg";
            if (header.Take(2).SequenceEqual(new byte[] {0x50, 0x4B}))
                return ".zip/.docx/.xlsx";

            return "unknown";
        }
    }
}