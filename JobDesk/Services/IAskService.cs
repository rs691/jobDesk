namespace JobDesk.Services;

public record AskResult(string Answer, string Source, string? ModelId, string? Error);

public interface IAskService
{
    Task<AskResult> AskAsync(string question, CancellationToken ct = default);
}
