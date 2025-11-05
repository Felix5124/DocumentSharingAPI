using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace DocumentSharingAPI.Services
{
    public interface ICorsConfigurationService
    {
        Task ConfigureCorsAsync();
    }

    public class CorsConfigurationService : ICorsConfigurationService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<CorsConfigurationService> _logger;

        public CorsConfigurationService(IConfiguration configuration, ILogger<CorsConfigurationService> logger)
        {
            _blobServiceClient = new BlobServiceClient(configuration["Storage:ConnectionString"]);
            _logger = logger;
        }

        public async Task ConfigureCorsAsync()
        {
            try
            {
                var corsRules = new List<BlobCorsRule>
                {
                    new BlobCorsRule
                    {
                        AllowedOrigins = "*", // Trong production nên specify domain cụ thể
                        AllowedMethods = "GET,HEAD,OPTIONS",
                        AllowedHeaders = "*",
                        ExposedHeaders = "*",
                        MaxAgeInSeconds = 3600
                    }
                };

                var properties = await _blobServiceClient.GetPropertiesAsync();
                properties.Value.Cors.Clear();
                
                foreach (var rule in corsRules)
                {
                    properties.Value.Cors.Add(rule);
                }

                await _blobServiceClient.SetPropertiesAsync(properties.Value);
                _logger.LogInformation("CORS configuration applied to Azure Blob Storage successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure CORS for Azure Blob Storage: {Message}", ex.Message);
                throw;
            }
        }
    }
}