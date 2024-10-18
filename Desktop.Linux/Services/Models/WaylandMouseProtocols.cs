using System.Drawing;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Enums;
using WaylandSharp;

namespace Remotely.Desktop.Linux.Services.Models;

internal sealed class WaylandMouseProtocols(ILogger logger) : WaylandProtocols
{
    private ZwlrVirtualPointerManagerV1? _pointerManager;
    private ZwlrVirtualPointerV1? _pointer;
    private WlSeat? _wlSeat;
    private WlOutput? _wlOutput;

    public override bool Bind(WlRegistry.GlobalEventArgs args, WlRegistry registry)
    {
        if (args.Interface == WlInterface.ZwlrVirtualPointerManagerV1.Name)
        {
            _pointerManager = registry.Bind<ZwlrVirtualPointerManagerV1>(args.Name, args.Interface, args.Version);
            OnBindCompleted(args);

            return true;
        }

        if (args.Interface == WlInterface.WlSeat.Name)
        {
            _wlSeat = registry.Bind<WlSeat>(args.Name, args.Interface, args.Version);
            OnBindCompleted(args);

            return true;
        }

        if (args.Interface == WlInterface.WlOutput.Name)
        {
            _wlOutput = registry.Bind<WlOutput>(args.Name, args.Interface, args.Version);
            OnBindCompleted(args);

            return true;
        }

        return false;
    }

    public void InitializePointer()
    {
        if (_wlSeat is null)
        {
            throw new ApplicationException("WlSeat is null");
        }

        if (_wlOutput is null)
            throw new ApplicationException("WlOutput is null");

        _pointer = _pointerManager!.CreateVirtualPointer(_wlSeat);

        logger.LogInformation("Pointer initialized: {Id}", _pointer.GetId().ToString());
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _pointerManager?.Dispose();
        _pointer?.Dispose();
        _wlSeat?.Dispose();
        _wlOutput?.Dispose();
    }

    public void Motion(double percentX, double percentY, Rectangle screenBounds)
    {
        if (_pointer is null)
            throw new ApplicationException("Pointer is null");

        // Calculate the absolute position from percentages
        double absX = screenBounds.X + (screenBounds.Width * percentX);
        double absY = screenBounds.Y + (screenBounds.Height * percentY);

        // Clamp the values to the screen size
        var absoluteX = (uint)Math.Clamp(absX, 0, screenBounds.Width - 1);
        var absoluteY = (uint)Math.Clamp(absY, 0, screenBounds.Height - 1);

        _pointer.MotionAbsolute(Time, absoluteX, absoluteY, (uint)screenBounds.Width, (uint)screenBounds.Height);
    }

    public void Click(int button, ButtonAction buttonAction)
    {
        if (_pointer is null)
            throw new ApplicationException("Pointer is null");

        WlPointerButtonState state = buttonAction switch
        {
            ButtonAction.Down => WlPointerButtonState.Pressed,
            ButtonAction.Up => WlPointerButtonState.Released,
            _ => throw new ArgumentOutOfRangeException(nameof(buttonAction), buttonAction, null)
        };

        uint waylandMouseButton = button switch
        {
            1 => 272, //BTN_LEFT
            2 => 273, //BTN_RIGHT
            3 => 274, //BTN_MIDDLE
            _ => 0 //Unmapped key
        };

        if (waylandMouseButton == 0)
            return;

        _pointer.Button(Time, waylandMouseButton, state);
    }

    public void Scroll(double deltaY)
    {
        if (_pointer is null)
            throw new ApplicationException("Pointer is null");

        _pointer.AxisSource(WlPointerAxisSource.Wheel);
        _pointer.Axis(Time, WlPointerAxis.VerticalScroll, deltaY);
        _pointer.Frame();
    }
}