using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;

namespace JobDesk.Services;

public static class BedrockAskClient
{
    public static async Task<string> InvokeAsync(AskOptions opts, string question, string context, CancellationToken ct)
    {
        var region = RegionEndpoint.GetBySystemName(string.IsNullOrWhiteSpace(opts.Region) ? "us-east-1" : opts.Region);
        using var client = new AmazonBedrockRuntimeClient(region);

        var prompt =
            "You help with a small work-order practice app called JobDesk.\n" +
            "Answer briefly from the JOB DATA only. No company brand names.\n" +
            "If asked for a note, write 2-4 rough sentences a tech might paste.\n" +
            "If data is missing, say so. Keep answers short and plain.\n\n" +
            $"JOB DATA:\n{context}\n\nQUESTION:\n{question}";

        var bodyObj = new Dictionary<string, object?>
        {
            ["messages"] = new object[]
            {
                new {
                    role = "user",
                    content = new object[] { new { text = prompt } }
                }
            },
            ["inferenceConfig"] = new { maxTokens = 400, temperature = 0.2 }
        };

        var payload = JsonSerializer.Serialize(bodyObj);
        var req = new InvokeModelRequest
        {
            ModelId = opts.ModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(payload))
        };

        var resp = await client.InvokeModelAsync(req, ct);
        using var reader = new StreamReader(resp.Body);
        var raw = await reader.ReadToEndAsync(ct);

        using var doc = JsonDocument.Parse(raw);
        if (doc.RootElement.TryGetProperty("output", out var output) &&
            output.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array && content.GetArrayLength() > 0 &&
            content[0].TryGetProperty("text", out var text))
            return text.GetString() ?? "";

        if (doc.RootElement.TryGetProperty("content", out var acontent) &&
            acontent.ValueKind == JsonValueKind.Array && acontent.GetArrayLength() > 0 &&
            acontent[0].TryGetProperty("text", out var atext))
            return atext.GetString() ?? "";

        return raw.Length > 800 ? raw[..800] : raw;
    }
}
