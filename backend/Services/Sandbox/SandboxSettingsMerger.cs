using System.Text.Json;
using System.Text.Json.Nodes;

namespace KonkoorHelp.Api.Services.Sandbox;

/// <summary>
/// Keeps sandbox/appsettings.json in sync with shipped defaults:
/// adds missing keys from appsettings.defaults.json, never overwrites existing values.
/// </summary>
public static class SandboxSettingsMerger
{
    public const string SandboxFolderName = "sandbox";
    public const string LiveFileName = "appsettings.json";
    public const string DefaultsFileName = "appsettings.defaults.json";
    public const string ExampleFileName = "appsettings.json.example";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    public static string ResolveSandboxDirectory(string contentRoot)
    {
        var fromEnv = Environment.GetEnvironmentVariable("ROTBEH_SANDBOX")?.Trim();
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        return Path.GetFullPath(Path.Combine(contentRoot, SandboxFolderName));
    }

    /// <summary>
    /// Ensures live sandbox settings exist and contain every key from defaults.
    /// Returns path to live appsettings.json (may be newly created).
    /// </summary>
    public static string EnsureMerged(string contentRoot, ILogger? logger = null)
    {
        var sandboxDir = ResolveSandboxDirectory(contentRoot);
        Directory.CreateDirectory(sandboxDir);

        var defaultsPath = Path.Combine(sandboxDir, DefaultsFileName);
        var livePath = Path.Combine(sandboxDir, LiveFileName);
        var examplePath = Path.Combine(sandboxDir, ExampleFileName);

        // Prefer defaults next to the app; fall back to repo-shaped copy under content root.
        if (!File.Exists(defaultsPath))
        {
            var fallback = Path.Combine(contentRoot, SandboxFolderName, DefaultsFileName);
            if (File.Exists(fallback) && !string.Equals(fallback, defaultsPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(fallback, defaultsPath, overwrite: true);
            }
        }

        if (!File.Exists(livePath))
        {
            if (File.Exists(examplePath))
            {
                File.Copy(examplePath, livePath);
                logger?.LogWarning("Created {Live} from example — fill production secrets before serving traffic.", livePath);
            }
            else if (File.Exists(defaultsPath))
            {
                File.Copy(defaultsPath, livePath);
                logger?.LogWarning("Created {Live} from defaults — fill ConnectionStrings and secrets.", livePath);
            }
            else
            {
                File.WriteAllText(livePath, "{}\n");
                logger?.LogWarning("Created empty {Live}; no defaults found under {Sandbox}.", livePath, sandboxDir);
            }
        }

        if (!File.Exists(defaultsPath))
        {
            logger?.LogInformation("No {Defaults} in sandbox; skip key merge.", DefaultsFileName);
            return livePath;
        }

        var defaultsNode = ParseObject(File.ReadAllText(defaultsPath));
        var liveNode = ParseObject(File.ReadAllText(livePath));
        var added = new List<string>();
        MergeMissing(defaultsNode, liveNode, path: "", added);

        if (added.Count > 0)
        {
            File.WriteAllText(livePath, liveNode.ToJsonString(WriteOptions) + Environment.NewLine);
            logger?.LogInformation(
                "Sandbox settings merged {Count} new key(s) from defaults: {Keys}",
                added.Count,
                string.Join(", ", added));
        }

        return livePath;
    }

    internal static JsonObject ParseObject(string json)
    {
        var node = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return node as JsonObject ?? new JsonObject();
    }

    /// <summary>
    /// Recursively copies keys that exist in <paramref name="defaults"/> but are absent in <paramref name="live"/>.
    /// Existing live values (including empty string / empty array) are preserved.
    /// </summary>
    internal static void MergeMissing(JsonObject defaults, JsonObject live, string path, List<string> added)
    {
        foreach (var (key, defaultValue) in defaults)
        {
            if (defaultValue is null) continue;
            var childPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";

            if (!live.ContainsKey(key) || live[key] is null)
            {
                live[key] = defaultValue.DeepClone();
                added.Add(childPath);
                continue;
            }

            if (defaultValue is JsonObject defaultObj && live[key] is JsonObject liveObj)
            {
                MergeMissing(defaultObj, liveObj, childPath, added);
            }
        }
    }
}
