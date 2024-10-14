using System.Diagnostics;
using System.Threading;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Linux.Startup;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Services;
using Remotely.Desktop.Shared.Startup;
using Remotely.Desktop.UI;
using Remotely.Desktop.UI.Services;
using Remotely.Shared.Services;
using Remotely.Shared.Utilities;

namespace Remotely.Desktop.Linux;

public class Program
{
    // This is needed for the visual designer to work.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static async Task Main(string[] args)
    {
        var version = AppVersionHelper.GetAppVersion();
        var logger = new FileLogger("Remotely_Desktop", version, "Program.cs");
        var filePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs().First();
        var serverUrl = Debugger.IsAttached ? "http://192.168.1.156:5000" : string.Empty;
        var getEmbeddedResult = EmbeddedServerDataProvider.Instance.TryGetEmbeddedData(filePath);
        if (getEmbeddedResult.IsSuccess)
        {
            serverUrl = getEmbeddedResult.Value.ServerUrl.AbsoluteUri;
        }
        else
        {
            logger.LogWarning(getEmbeddedResult.Exception, "Failed to extract embedded server data");
        }

        var services = new ServiceCollection();

        services.AddSingleton<IEmbeddedServerDataProvider, EmbeddedServerDataProvider>();
        services.AddRemoteControlLinux();

        services.AddLogging(builder =>
        {
            // if (EnvironmentHelper.IsDebug)
            // {
                builder.SetMinimumLevel(LogLevel.Debug);
            // }
            builder.AddProvider(new FileLoggerProvider("Remotely_Desktop", version));
        });

        var provider = services.BuildServiceProvider();

        var appState = provider.GetRequiredService<IAppState>();

        if (getEmbeddedResult.IsSuccess)
        {
            appState.OrganizationId = getEmbeddedResult.Value.OrganizationId;
            appState.Host = getEmbeddedResult.Value.ServerUrl.AbsoluteUri;
        }

        if (appState.ArgDict.TryGetValue("org-id", out var orgId))
        {
            appState.OrganizationId = orgId;
        }

        var result = await provider.UseRemoteControlClient(
            args,
            "The remote control client for Remotely.",
            serverUrl,
            false);

        if (!result.IsSuccess)
        {
            logger.LogError(result.Exception, "Failed to start remote control client");
            Environment.Exit(1);
        }


        Console.WriteLine("Press Ctrl + C to exit.");

        var shutdownService = provider.GetRequiredService<IShutdownService>();
        Console.CancelKeyPress += async (_, _) =>
        {
            await shutdownService.Shutdown();
        };

        var dispatcher = provider.GetRequiredService<IUiDispatcher>();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, dispatcher.ApplicationExitingToken);
        }
        catch (TaskCanceledException) { }
    }
}