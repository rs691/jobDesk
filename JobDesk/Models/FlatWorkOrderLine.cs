namespace JobDesk.Models;

// One row of a denormalized "WorkOrderSheet": customer and material facts
// are copied onto every material line. Used only by the normalization lesson.
public class FlatWorkOrderLine
{
    public int WorkOrderId { get; set; }
    public string WorkOrderTitle { get; set; } = string.Empty;
    public WorkOrderStatus Status { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerCity { get; set; }
    public string? MaterialName { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? Quantity { get; set; }
}

public class NormalizationViewModel
{
    public List<FlatWorkOrderLine> FlatLines { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
    public List<WorkOrder> WorkOrders { get; set; } = new();
    public List<Material> Materials { get; set; } = new();
    public List<WorkOrderMaterial> Lines { get; set; } = new();
}
