namespace JobDesk.Models;

// Metadata lives in the database; the file bytes live in IFileStorage
// (a local folder on F5, an S3 bucket in the cloud). StorageKey links the two.
public class WorkOrderAttachment
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    public string StorageKey { get; set; } = string.Empty;
    public string StorageProvider { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
