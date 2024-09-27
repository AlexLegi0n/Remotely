namespace Sirona.RemoteControl.Synchronizer.Services.Launchers;

public static class AppLaunchersConfiguration
{
    public static void AddAppLaunchers(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IAppLauncher, RemotelyAppLauncher>();
    }
}