using JobDesk.Models;
using JobDesk.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobDesk.Controllers;

// Thin controller: validate input, call the service, pick a view.
public class CustomersController : Controller
{
    private readonly IJobService _jobs;

    public CustomersController(IJobService jobs) => _jobs = jobs;

    public async Task<IActionResult> Index() => View(await _jobs.GetCustomersAsync());

    public IActionResult Create() => View(new Customer());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Phone,Email,City")] Customer customer)
    {
        if (!ModelState.IsValid) return View(customer);

        await _jobs.AddCustomerAsync(customer);
        TempData["Message"] = $"Added customer \"{customer.Name}\".";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _jobs.GetCustomerAsync(id);
        return customer is null ? NotFound() : View(customer);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Phone,Email,City")] Customer customer)
    {
        if (id != customer.Id) return BadRequest();
        if (!ModelState.IsValid) return View(customer);

        if (!await _jobs.UpdateCustomerAsync(customer)) return NotFound();
        TempData["Message"] = $"Saved \"{customer.Name}\".";
        return RedirectToAction(nameof(Index));
    }
}
