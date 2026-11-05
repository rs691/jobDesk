using System.ComponentModel.DataAnnotations;

namespace JobDesk.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(100)]
    public string? Email { get; set; }

    [StringLength(60)]
    public string? City { get; set; }

    public List<WorkOrder> WorkOrders { get; set; } = new();
}
