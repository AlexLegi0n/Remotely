using System.Drawing;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Linux.Services.Models;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Enums;
using Remotely.Desktop.Shared.Services;
using WaylandSharp;

namespace Remotely.Desktop.Linux.Services;

internal class WaylandKeyboardMouseInputLinux : IKeyboardMouseInput, IDisposable
{
    private readonly WaylandKeyboardProtocols _keyboardProtocols;
    private readonly ILogger<WaylandKeyboardMouseInputLinux> _logger;
    private readonly WaylandMouseProtocols _mouseProtocols;

    private readonly WlDisplay _wlDisplay;

    public WaylandKeyboardMouseInputLinux(ILogger<WaylandKeyboardMouseInputLinux> logger)
    {
        _logger = logger;

        _logger.LogDebug("Using [{KeyboardMouseComponent}] - Version 1.13.51", nameof(WaylandKeyboardMouseInputLinux));

        string waylandServer = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/run/user/1000";
        string displayName = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "wayland-1";
        string wlShPath = $"{waylandServer}/{displayName}";

        _logger.LogDebug("Connecting to wayland display. Wayland connection path: {Path}", wlShPath);

        _wlDisplay = WlDisplay.Connect(wlShPath);

        _keyboardProtocols = new WaylandKeyboardProtocols();
        _keyboardProtocols.KeymapChanged += (_, _) =>
        {
            _logger.LogDebug("Keymap changed");
            _wlDisplay.Roundtrip();
        };
        _keyboardProtocols.BindCompleted += (_, args) =>
            _logger.LogDebug("Binding wayland protocol: [{Protocol}] {Version}", args.Interface, args.Version);

        _mouseProtocols = new WaylandMouseProtocols(_logger);
        _mouseProtocols.BindCompleted += (_, args) =>
            _logger.LogDebug("Binding wayland protocol: [{Protocol}] {Version}", args.Interface, args.Version);
    }

    public void Dispose()
    {
        _keyboardProtocols.Dispose();
        _mouseProtocols.Dispose();
        _wlDisplay.Dispose();
    }

    public void Init()
    {
        using WlRegistry registry = _wlDisplay.GetRegistry();
        registry.Global += RegistryOnGlobal;
        _wlDisplay.Roundtrip();

        _keyboardProtocols.InitializeKeyboard();
        _mouseProtocols.InitializePointer();
        _wlDisplay.Roundtrip();
    }

    public void SendKeyDown(string key)
    {
        try
        {
            _logger.LogDebug("Sending key event: [{Key}]. Pressed: {Pressed}", key, true);

            uint mapped = _keyboardProtocols.SendKey(key, true);
            _wlDisplay.Roundtrip();

            _logger.LogDebug("Sending key event: [{Key}] with mapped code: [{Code}]. Pressed: {Pressed}", key,
                mapped, true);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to send key event down");
        }
    }

    public void SendKeyUp(string key)
    {
        try
        {
            _logger.LogDebug("Sending key event: [{Key}]. Pressed: {Pressed}", key, false);

            uint mapped = _keyboardProtocols.SendKey(key, false);
            _wlDisplay.Roundtrip();

            _logger.LogDebug("Sending key event: [{Key}] with mapped code: [{Code}]. Pressed: {Pressed}", key,
                mapped, false);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to send key event up");
        }
    }

    public void SendMouseMove(double percentX, double percentY, IViewer viewer)
    {
        try
        {
            _logger.LogTrace("SendMouseMove: [{X}] [{Y}]", percentX, percentY);

            Rectangle screenBounds = viewer.Capturer.CurrentScreenBounds;

            _mouseProtocols.Motion(percentX, percentY, screenBounds);
            _wlDisplay.Roundtrip();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to send mouse move");
        }
    }

    public void SendMouseWheel(int deltaY)
    {
        try
        {
            _logger.LogDebug("SendMouseScroll: [{X}]", deltaY);

            _mouseProtocols.Scroll(deltaY);
            _wlDisplay.Roundtrip();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to send mouse wheel");
        }
    }

    public void SendText(string transferText)
    {
        foreach (char key in transferText)
        {
            SendKeyDown(key.ToString());
            SendKeyUp(key.ToString());
        }
    }

    public void ToggleBlockInput(bool toggleOn)
    {
        // Not implemented.
    }

    public void SetKeyStatesUp()
    {
        // Not implemented.
    }

    public void SendMouseButtonAction(int button, ButtonAction buttonAction, double percentX, double percentY,
        IViewer viewer)
    {
        try
        {
            int mouseButton = button + 1;

            _logger.LogDebug("Sending mouse button {Button}, state {Action}", mouseButton, buttonAction);

            _mouseProtocols.Click(mouseButton, buttonAction);
            _wlDisplay.Roundtrip();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send mouse button  {Button} with action {Action}", button + 1, buttonAction);
        }
    }

    private void RegistryOnGlobal(object? sender, WlRegistry.GlobalEventArgs e)
    {
        try
        {
            WlRegistry registry = (WlRegistry)sender!;

            _keyboardProtocols.Bind(e, registry);
            _mouseProtocols.Bind(e, registry);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to register wayland protocol");
            throw;
        }
    }
}