using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace KonkoorHelp.Tests;

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "KonkoorHelp.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
