namespace JobDesk.Storage;

// Where attachment bytes live. Callers only know a key like
// "work-orders/3/9f1c....jpg"; they don't know if it's a folder or an S3 bucket.
public interface IFileStorage
{
    string ProviderName { get; }
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

public class StorageOptions
{
    public const string Section = "Storage";

    // "Local" (default) or "S3"
    public string Provider { get; set; } = "Local";

    // Relative to the project folder when running locally.
    public string LocalPath { get; set; } = "App_Data/uploads";
}

// Bound from the "AWS" section. Region / Profile in the same section are
// read by the AWS SDK itself. Credentials never go in appsettings: the SDK
// finds them in env vars, ~/.aws/credentials, or the IAM role of the server.
public class AwsS3Options
{
    public const string Section = "AWS";

    public string BucketName { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "jobdesk/";
}
