using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace KonkoorHelp.Api.Services.Learning.Ai;

public sealed class NullAiProvider : IAiProvider
{
    public string Code => "null";

    public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new AiCompletionResult(false, "", "ارائه‌دهنده هوش مصنوعی پیکربندی نشده است.", Code));
}

/// <summary>Deterministic local coach used when no external API key is configured.</summary>
public sealed class HeuristicAiProvider : IAiProvider
{
    public string Code => "heuristic";

    public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken)
    {
        var prompt = request.UserPrompt;
        string text;
        if (prompt.Contains("\"kind\":\"weekly_report\"", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("weekly_report", StringComparison.OrdinalIgnoreCase))
        {
            text = BuildWeekly(prompt);
        }
        else if (prompt.Contains("\"kind\":\"planner\"", StringComparison.OrdinalIgnoreCase)
                 || prompt.Contains("planner", StringComparison.OrdinalIgnoreCase))
        {
            text = BuildPlanner(prompt);
        }
        else if (prompt.Contains("\"kind\":\"chat\"", StringComparison.OrdinalIgnoreCase))
        {
            text = BuildChat(prompt);
        }
        else
        {
            text = BuildCoach(prompt);
        }

        return Task.FromResult(new AiCompletionResult(true, text, null, Code));
    }

    private static string BuildWeekly(string prompt)
    {
        var minutes = ExtractInt(prompt, "studyMinutes") ?? 0;
        var prev = ExtractInt(prompt, "prevStudyMinutes") ?? 0;
        var weak = ExtractString(prompt, "weakestSubject") ?? "ریاضی";
        var strong = ExtractString(prompt, "strongestSubject") ?? "علوم";
        var delta = minutes - prev;
        var trend = delta >= 0
            ? $"زمان مطالعه این هفته نسبت به قبل حدود {Math.Abs(delta)} دقیقه افزایش یافته است."
            : $"زمان مطالعه این هفته حدود {Math.Abs(delta)} دقیقه کمتر از هفته قبل بوده است.";
        return $$"""
            {
              "narrative": "{{trend}} عملکرد {{strong}} بهتر شده، اما {{weak}} نیاز به تمرین بیشتری دارد. سه جلسه متمرکز روی {{weak}} برای هفته آینده پیشنهاد می‌شود.",
              "recommendations": [
                {"title":"سه جلسه {{weak}}","kind":"StudyPlan"},
                {"title":"بازبینی مباحث ضعیف","kind":"Review"}
              ]
            }
            """;
    }

    private static string BuildPlanner(string prompt)
    {
        var weak = ExtractString(prompt, "weakestSubject") ?? "ریاضی";
        var minutes = ExtractInt(prompt, "availableMinutes") ?? 120;
        var block = Math.Max(25, Math.Min(50, minutes / 3));
        return $$"""
            {
              "title":"برنامه پیشنهادی هفته",
              "schedule":[
                {"day":"شنبه","subject":"{{weak}}","minutes":{{block}},"focus":"تمرین هدفمند"},
                {"day":"دوشنبه","subject":"{{weak}}","minutes":{{block}},"focus":"بازبینی اشتباهات"},
                {"day":"چهارشنبه","subject":"مرور عمومی","minutes":{{block}},"focus":"بازبینی فاصله‌دار"}
              ]
            }
            """;
    }

    private static string BuildCoach(string prompt)
    {
        var weak = ExtractString(prompt, "weakestSubject") ?? "ریاضی";
        return $$"""
            {
              "analysis":"بر اساس داده‌های مجاز فضای کاری، ضعف نسبی در {{weak}} دیده می‌شود.",
              "recommendations":[
                {"title":"تمرکز روی {{weak}}","kind":"Weakness","body":"دو جلسه کوتاه با آزمون موضوعی."},
                {"title":"بازبینی سررسید","kind":"Review","body":"کارت‌های overdue را امروز مرور کن."},
                {"title":"ماموریت تمرینی","kind":"Quest","body":"یک ماموریت روزانه روی مبحث ضعیف فعال کن."}
              ]
            }
            """;
    }

    private static string BuildChat(string prompt)
    {
        var weak = ExtractString(prompt, "weakestSubject") ?? "ریاضی";
        var confidence = ExtractString(prompt, "confidence") ?? "medium";
        var reply = confidence == "low"
            ? "داده کافی نیست؛ بعد از چند ارزیابی یا بازبینی موفق دوباره بپرس."
            : $"طبق شواهد فضای کاری، روی «{weak}» تمرکز کوتاه داشته باش و کارت‌های سررسید را امروز مرور کن.";
        return $$"""
            {
              "reply":"{{reply}}",
              "confidence":"{{confidence}}",
              "evidenceRefs":[{"type":"Fact","id":"weakestSubject","subject":"{{weak}}","detail":"از facts مجاز"}]
            }
            """;
    }

    private static int? ExtractInt(string text, string key)
    {
        var marker = $"\"{key}\":";
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var slice = text[(idx + marker.Length)..];
        var digits = new string(slice.TakeWhile(ch => char.IsDigit(ch) || ch == '-').ToArray());
        return int.TryParse(digits, out var value) ? value : null;
    }

    private static string? ExtractString(string text, string key)
    {
        var marker = $"\"{key}\":\"";
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var end = text.IndexOf('"', start);
        return end > start ? text[start..end] : null;
    }
}

public sealed class OpenAiCompatibleProvider(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options) : IAiProvider
{
    public string Code => "openai";

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken)
    {
        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.OpenAiApiKey))
        {
            return new AiCompletionResult(false, "", "کلید OpenAI تنظیم نشده است.", Code);
        }

        var baseUrl = string.IsNullOrWhiteSpace(cfg.OpenAiBaseUrl) ? "https://api.openai.com/v1" : cfg.OpenAiBaseUrl.TrimEnd('/');
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.OpenAiApiKey);
        var payload = new
        {
            model = string.IsNullOrWhiteSpace(request.Model) ? cfg.OpenAiModel : request.Model,
            temperature = request.Temperature,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt },
            },
        };
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var http = httpClientFactory.CreateClient(nameof(OpenAiCompatibleProvider));
            using var response = await http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new AiCompletionResult(false, "", $"OpenAI error {(int)response.StatusCode}", Code);
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            return new AiCompletionResult(true, text, null, Code);
        }
        catch (Exception ex)
        {
            return new AiCompletionResult(false, "", ex.Message, Code);
        }
    }
}

public sealed class AiProviderResolver(IEnumerable<IAiProvider> providers) : IAiProviderResolver
{
    private readonly Dictionary<string, IAiProvider> _map = providers.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AvailableCodes => _map.Keys.OrderBy(item => item).ToList();

    public IAiProvider Resolve(string providerCode)
    {
        if (string.IsNullOrWhiteSpace(providerCode)) providerCode = "heuristic";
        if (_map.TryGetValue(providerCode, out var provider)) return provider;
        if (_map.TryGetValue("heuristic", out var fallback)) return fallback;
        return _map.Values.First();
    }
}
