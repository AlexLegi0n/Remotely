using System.Drawing;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Linux.Services.Models;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Services;
using Remotely.Shared.Primitives;
using SkiaSharp;
using WaylandSharp;

namespace Remotely.Desktop.Linux.Services;

internal sealed class WaylandScreenCapturerLinux : IScreenCapturer
{
    private readonly IImageHelper _imageHelper;
    private readonly ILogger<WaylandScreenCapturerLinux> _logger;

    private readonly object _screenBoundsLock = new();
    private readonly WaylandScreenProtocols _screenProtocols;
    private readonly List<WaylandScreen> _screens = [];
    private readonly WlDisplay _wlDisplay;

    private SKBitmap? _currentFrame;

    private WaylandScreen? _currentScreen;
    private SKBitmap? _previousFrame;

    private bool _screenshotDone;
    private bool _screenshotFailed;

    public WaylandScreenCapturerLinux(ILogger<WaylandScreenCapturerLinux> logger, IImageHelper imageHelper)
    {
        logger.LogDebug("Using [{ScreenCaptureComponent}] - Version 1.13.2", nameof(WaylandScreenCapturerLinux));

        _logger = logger;
        _imageHelper = imageHelper;
        _screenProtocols = new WaylandScreenProtocols();

        _screenProtocols.BindCompleted += (_, args) =>
            _logger.LogDebug("Binding wayland protocol: [{Protocol}] {Version}", args.Interface, args.Version);
        _screenProtocols.ScreenshotDone += (_, args) =>
        {
            _screenshotDone = args.ScreenshotDone;
            _screenshotFailed = false;
        };
        _screenProtocols.ScreenshotFailed += (_, args) =>
        {
            _logger.LogWarning("Screenshot failed: {Error}", args.Message);
            _screenshotFailed = true;
        };

        string waylandServer = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/run/user/1000";
        string displayName = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "wayland-1";
        string wlshPath = $"{waylandServer}/{displayName}";

        _logger.LogDebug("Connecting to wayland display. Wayland connection path: {Path}", wlshPath);

        _wlDisplay = WlDisplay.Connect(wlshPath);
        Init();
    }


    public event EventHandler<Rectangle>? ScreenChanged;
    public bool CaptureFullscreen { get; set; } = true;
    public Rectangle CurrentScreenBounds { get; private set; }
    public bool IsGpuAccelerated => false;
    public string SelectedScreen => _currentScreen?.Name ?? String.Empty;


    public void Dispose()
    {
        _logger.LogDebug("Disposing WaylandScreenCapturerLinux");

        _wlDisplay.Dispose();
        _screenProtocols.Dispose();
        _currentFrame?.Dispose();
        _previousFrame?.Dispose();
        _currentScreen?.Dispose();

        foreach (WaylandScreen output in _screens)
        {
            output.Dispose();
        }
    }

    public IEnumerable<string> GetDisplayNames()
    {
        return _screens.Select(x => x.Name);
    }

    public SKRect GetFrameDiffArea()
    {
        lock (_screenBoundsLock)
        {
            return _currentFrame is null
                ? SKRect.Empty
                : _imageHelper.GetDiffArea(_currentFrame, _previousFrame, CaptureFullscreen);
        }
    }

    public Result<SKBitmap> GetImageDiff()
    {
        lock (_screenBoundsLock)
        {
            return _currentFrame is null
                ? Result.Fail<SKBitmap>("Current frame is null.")
                : _imageHelper.GetImageDiff(_currentFrame, _previousFrame);
        }
    }

