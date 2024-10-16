using System.Drawing;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Linux.Services.Models;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Enums;
using Remotely.Desktop.Shared.Services;
using WaylandSharp;

namespace Remotely.Desktop.Linux.Services;

internal class WaylandKeyboardMouseInputLinux : IKeyboardMouseInput
{
    private readonly WaylandKeyboardProtocols _keyboardProtocols;
    private readonly ILogger<WaylandKeyboardMouseInputLinux> _logger;

    private readonly WlDisplay _wlDisplay;

    public WaylandKeyboardMouseInputLinux(ILogger<WaylandKeyboardMouseInputLinux> logger)
    {
        logger.LogDebug("WaylandKeyboardMouseInputLinux - Version 1.13.28");

        _logger = logger;

        string waylandServer = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/run/user/1000";
        string displayName = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "wayland-1";
        string wlShPath = $"{waylandServer}/{displayName}";

        _logger.LogDebug("Connecting to wayland display. Wayland connection path: {Path}", wlShPath);

        _wlDisplay = WlDisplay.Connect(wlShPath);

        _keyboardProtocols = new WaylandKeyboardProtocols(_logger);

        _keyboardProtocols.KeymapChanged += (_, _) =>
        {
            _logger.LogDebug("Keymap changed");
            
            _wlDisplay.Roundtrip();
        };
        _keyboardProtocols.BindCompleted += (_, args) =>
            _logger.LogDebug("Binding wayland protocol: [{Protocol}] {Version}", args.Interface, args.Version);
    }

    public void Init()
    {
        using WlRegistry registry = _wlDisplay.GetRegistry();
        registry.Global += RegistryOnGlobal;
        _wlDisplay.Roundtrip();

        _keyboardProtocols.InitializeKeyboard();
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
        Rectangle screenBounds = viewer.Capturer.CurrentScreenBounds;
        int x = screenBounds.X + (int)(screenBounds.Width * percentX);
        int y = screenBounds.Y + (int)(screenBounds.Height * percentY);


        // _mouseProtocols.Motion(x, y);
    }

    public void SendMouseWheel(int deltaY)
    {
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
    }

    private void RegistryOnGlobal(object? sender, WlRegistry.GlobalEventArgs e)
    {
        try
        {
            WlRegistry registry = (WlRegistry)sender!;

            _keyboardProtocols.Bind(e, registry);
            // _mouseProtocols.Bind(e, registry);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to register wayland protocol");
            throw;
        }
    }
}