using JobDesk.Models;

namespace JobDesk.Services;

// Controllers depend on this interface, not on EF directly,
// so the data access can be swapped or faked in tests.
public interface IJobService
{
    Task<List<Customer>> GetCustomersAsync();
    Task<Customer?> GetCustomerAsync(int id);
    Task AddCustomerAsync(Customer customer);
    Task<bool> UpdateCustomerAsync(Customer customer);

    Task<List<WorkOrder>> GetWorkOrdersAsync(WorkOrderStatus? status);
    Task<WorkOrder?> GetWorkOrderAsync(int id);
    Task<WorkOrder?> GetWorkOrderDetailsAsync(int id);
    Task AddWorkOrderAsync(WorkOrder workOrder);
    Task<bool> UpdateWorkOrderAsync(WorkOrder workOrder);

    // Stored procedures on SQL Server, the same SQL inline on SQLite.
    Task<OpenJobsReportViewModel> GetOpenJobsReportAsync(WorkOrderStatus status, bool useBuggyQuery = false);
    Task<CustomerJobsViewModel> GetWorkOrdersByCustomerAsync(int customerId);

    Task<NormalizationViewModel> GetNormalizationLessonAsync();
}
