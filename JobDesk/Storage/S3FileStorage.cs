using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace JobDesk.Storage;

// Used when Storage:Provider = "S3". IAmazonS3 comes from DI (AddAWSService),
// which picks up region from config and credentials from the default AWS chain.
public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly AwsS3Options _options;

    public S3FileStorage(IAmazonS3 s3, IOptions<AwsS3Options> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public string ProviderName => $"Amazon S3 (s3://{_options.BucketName}/{_options.KeyPrefix})";

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = FullKey(key),
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _s3.GetObjectAsync(_options.BucketName, FullKey(key), ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _s3.DeleteObjectAsync(_options.BucketName, FullKey(key), ct);
    }

    private string FullKey(string key) => _options.KeyPrefix + key;
}
