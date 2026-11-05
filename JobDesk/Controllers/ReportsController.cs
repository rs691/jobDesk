using JobDesk.Models;
using JobDesk.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobDesk.Controllers;

public class ReportsController : Controller
{
    private readonly IJobService _jobs;

    public ReportsController(IJobService jobs) => _jobs = jobs;

    // GET /Reports?status=Open&buggy=true&customerId=1
    public async Task<IActionResult> Index(WorkOrderStatus status = WorkOrderStatus.Open, bool buggy = false, int? customerId = null)
    {
        var customers = await _jobs.GetCustomersAsync();
        var selectedCustomer = customerId ?? customers.FirstOrDefault()?.Id ?? 0;

        return View(new ReportsPageViewModel
        {
            OpenJobs = await _jobs.GetOpenJobsReportAsync(status, buggy),
            CustomerJobs = await _jobs.GetWorkOrdersByCustomerAsync(selectedCustomer),
            Customers = customers
        });
    }
}
