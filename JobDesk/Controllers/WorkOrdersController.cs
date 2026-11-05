using JobDesk.Models;
using JobDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace JobDesk.Controllers;

public class WorkOrdersController : Controller
{
    private readonly IJobService _jobs;
    private readonly IAttachmentService _attachments;

    public WorkOrdersController(IJobService jobs, IAttachmentService attachments)
    {
        _jobs = jobs;
        _attachments = attachments;
    }

    // GET /WorkOrders?status=Open
    public async Task<IActionResult> Index(WorkOrderStatus? status)
    {
        ViewBag.Status = status;
        return View(await _jobs.GetWorkOrdersAsync(status));
    }

    public async Task<IActionResult> Create()
    {
        await LoadCustomersAsync();
        return View(new WorkOrder { ScheduledDate = DateTime.Today.AddDays(7) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("CustomerId,Title,Status,ScheduledDate")] WorkOrder workOrder)
    {
        if (!ModelState.IsValid)
        {
            await LoadCustomersAsync();
            return View(workOrder);
        }

        await _jobs.AddWorkOrderAsync(workOrder);
        TempData["Message"] = $"Created work order \"{workOrder.Title}\".";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var workOrder = await _jobs.GetWorkOrderAsync(id);
        if (workOrder is null) return NotFound();

        await LoadCustomersAsync();
        return View(workOrder);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,CustomerId,Title,Status,ScheduledDate")] WorkOrder workOrder)
    {
        if (id != workOrder.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await LoadCustomersAsync();
            return View(workOrder);
        }

        if (!await _jobs.UpdateWorkOrderAsync(workOrder)) return NotFound();
        TempData["Message"] = $"Saved work order #{workOrder.Id}.";
        return RedirectToAction(nameof(Index));
    }

    // GET /WorkOrders/Details/3 — materials plus photo/file attachments
    public async Task<IActionResult> Details(int id)
    {
        var workOrder = await _jobs.GetWorkOrderDetailsAsync(id);
        if (workOrder is null) return NotFound();

        ViewBag.StorageProvider = _attachments.StorageProvider;
        return View(workOrder);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(AttachmentService.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(int id, IFormFile? file)
    {
        if (file is null)
        {
            TempData["Error"] = "Choose a file to upload.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var stream = file.OpenReadStream();
        var result = await _attachments.UploadAsync(id, stream, file.FileName, file.ContentType, file.Length);
        TempData[result.Success ? "Message" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET /WorkOrders/Attachment/5 — streams the file from whichever IFileStorage is configured
    public async Task<IActionResult> Attachment(int id)
    {
        var file = await _attachments.OpenAsync(id);
        if (file is null) return NotFound();

        var (attachment, content) = file.Value;
        return attachment.IsImage
            ? File(content, attachment.ContentType)
            : File(content, attachment.ContentType, attachment.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id)
    {
        var workOrderId = await _attachments.DeleteAsync(id);
        if (workOrderId is null) return NotFound();

        TempData["Message"] = "Attachment deleted.";
        return RedirectToAction(nameof(Details), new { id = workOrderId });
    }

    private async Task LoadCustomersAsync() =>
        ViewBag.Customers = new SelectList(await _jobs.GetCustomersAsync(), nameof(Customer.Id), nameof(Customer.Name));
}
