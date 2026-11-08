using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;

namespace KonkoorHelp.Api.Services.Sandbox;

public static class SandboxConfigurationExtensions
{
    /// <summary>
    /// Merges sandbox defaults into live settings, then registers sandbox/appsettings.json
    /// so it overrides packaged appsettings but stays below environment variables / CLI.
    /// </summary>
    public static WebApplicationBuilder AddRotbehSandboxConfiguration(this WebApplicationBuilder builder)
    {
        var contentRoot = builder.Environment.ContentRootPath;
        var bootstrapLogger = LoggerFactory
            .Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information))
            .CreateLogger("SandboxSettings");

        var livePath = SandboxSettingsMerger.EnsureMerged(contentRoot, bootstrapLogger);
        var sandboxDir = Path.GetDirectoryName(livePath)!;

        var jsonSource = new JsonConfigurationSource
        {
            FileProvider = new PhysicalFileProvider(sandboxDir),
            Path = Path.GetFileName(livePath),
            Optional = true,
            ReloadOnChange = true,
        };

        // Insert before environment variables so ROTBEH_* / ASPNETCORE_* still win.
        var sources = builder.Configuration.Sources;
        var envIndex = -1;
        for (var i = 0; i < sources.Count; i++)
        {
            if (sources[i] is EnvironmentVariablesConfigurationSource)
            {
                envIndex = i;
                break;
            }
        }

        if (envIndex >= 0)
        {
            sources.Insert(envIndex, jsonSource);
        }
        else
        {
            sources.Add(jsonSource);
        }

        return builder;
    }
}
