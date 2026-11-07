namespace KonkoorHelp.Api.Services.Learning.Ai;

public interface IAiProvider
{
    string Code { get; }
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken);
}

public sealed record AiCompletionRequest(
    string Model,
    string SystemPrompt,
    string UserPrompt,
    double Temperature = 0.4);

public sealed record AiCompletionResult(
    bool Ok,
    string Text,
    string? Error = null,
    string ProviderCode = "");

public interface IAiProviderResolver
{
    IAiProvider Resolve(string providerCode);
    IReadOnlyList<string> AvailableCodes { get; }
}

public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public string DefaultProvider { get; set; } = "heuristic";
    public string? OpenAiApiKey { get; set; }
    public string? OpenAiBaseUrl { get; set; }
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string? AzureOpenAiApiKey { get; set; }
    public string? AzureOpenAiEndpoint { get; set; }
    public string? AzureOpenAiDeployment { get; set; }
}
