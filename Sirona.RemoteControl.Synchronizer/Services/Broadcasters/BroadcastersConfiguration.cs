namespace Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

public static class BroadcastersConfiguration
{
    public static void AddBroadcasters(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IStateBroadcaster, StateBroadcaster>();
        builder.Services.AddSingleton<IPermissionBroadcaster, PermissionBroadcaster>();
    } 
}