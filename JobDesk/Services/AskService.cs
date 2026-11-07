using System.Text;
using JobDesk.Data;
using JobDesk.Models;
using JobDesk.Services.Databricks;
using Microsoft.EntityFrameworkCore;

namespace JobDesk.Services;

public class AskOptions
{
    public const string Section = "Ask";
    public bool UseBedrock { get; set; } = true;
    public string ModelId { get; set; } = "amazon.nova-micro-v1:0";
    public string Region { get; set; } = "us-east-1";
}

public class AskService : IAskService
{
    private readonly JobDeskContext _db;
    private readonly AskOptions _opts;
    private readonly DatabricksOptions _dbx;
    private readonly IDatabricksStatementClient _databricks;
    private readonly ILogger<AskService> _log;

    public AskService(
        JobDeskContext db,
        AskOptions opts,
        DatabricksOptions dbx,
        IDatabricksStatementClient databricks,
        ILogger<AskService> log)
    {
        _db = db;
        _opts = opts;
        _dbx = dbx;
        _databricks = databricks;
        _log = log;
    }

    public async Task<AskResult> AskAsync(string question, CancellationToken ct = default)
    {
        question = (question ?? "").Trim();
        if (question.Length == 0)
            return new AskResult("Type a short question about the jobs.", "none", null, null);
        if (question.Length > 500)
            question = question[..500];

        if (_dbx.Enabled)
        {
            if (!_dbx.IsConfigured)
            {
                var localOnly = await AnswerFromLocalAsync(question, ct);
                return localOnly with
                {
                    Answer = localOnly.Answer
                        + "\n\n(Databricks is enabled but not fully configured — no warehouse token/catalog. Answer uses local SQLite demo only.)",
                    Source = "local+databricks-unconfigured",
                    Error = "Databricks not configured."
                };
            }

            var intent = AskIntentResolver.Resolve(question);
            if (intent is null)
                return new AskResult(AskIntentResolver.HelpText(), "databricks-help", null, null);

            try
            {
                var sql = AskAllowlistedSql.Build(intent.Value, _dbx);
                var query = await _databricks.ExecuteSelectAsync(sql, ct);
                var facts = AskFactsFormatter.FormatFacts(intent.Value, query, _dbx);

                if (_opts.UseBedrock)
                {
                    try
                    {
                        var answer = await BedrockAskClient.InvokeAsync(
                            _opts,
                            question,
                            facts + "\nAnswer briefly using only these facts. Say if the facts are empty.",
                            ct);
                        if (!string.IsNullOrWhiteSpace(answer))
                            return new AskResult(answer.Trim(), "databricks+bedrock", _opts.ModelId, null);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Bedrock failed after Databricks facts");
                        var plain = AskFactsFormatter.FormatDeterministic(intent.Value, query, _dbx);
                        return new AskResult(
                            plain + "\n\n(Bedrock unavailable — showing Databricks rows only.)",
                            "databricks",
                            null,
                            ex.Message);
                    }
                }

                return new AskResult(
                    AskFactsFormatter.FormatDeterministic(intent.Value, query, _dbx),
                    "databricks",
                    null,
                    null);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Databricks Ask path failed");
                var fallback = await AnswerFromLocalAsync(question, ct);
                return new AskResult(
                    fallback.Answer
                    + "\n\n(Databricks warehouse unavailable — answer below uses local JobDesk SQLite demo, not the synthetic warehouse.)",
                    fallback.Source + "+databricks-fallback",
                    fallback.ModelId,
                    ex.Message);
            }
        }

        return await AnswerFromLocalAsync(question, ct);
    }

    private async Task<AskResult> AnswerFromLocalAsync(string question, CancellationToken ct)
    {
        var context = await BuildLocalContextAsync(ct);

        if (_opts.UseBedrock)
        {
            try
            {
                var answer = await BedrockAskClient.InvokeAsync(
                    _opts,
                    question,
                    "LOCAL SQLITE DEMO DATA (small seed — not the synthetic Databricks warehouse):\n" + context,
                    ct);
                if (!string.IsNullOrWhiteSpace(answer))
                    return new AskResult(answer.Trim(), "bedrock", _opts.ModelId, null);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Bedrock Ask failed; using local fallback");
                var fallback = HeuristicAnswer(question, context);
                return new AskResult(
                    fallback + "\n\n(Bedrock unavailable right now — answered from the local demo list instead.)",
                    "fallback",
                    _opts.ModelId,
                    ex.Message);
            }
        }

        return new AskResult(HeuristicAnswer(question, context), "local", null, null);
    }

    private async Task<string> BuildLocalContextAsync(CancellationToken ct)
    {
        var customers = await _db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var orders = await _db.WorkOrders.AsNoTracking()
            .Include(w => w.Customer)
            .Include(w => w.Materials).ThenInclude(m => m.Material)
            .OrderBy(w => w.ScheduledDate).ThenBy(w => w.Id)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("CUSTOMERS:");
        foreach (var c in customers)
            sb.AppendLine($"- {c.Id}: {c.Name} ({c.City}) phone={c.Phone}");

        sb.AppendLine("WORK ORDERS:");
        foreach (var w in orders)
        {
            var cost = w.Materials.Sum(m => m.Quantity * (m.Material?.UnitCost ?? 0));
            sb.AppendLine(
                $"- #{w.Id} [{w.Status}] \"{w.Title}\" customer={w.Customer?.Name} scheduled={w.ScheduledDate?.ToString("yyyy-MM-dd") ?? "none"} materialCost=${cost:0.00}");
        }

        var openByCust = orders.Where(w => w.Status == WorkOrderStatus.Open)
            .GroupBy(w => w.Customer?.Name ?? "?")
            .Select(g => $"{g.Key}: {g.Count()} open");
        sb.AppendLine("OPEN BY CUSTOMER: " + string.Join("; ", openByCust));
        return sb.ToString();
    }

    private static string HeuristicAnswer(string question, string context)
    {
        var q = question.ToLowerInvariant();
        var lines = context.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (q.Contains("open") && (q.Contains("customer") || q.Contains("who") || q.Contains("summary")))
        {
            var open = lines.FirstOrDefault(l => l.StartsWith("OPEN BY CUSTOMER"));
            return open != null ? open.Replace("OPEN BY CUSTOMER: ", "Open jobs — ") : "No open jobs.";
        }

        if (q.Contains("next") || q.Contains("schedule") || q.Contains("upcoming"))
        {
            var upcoming = lines.Where(l => l.Contains("[Open]") || l.Contains("[Scheduled]"))
                .Where(l => l.Contains("scheduled=") && !l.Contains("scheduled=none"))
                .Take(5);
            return "Next up:\n" + string.Join("\n", upcoming);
        }

        if (q.Contains("material") || q.Contains("cost") || q.Contains("total"))
        {
            var costs = lines.Where(l => l.StartsWith("- #") && l.Contains("materialCost=")).Take(10);
            return "Material costs:\n" + string.Join("\n", costs);
        }

        if (q.Contains("note") || q.Contains("draft") || q.Contains("write"))
        {
            var first = lines.FirstOrDefault(l => l.Contains("[Open]"));
            return "Draft note:\nCrew — check materials before leave. Re: " + (first ?? "open jobs") +
                   "\nCall customer if site access needed.";
        }

        return "I can answer about open jobs by customer, next scheduled work, material totals, or draft a short note. "
               + "When Databricks is enabled, try one of the allowlisted warehouse questions listed on this page.\n\n"
               + (lines.FirstOrDefault(l => l.StartsWith("OPEN BY CUSTOMER")) ?? "");
    }
}
