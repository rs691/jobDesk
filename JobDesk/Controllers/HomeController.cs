using System.Diagnostics;
using JobDesk.Models;
using JobDesk.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobDesk.Controllers;

public class HomeController : Controller
{
    private readonly IJobService _jobs;

    public HomeController(IJobService jobs) => _jobs = jobs;

    public async Task<IActionResult> Index()
    {
        ViewBag.CustomerCount = (await _jobs.GetCustomersAsync()).Count;
        ViewBag.OpenCount = (await _jobs.GetWorkOrdersAsync(WorkOrderStatus.Open)).Count;
        ViewBag.TotalCount = (await _jobs.GetWorkOrdersAsync(null)).Count;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
