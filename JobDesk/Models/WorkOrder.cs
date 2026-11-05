using System.ComponentModel.DataAnnotations;

namespace JobDesk.Models;

public enum WorkOrderStatus
{
    Open,
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public class WorkOrder
{
    public int Id { get; set; }

    [Display(Name = "Customer")]
    [Range(1, int.MaxValue, ErrorMessage = "Pick a customer.")]
    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;

    [DataType(DataType.Date), Display(Name = "Scheduled")]
    public DateTime? ScheduledDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<WorkOrderMaterial> Materials { get; set; } = new();

    public List<WorkOrderAttachment> Attachments { get; set; } = new();
}
