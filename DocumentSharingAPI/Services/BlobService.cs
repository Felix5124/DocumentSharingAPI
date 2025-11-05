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

    // Upload file lên container
    public async Task<string> UploadAsync(string container, string blobName, Stream file, string contentType)
    {
        var containerClient = _svc.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync();

        var client = containerClient.GetBlobClient(blobName);

        // Nếu blob đã tồn tại thì xóa trước (kèm snapshots)
        await client.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await client.UploadAsync(file, new BlobUploadOptions
        {
            HttpHeaders = headers
            // Không có Overwrite ở đây, đừng cố thêm
        });

        return client.Uri.ToString();
    }


    // Download file từ blob
    public async Task<Stream> DownloadAsync(string container, string blobName)
    {
        var client = _svc.GetBlobContainerClient(container).GetBlobClient(blobName);
        if (!await client.ExistsAsync())
            throw new FileNotFoundException(blobName);

        var resp = await client.DownloadStreamingAsync();
        return resp.Value.Content;
    }

    // Xóa file
    public async Task DeleteAsync(string container, string blobName)
    {
        var client = _svc.GetBlobContainerClient(container).GetBlobClient(blobName);
        await client.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
    }

    // Tạo link SAS tạm thời cho quyền READ
    public string GetReadSasUrl(string container, string blobName, TimeSpan ttl)
    {
        var client = _svc.GetBlobContainerClient(container).GetBlobClient(blobName);

        if (!client.CanGenerateSasUri)
            throw new InvalidOperationException("Storage credentials không hỗ trợ SAS. Hãy dùng connection string có account key.");

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
