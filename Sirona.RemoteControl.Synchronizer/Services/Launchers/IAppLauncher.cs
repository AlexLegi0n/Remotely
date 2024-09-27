namespace Sirona.RemoteControl.Synchronizer.Services.Launchers;

public interface IAppLauncher
{
    Task StartApp(CancellationToken cancellationToken = default);
    Task StopApp(CancellationToken cancellationToken = default);
}