    public Result<SKBitmap> GetNextFrame()
    {
        lock (_screenBoundsLock)
        {
            try
            {
                if (_currentFrame != null)
                {
                    _previousFrame?.Dispose();
                    _previousFrame = _currentFrame;

                    _logger.LogDebug("Previous frame changed");
                }

                _currentFrame = GetWaylandCapture();

                return Result.Ok(_currentFrame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting next frame");

                Init();

                return Result.Fail<SKBitmap>(ex);
            }
        }
    }

    public int GetScreenCount()
    {
        return _screens.Count;
    }

    public Rectangle GetVirtualScreenBounds()
    {
        int lowestX = 0;
        int highestX = 0;
        int lowestY = 0;
        int highestY = 0;

        foreach (WaylandScreen output in _screens)
        {
            lowestX = Math.Min(lowestX, output.OffsetX);
            highestX = Math.Max(highestX, output.OffsetX + output.Width);
            lowestY = Math.Min(lowestY, output.OffsetY);
            highestY = Math.Max(highestY, output.OffsetY + output.Height);
        }


        Rectangle virtualScreenBounds = new(lowestX, lowestY, highestX - lowestX, highestY - lowestY);

        _logger.LogDebug("Received virtual screen bounds: {@Rectangles}", virtualScreenBounds);

        return virtualScreenBounds;
    }

    public void Init()
    {
        try
        {
            CaptureFullscreen = true;
            CleanOutput();

            using WlRegistry registry = _wlDisplay.GetRegistry();

            _logger.LogDebug("Wayland registry initialized");

            registry.Global += RegistryOnGlobal;
            _wlDisplay.Roundtrip();

            if (!_screenProtocols.HasScreenCastingProtocol)
            {
                throw new ArgumentException("No suitable protocol found for screen casting");
            }

            _logger.LogDebug("Screen casting protocol: [{Protocol}]", _screenProtocols.GetScreenCastingProtocol());

            IEnumerable<WaylandScreen> outputs = _screenProtocols.WlOutputs.Select(x => new WaylandScreen(x));
            _screens.AddRange(outputs);

            _logger.LogDebug("Found {Count} screens", _screens.Count);

            foreach (WaylandScreen screen in _screens)
            {
                InitializeScreen(screen);
            }

            _wlDisplay.Roundtrip();

            if (!string.IsNullOrWhiteSpace(SelectedScreen) && _screens.Any(x => x.Name == SelectedScreen))
            {
                return;
            }

            _currentScreen = _screens.First();
            RefreshCurrentScreenBounds();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while initializing");
        }
    }

    public void SetSelectedScreen(string displayName)
    {
        lock (_screenBoundsLock)
        {
            try
            {
                _logger.LogInformation("Setting display to {DisplayName}", displayName);

                if (displayName == SelectedScreen)
                {
                    return;
                }

                _currentScreen = _screens.FirstOrDefault(x => x.Name == displayName) ?? _screens.First();

                RefreshCurrentScreenBounds();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while setting selected display");
            }
        }
    }

    private void InitializeScreen(WaylandScreen screen)
    {
        string outputId = screen.Output.GetId().ToString();
        _logger.LogDebug("Initializing screen: {Id}", outputId);

        screen.ZxdgOutputV1 = _screenProtocols.ZxdgOutputManagerV1.GetXdgOutput(screen.Output);

        _logger.LogDebug("XdgOutputV1 initialized for output: {Id}", outputId);

        screen.ZxdgOutputV1.Name += (sender, args) =>
        {
            _logger.LogDebug("On name output: {Name}", args.Name);

            ZxdgOutputV1 xdgOutputSender = (ZxdgOutputV1)sender!;

            if (screen.ZxdgOutputV1.GetId() != xdgOutputSender.GetId())
            {
                return;
            }

            screen.Name = args.Name;
        };

        screen.Output.Geometry += (sender, args) =>
        {
            WlOutput? outputSender = sender as WlOutput;

            _logger.LogDebug("On Geometry output: {X}x{Y}x{Width}x{Height}", args.X, args.Y, args.PhysicalWidth,
                args.PhysicalHeight);

            if (screen.Output.GetId() != outputSender?.GetId())
            {
                return;
            }

            screen.OffsetX = args.X;
            screen.OffsetY = args.Y;
        };
        screen.Output.Mode += (sender, args) =>
        {
            WlOutput? outputSender = sender as WlOutput;

            _logger.LogDebug("On Mode output: {Width}x{Height}", args.Width, args.Height);

            if (screen.Output.GetId() != outputSender?.GetId() || args.Flags == 0)
            {
                return;
            }

            screen.Width = args.Width;
            screen.Height = args.Height;
        };
    }

    private void CleanOutput()
    {
        _logger.LogDebug("Cleaning outputs");

        foreach (WaylandScreen output in _screens)
        {
            output.Dispose();
        }

        _screens.Clear();
    }

    private void RegistryOnGlobal(object? sender, WlRegistry.GlobalEventArgs e)
    {
        WlRegistry registry = (WlRegistry)sender!;

        _logger.LogTrace("Found wayland protocol: {InterfaceName}", e.Interface);

        _screenProtocols.Bind(e, registry);
    }

    private void RefreshCurrentScreenBounds()
    {
        WaylandScreen screen = _screens.First(x => x.Name == SelectedScreen);

        _logger.LogDebug("Setting new screen bounds: {Width}, {Height}, {OffsetX}, {OffsetY}",
            screen.Width,
            screen.Height,
            screen.OffsetY,
            screen.OffsetY);

        CurrentScreenBounds = new Rectangle(screen.OffsetX, screen.OffsetY, screen.Width, screen.Height);
        CaptureFullscreen = true;

        ScreenChanged?.Invoke(this, CurrentScreenBounds);
    }

    private SKBitmap GetWaylandCapture()
    {
        _logger.LogDebug("Getting capture from current screen");

        SKBitmap bitmap = new();

        lock (_screenBoundsLock)
        {
            if (_currentScreen is null)
            {
                return bitmap;
            }

            _screenProtocols.TakeScreenShot(_currentScreen);

            bool done = _screenshotDone = false;

            while (!done && _wlDisplay.Dispatch() != -1)
            {
                _logger.LogDebug("Dispatching events. Screenshot done: {Done}", _screenshotDone);

                done = _screenshotDone;

                if (_screenshotFailed)
                    return _currentFrame!;
            }
        }

        try
        {
            _logger.LogDebug("Creating bitmap from a wl buffer");

            // Create an SKImageInfo object with the width, height, and pixel format of the image
            SKImageInfo imageInfo = new(CurrentScreenBounds.Width,
                CurrentScreenBounds.Height,
                SKColorType.Rgb888x,
                SKAlphaType.Opaque);

            // Create an SKPixmap object that references the pixel data
            using SKPixmap pixmap = new(imageInfo, _currentScreen.Data);

            // Create an SkBitmap from the SKPixmap
            bitmap.InstallPixels(pixmap);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error during creating bitmap. {Message}", e.Message);

            throw;
        }

        _logger.LogDebug("Screenshot completed successfully");

        return bitmap;
    }
}