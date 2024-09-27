using System.Reactive.Linq;

namespace Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

public interface IStateBroadcaster : IBroadcaster<SynchronizerState>;

internal sealed class StateBroadcaster : IStateBroadcaster
{
    public IObservable<SynchronizerState> Subscribe() => Observable.FromEvent<SynchronizerState>(x => Received += x, x => Received -= x);

    public void Broadcast(SynchronizerState item)
    {
        Received?.Invoke(item);
    }

    private event Action<SynchronizerState>? Received;
}