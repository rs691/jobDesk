using JobDesk.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobDesk.Controllers;

public class AskController : Controller
{
    private readonly IAskService _ask;

    public AskController(IAskService ask) => _ask = ask;

    [AcceptVerbs("GET", "POST")]
    [IgnoreAntiforgeryToken] // simple ask form; no state change
    public async Task<IActionResult> Index(string? q)
    {
        ViewData["Title"] = "Ask";
        ViewBag.Question = q ?? "";
        if (string.IsNullOrWhiteSpace(q))
            return View();

        var result = await _ask.AskAsync(q);
        ViewBag.Answer = result.Answer;
        ViewBag.Source = result.Source;
        ViewBag.ModelId = result.ModelId;
        return View();
    }
}
