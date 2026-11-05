using System.Text.RegularExpressions;

namespace JobDesk.Data;

// Reads sql/stored-procedures.sql (copied to the output folder by the .csproj)
// so the Reports page can show the real T-SQL instead of a pasted copy.
public static class StoredProcedureScripts
{
    private static readonly Lazy<string> Script = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "sql", "stored-procedures.sql");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    });

    // "GO" is a sqlcmd/SSMS batch separator, not T-SQL, so split on it before executing.
    private static string[] Batches() => Regex.Split(Script.Value, @"^\s*GO\s*$", RegexOptions.Multiline);

    public static IEnumerable<string> CreateBatches() =>
        Batches()
            .Select(b => b.IndexOf("CREATE OR ALTER PROCEDURE", StringComparison.OrdinalIgnoreCase) is var i and >= 0 ? b[i..].Trim() : null)
            .OfType<string>();

    public static string Get(string procName)
    {
        var batch = Batches().FirstOrDefault(b => b.Contains($"PROCEDURE {procName}", StringComparison.OrdinalIgnoreCase));
        if (batch is null) return $"-- {procName} not found in sql/stored-procedures.sql";

        var start = batch.IndexOf("CREATE OR ALTER PROCEDURE", StringComparison.OrdinalIgnoreCase);
        return (start >= 0 ? batch[start..] : batch).Trim();
    }
}
