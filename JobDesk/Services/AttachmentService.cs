using JobDesk.Data;
using JobDesk.Hosting;
using JobDesk.Models;
using JobDesk.Storage;
using Microsoft.EntityFrameworkCore;

namespace JobDesk.Services;

public interface IAttachmentService
{
    string StorageProvider { get; }
    Task<AttachmentResult> UploadAsync(int workOrderId, Stream content, string fileName, string contentType, long length);
    Task<(WorkOrderAttachment Attachment, Stream Content)?> OpenAsync(int attachmentId);
    Task<int?> DeleteAsync(int attachmentId);
}

public record AttachmentResult(bool Success, string Message);

public class AttachmentService : IAttachmentService
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf"
    };

    private readonly JobDeskContext _db;
    private readonly IFileStorage _storage;
    private readonly DemoOptions _demo;

    public AttachmentService(JobDeskContext db, IFileStorage storage, DemoOptions demo)
    {
        _db = db;
        _storage = storage;
        _demo = demo;
    }

    public string StorageProvider => _storage.ProviderName;

    public async Task<AttachmentResult> UploadAsync(int workOrderId, Stream content, string fileName, string contentType, long length)
    {
        if (_demo.ReadOnly) throw new ReadOnlyDemoException();
        if (length == 0) return new(false, "Choose a file to upload.");
        if (length > MaxBytes) return new(false, "Files must be 5 MB or smaller.");

        var extension = Path.GetExtension(fileName);
        if (!AllowedTypes.TryGetValue(extension, out var safeContentType))
            return new(false, "Only JPG, PNG, GIF, WEBP, or PDF files are allowed.");

        if (!await _db.WorkOrders.AnyAsync(w => w.Id == workOrderId))
            return new(false, "Work order not found.");

        // Never use the uploaded name as the storage key: it can contain paths or collide.
        var key = $"work-orders/{workOrderId}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await _storage.SaveAsync(key, content, safeContentType);

        var attachment = new WorkOrderAttachment
        {
            WorkOrderId = workOrderId,
            FileName = Path.GetFileName(fileName),
            ContentType = safeContentType,
            SizeBytes = length,
            StorageKey = key,
            StorageProvider = _storage.ProviderName
        };

        try
        {
            _db.WorkOrderAttachments.Add(attachment);
            await _db.SaveChangesAsync();
        }
        catch
        {
            await _storage.DeleteAsync(key);
            throw;
        }

        return new(true, $"Uploaded \"{attachment.FileName}\".");
    }

    public async Task<(WorkOrderAttachment Attachment, Stream Content)?> OpenAsync(int attachmentId)
    {
        var attachment = await _db.WorkOrderAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (attachment is null) return null;

        var stream = await _storage.OpenReadAsync(attachment.StorageKey);
        return stream is null ? null : (attachment, stream);
    }

    public async Task<int?> DeleteAsync(int attachmentId)
    {
        if (_demo.ReadOnly) throw new ReadOnlyDemoException();
        var attachment = await _db.WorkOrderAttachments.FindAsync(attachmentId);
        if (attachment is null) return null;

        await _storage.DeleteAsync(attachment.StorageKey);
        _db.WorkOrderAttachments.Remove(attachment);
        await _db.SaveChangesAsync();
        return attachment.WorkOrderId;
    }
}
