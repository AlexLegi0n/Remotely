using WaylandSharp;

namespace Remotely.Desktop.Linux.Services.Models;

public abstract class WaylandProtocols : IDisposable
{
    public event EventHandler<WlRegistry.GlobalEventArgs>? BindCompleted;

    public abstract bool Bind(WlRegistry.GlobalEventArgs args, WlRegistry registry);

    protected void OnBindCompleted(WlRegistry.GlobalEventArgs args)
    {
        BindCompleted?.Invoke(this, args);
    }

    protected abstract void Dispose(bool disposing);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}