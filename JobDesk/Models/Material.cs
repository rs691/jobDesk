namespace JobDesk.Models;

public class Material
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "each";
    public decimal UnitCost { get; set; }
}

// One row per material line on a work order. This is the "line grain" table:
// a work order can have zero, one, or many of these.
public class WorkOrderMaterial
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public int MaterialId { get; set; }
    public Material? Material { get; set; }
    public decimal Quantity { get; set; }
}
