using System.Diagnostics;
using System.Text.RegularExpressions;
using Sirona.RemoteControl.Synchronizer.Extensions;
using Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

namespace Sirona.RemoteControl.Synchronizer.Services.Launchers;

internal sealed class RemotelyAppLauncher(
    IStateBroadcaster broadcaster,
    ILogger<RemotelyAppLauncher> logger,
    IConfiguration configuration) : IAppLauncher, IDisposable
{
    private Process? _runningProcess;

    public Task StartApp(CancellationToken cancellationToken = default)
    {
        if (_runningProcess?.HasExited == false)
        {
            broadcaster.Broadcast(new SynchronizerState()
            {
                State = State.ConnectionError,
                Message = "Remotely client has already been started"
            });

            return Task.CompletedTask;
        }

        string remotelyServer = configuration.GetRequiredValue("RemotelyServer");
        int grpcServerPort = configuration.GetRequiredValue<int>("grpcPort");
        string path = configuration.GetRequiredValue<string>("RemotelyDesktopAppPath");

        StartLinuxDesktopApp(path, $"--mode Attended --host {remotelyServer} --grpc http://localhost:{grpcServerPort}");

        broadcaster.Broadcast(new SynchronizerState
        {
            State = State.Running,
            Message = "Remotely client has been started"
        });

        return Task.CompletedTask;
    }

    public Task StopApp(CancellationToken cancellationToken = default)
    {
        _runningProcess?.Kill();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _runningProcess?.Kill();
    }

    private void RunningProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        broadcaster.Broadcast(new SynchronizerState
        {
            Message = e.Data,
            State = State.ConnectionError
        });
    }

    private void RunningProcessOnExited(object? sender, EventArgs e)
    {
        broadcaster.Broadcast(new SynchronizerState
        {
            State = State.NotRunning,
            Message = "Remotely client has been stopped"
        });
    }


    private int StartLinuxDesktopApp(string fileName, string args)
    {
        string xdisplay = ":0";
        string xauthority = string.Empty;

        bool xResult = TryGetXAuth("Xorg", out XAuthInfo? xAuthInfo);

        if (!xResult)
        {
            // If running Wayland, this still ends up still being unusable.
            // This X server will only provide a black screen with any apps
            // launched within the display it's using, but the display won't
            // show anything being rendered by the Wayland compositor.  It's
            // better than simply crashing, though, so I'll leave it here
            // until Wayland support is added.
            xResult = TryGetXAuth("Xwayland", out xAuthInfo);
        }

        if (xResult)
        {
            xdisplay = xAuthInfo!.XDisplay;
            xauthority = xAuthInfo!.XAuthority;
        }
        else
        {
            logger.LogError("Failed to get X server auth");
        }

        string? whoString = InvokeProcessOutput("who", "")?.Trim();
        string username = "";

        if (!string.IsNullOrWhiteSpace(whoString))
        {
            try
            {
                string[] whoLines = whoString.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                string? whoLine = whoLines.FirstOrDefault(x =>
                    Regex.IsMatch(x.Split(" ", StringSplitOptions.RemoveEmptyEntries).Last(),
                        @"\(:[\d]*\)"));

                if (!string.IsNullOrWhiteSpace(whoLine))
                {
                    string[] whoSplit = whoLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    username = whoSplit[0];
                    xdisplay = whoSplit.Last().TrimStart('(').TrimEnd(')');
                    xauthority = $"/home/{username}/.Xauthority";
                    args = $"-u {username} {args}";
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while getting current X11 user");
            }
        }

        ProcessStartInfo psi = new()
        {
            FileName = fileName,
            Arguments = args
        };

        psi.Environment.Add("DISPLAY", xdisplay);
        psi.Environment.Add("XAUTHORITY", xauthority);

        logger.LogInformation(
            "Attempting to launch screen caster with username {Username}, xauthority {Authority}, display {Display}, and args {Args}",
            username,
            xauthority,
            xdisplay,
            args);

        _runningProcess = new Process
        {
            EnableRaisingEvents = true,
            StartInfo = psi
        };
        _runningProcess!.ErrorDataReceived += RunningProcessOnErrorDataReceived;
        _runningProcess!.Exited += RunningProcessOnExited;

        _runningProcess.Start();

        return _runningProcess?.Id ?? throw new InvalidOperationException("Failed to launch desktop app.");
    }

    private bool TryGetXAuth(string xServerProcess, out XAuthInfo? xAuthInfo)
    {
        try
        {
            string xdisplay = ":0";
            string xauthority = string.Empty;

            string? xprocess = InvokeProcessOutput("ps", $"-C {xServerProcess} -f")
                .Split(Environment.NewLine)
                .FirstOrDefault(x => x.Contains(" -auth "));

            if (string.IsNullOrWhiteSpace(xprocess))
            {
                logger.LogInformation("{XServerProcess} process not found", xServerProcess);
                xAuthInfo = null;

                return false;
            }

            logger.LogInformation("Resolved X server process: {Process}", xprocess);

            List<string> xprocSplit = xprocess
                .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            int xProcIndex = xprocSplit.IndexWhere(x => x.EndsWith(xServerProcess));

            if (xProcIndex > -1 && xprocSplit[xProcIndex + 1].StartsWith(':'))
            {
                xdisplay = xprocSplit[xProcIndex + 1];
            }

            int authIndex = xprocSplit.IndexOf("-auth");

            if (authIndex > -1)
            {
                xauthority = xprocSplit[authIndex + 1];
            }

            xAuthInfo = new XAuthInfo(xdisplay, xauthority);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while getting X auth for {ServerProcess}", xServerProcess);

            xAuthInfo = null;

            return false;
        }
    }

    private string InvokeProcessOutput(string command, string arguments)
    {
        try
        {
            ProcessStartInfo psi = new(command, arguments)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "RunAs",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            Process? proc = Process.Start(psi);
            proc?.WaitForExit();

            return proc?.StandardOutput.ReadToEnd() ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start process");

            return string.Empty;
        }
    }

    private record XAuthInfo(string XDisplay, string XAuthority);
}