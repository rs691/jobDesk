using JobDesk.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobDesk.Controllers;

public class LearnController : Controller
{
    private readonly IJobService _jobs;

    public LearnController(IJobService jobs) => _jobs = jobs;

    public IActionResult Index() => RedirectToAction(nameof(Normalization));

    // GET /Learn/Normalization
    public async Task<IActionResult> Normalization() => View(await _jobs.GetNormalizationLessonAsync());
}
