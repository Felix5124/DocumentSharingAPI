using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace DocumentSharingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CorsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<CorsController> _logger;

        public CorsController(IConfiguration configuration, ILogger<CorsController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("configure-azure-storage")]
        public async Task<IActionResult> ConfigureAzureStorageCors()
        {
            try
            {
                var connectionString = _configuration["Storage:ConnectionString"];
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Storage connection string not configured");
                }

                var blobServiceClient = new BlobServiceClient(connectionString);

                var corsRules = new List<BlobCorsRule>
                {
                    new BlobCorsRule
                    {
                        AllowedOrigins = "*", // Trong production nên thay bằng domain cụ thể
                        AllowedMethods = "GET,HEAD,OPTIONS",
                        AllowedHeaders = "*",
                        ExposedHeaders = "*",
                        MaxAgeInSeconds = 3600
                    }
                };

                var properties = await blobServiceClient.GetPropertiesAsync();
                properties.Value.Cors.Clear();
                
                foreach (var rule in corsRules)
                {
                    properties.Value.Cors.Add(rule);
                }

                await blobServiceClient.SetPropertiesAsync(properties.Value);
                
                _logger.LogInformation("CORS configuration applied to Azure Blob Storage successfully.");
                
                return Ok(new { 
                    message = "CORS configuration applied successfully",
                    rules = corsRules.Select(r => new {
                        allowedOrigins = r.AllowedOrigins,
                        allowedMethods = r.AllowedMethods,
                        allowedHeaders = r.AllowedHeaders,
                        exposedHeaders = r.ExposedHeaders,
                        maxAge = r.MaxAgeInSeconds
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure CORS for Azure Blob Storage: {Message}", ex.Message);
                return StatusCode(500, new { 
                    message = "Failed to configure CORS", 
                    error = ex.Message 
                });
            }
        }

        [HttpGet("check-cors")]
        public async Task<IActionResult> CheckCorsConfiguration()
        {
            try
            {
                var connectionString = _configuration["Storage:ConnectionString"];
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Storage connection string not configured");
                }

                var blobServiceClient = new BlobServiceClient(connectionString);
                var properties = await blobServiceClient.GetPropertiesAsync();
                
                var corsRules = properties.Value.Cors.Select(r => new {
                    allowedOrigins = r.AllowedOrigins,
                    allowedMethods = r.AllowedMethods,
                    allowedHeaders = r.AllowedHeaders,
                    exposedHeaders = r.ExposedHeaders,
                    maxAge = r.MaxAgeInSeconds
                }).ToList();

                return Ok(new { 
                    message = "Current CORS configuration",
                    corsRules = corsRules
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check CORS configuration: {Message}", ex.Message);
                return StatusCode(500, new { 
                    message = "Failed to check CORS configuration", 
                    error = ex.Message 
                });
            }
        }
    }
}