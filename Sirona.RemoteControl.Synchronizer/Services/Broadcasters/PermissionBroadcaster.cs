using System.Reactive.Linq;

namespace Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

public interface IPermissionBroadcaster : IBroadcaster<SessionPermissions>;

internal sealed class PermissionBroadcaster : IPermissionBroadcaster
{
    public void Broadcast(SessionPermissions item)
    {
        Received?.Invoke(item);
    }

    IObservable<SessionPermissions> IBroadcaster<SessionPermissions>.Subscribe()
    {
        return Observable.FromEvent<SessionPermissions>(x => Received += x, x => Received -= x);
    }

    private event Action<SessionPermissions>? Received;
}