namespace Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

public interface IBroadcaster<TItem>
{
    IObservable<TItem> Subscribe();

    void Broadcast(TItem item);
}