using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

public interface IBlobService
{
    Task<string> UploadAsync(string container, string blobName, Stream file, string contentType);
    Task<Stream> DownloadAsync(string container, string blobName);
    Task DeleteAsync(string container, string blobName);
    string GetReadSasUrl(string container, string blobName, TimeSpan ttl);
}

public class BlobService : IBlobService
{
    private readonly BlobServiceClient _svc;
    public BlobService(IConfiguration cfg)
    {
        _svc = new BlobServiceClient(cfg["Storage:ConnectionString"]);
    }

    // 1) Upload file lên container private
    public async Task<string> UploadAsync(string container, string blobName, Stream file, string contentType)
    {
        var containerClient = _svc.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync();
        var client = containerClient.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await client.UploadAsync(file, new BlobUploadOptions { HttpHeaders = headers });

        // Trả về URI (không truy cập được nếu không có SAS hoặc stream qua API)
        return client.Uri.ToString();
    }

    // 2) Tải file (dành cho server stream về client)
    public async Task<Stream> DownloadAsync(string container, string blobName)
    {
        var client = _svc.GetBlobContainerClient(container).GetBlobClient(blobName);
        if (!await client.ExistsAsync()) throw new FileNotFoundException(blobName);
        var resp = await client.DownloadStreamingAsync();
        return resp.Value.Content; // nhớ set Content-Type ở controller
    }

    // 3) Xóa file (kể cả snapshot)
    public async Task DeleteAsync(string container, string blobName)
    {
        var client = _svc.GetBlobContainerClient(container).GetBlobClient(blobName);
        await client.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
    }

    // 4) Cấp link tải tạm (SAS) quyền READ, hết hạn sau ttl
    public string GetReadSasUrl(string container, string blobName, TimeSpan ttl)
    {
        var client = _svc.GetBlobContainerClient(container).GetBlobClient(blobName);
        if (!client.CanGenerateSasUri)
            throw new InvalidOperationException("Storage credentials không hỗ trợ SAS. Hãy dùng connection string với account key.");

        var sas = new BlobSasBuilder
        {
            BlobContainerName = container,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(ttl)
        };
        sas.SetPermissions(BlobSasPermissions.Read);

        return client.GenerateSasUri(sas).ToString();
    }
}
