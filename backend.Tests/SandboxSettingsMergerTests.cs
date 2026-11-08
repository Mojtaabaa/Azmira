using System.Text.Json.Nodes;
using KonkoorHelp.Api.Services.Sandbox;

namespace KonkoorHelp.Tests;

public class SandboxSettingsMergerTests
{
    [Fact]
    public void MergeMissing_adds_new_keys_without_clobbering_existing()
    {
        var defaults = SandboxSettingsMerger.ParseObject(
            """
            {
              "ConnectionStrings": { "KonkoorHelp": "", "Extra": "new" },
              "Ai": { "DefaultProvider": "heuristic", "OpenAiModel": "gpt-4o-mini" },
              "NewSection": { "Flag": true }
            }
            """);
        var live = SandboxSettingsMerger.ParseObject(
            """
            {
              "ConnectionStrings": { "KonkoorHelp": "Server=prod;" },
              "Ai": { "DefaultProvider": "openai", "OpenAiApiKey": "secret" }
            }
            """);

        var added = new List<string>();
        SandboxSettingsMerger.MergeMissing(defaults, live, "", added);

        Assert.Equal("Server=prod;", live["ConnectionStrings"]!["KonkoorHelp"]!.GetValue<string>());
        Assert.Equal("openai", live["Ai"]!["DefaultProvider"]!.GetValue<string>());
        Assert.Equal("secret", live["Ai"]!["OpenAiApiKey"]!.GetValue<string>());
        Assert.Equal("new", live["ConnectionStrings"]!["Extra"]!.GetValue<string>());
        Assert.Equal("gpt-4o-mini", live["Ai"]!["OpenAiModel"]!.GetValue<string>());
        Assert.True(live["NewSection"]!["Flag"]!.GetValue<bool>());
        Assert.Contains("ConnectionStrings.Extra", added);
        Assert.Contains("Ai.OpenAiModel", added);
        Assert.Contains("NewSection", added);
    }

    [Fact]
    public void EnsureMerged_creates_live_file_and_merges_defaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "rotbeh-sandbox-" + Guid.NewGuid().ToString("N"));
        var sandbox = Path.Combine(root, "sandbox");
        Directory.CreateDirectory(sandbox);
        File.WriteAllText(
            Path.Combine(sandbox, "appsettings.defaults.json"),
            """
            { "ConnectionStrings": { "KonkoorHelp": "" }, "BrandNew": { "X": 1 } }
            """);
        File.WriteAllText(
            Path.Combine(sandbox, "appsettings.json"),
            """
            { "ConnectionStrings": { "KonkoorHelp": "Server=keep;" } }
            """);

        try
        {
            var livePath = SandboxSettingsMerger.EnsureMerged(root);
            var json = File.ReadAllText(livePath);
            var node = JsonNode.Parse(json)!.AsObject();
            Assert.Equal("Server=keep;", node["ConnectionStrings"]!["KonkoorHelp"]!.GetValue<string>());
            Assert.Equal(1, node["BrandNew"]!["X"]!.GetValue<int>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
