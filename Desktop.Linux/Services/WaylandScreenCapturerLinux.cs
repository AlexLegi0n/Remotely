using System.Drawing;
using System.Runtime.InteropServices;
using Desktop.Native.Linux;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Extensions;
using Remotely.Shared.Primitives;
using SkiaSharp;
using WaylandSharp;

namespace Remotely.Desktop.Linux.Services;

internal sealed class WaylandScreen(WlOutput output) : IDisposable
{
    public IntPtr Data;

    public int Width { get; set; }
    public int Height { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public string Name { get; set; } = string.Empty;

    public WlOutput Output { get; } = output;
    public WlBuffer? Buffer { get; set; }
    public ZxdgOutputV1? ZxdgOutputV1 { get; set; }

    public void Dispose()
    {
        Output.Dispose();
        Buffer?.Dispose();
        ZxdgOutputV1?.Dispose();

        Marshal.FreeHGlobal(Data);
    }
}

internal sealed class WaylandScreenCapturerLinux : IScreenCapturer
{
    private readonly ILogger<WaylandScreenCapturerLinux> _logger;
    private readonly object _screenBoundsLock = new();

    private readonly List<WaylandScreen> _screens = [];

    private readonly WlDisplay _wlDisplay;

    private SKBitmap? _currentFrame;

    private WaylandScreen? _currentScreen;
    private ZxdgOutputManagerV1? _outputManager;
    private SKBitmap? _previousFrame;

    private bool _screenshotDone;
    private WestonScreenshooter? _westonScreenshooter;
    private WlShm? _wlShm;
    private ZwlrScreencopyManagerV1? _zwlrScreencopyManager;

    public WaylandScreenCapturerLinux(ILogger<WaylandScreenCapturerLinux> logger)
    {
        _logger = logger;
        string waylandServer = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/run/user/1000";
        string displayName = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "wayland-1";
        string wlshPath = $"{waylandServer}/{displayName}";

        _logger.LogDebug("Wayland connection path: {Path}", wlshPath);

        _wlDisplay = WlDisplay.Connect(wlshPath);
    }

    public void Dispose()
    {
        _wlDisplay.Dispose();
        _westonScreenshooter?.Dispose();
        _zwlrScreencopyManager?.Dispose();

        foreach (WaylandScreen output in _screens)
        {
            output.Dispose();
        }
    }

    public event EventHandler<Rectangle>? ScreenChanged;
    public bool CaptureFullscreen { get; set; } = true;
    public Rectangle CurrentScreenBounds { get; private set; }
    public bool IsGpuAccelerated => false;
    public string SelectedScreen => _currentScreen?.Name ?? String.Empty;


    public IEnumerable<string> GetDisplayNames()
    {
        return _screens.Select(x => x.Name);
    }

    public SKRect GetFrameDiffArea()
    {
        lock (_screenBoundsLock)
        {
            return _currentFrame?.ToRectangle() ?? SKRect.Empty;
        }
    }

    public Result<SKBitmap> GetImageDiff()
    {
        lock (_screenBoundsLock)
        {
            return _currentFrame is not null
                ? Result.Ok(_currentFrame.Copy())
                : Result.Fail<SKBitmap>("Current frame is null.");
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
            registry.Global += RegistryOnGlobal;
            _wlDisplay.Roundtrip();

            _logger.LogInformation("Screens count: {Count}", _screens.Count);

            if (_westonScreenshooter == null)
            {
                _logger.LogWarning("{Protocol} interface has not been found. It will be disabled",
                    WlInterface.WestonScreenshooter.Name);

                if (_zwlrScreencopyManager == null)
                {
                    _logger.LogWarning("{Protocol} interface has not been found. It will be disabled",
                        WlInterface.ZwlrScreencopyManagerV1.Name);

                    throw new ArgumentException("No suitable protocol found for screen casting");
                }

                _logger.LogDebug("{Protocol} interface has been found. It will be enabled for screen casting",
                    WlInterface.ZwlrScreencopyManagerV1.Name);
            }
            else
            {
                _logger.LogDebug("{Protocol} interface has been found. It will be enabled for screen casting",
                    WlInterface.WestonScreenshooter.Name);
            }

            if (_wlShm == null)
            {
                throw new ArgumentNullException(nameof(_westonScreenshooter), "wl_shm interface has not been found");
            }

            if (_outputManager == null)
            {
                throw new ArgumentNullException(nameof(_outputManager), "zxdg_output_manager_v1 interface has not been found");
            }

            if (_westonScreenshooter != null)
            {
                _westonScreenshooter.Done += WestonScreenshooterOnDone;
            }

            foreach (WaylandScreen screen in _screens)
            {
                InitializeScreens(screen);
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

    private void InitializeScreens(WaylandScreen screen)
    {
        screen.ZxdgOutputV1 = _outputManager!.GetXdgOutput(screen.Output);

        screen.ZxdgOutputV1.Name += (sender, args) =>
        {
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
        foreach (WaylandScreen output in _screens)
        {
            output.Dispose();
        }

        _screens.Clear();
    }

    private WlBuffer? CreateBuffer(int outputWidth, int outputHeight, out IntPtr data)
    {
        // Calculate the stride (number of bytes per row) assuming 32-bit RGBA format (4 bytes per pixel)
        int stride = outputWidth * 4;
        // Calculate the total size of the buffer
        int size = stride * outputHeight;

        // Create an anonymous file descriptor for the shared memory buffer
        int fd = Libc.OpenAnonymousFile(size);

        if (fd < 0)
        {
            _logger.LogWarning("Creating a buffer file for {Size} B failed: {Error}",
                size,
                Marshal.GetLastWin32Error());
            data = IntPtr.Zero;

            return null;
        }

        // Map the shared memory buffer into the process address space
        data = Libc.mmap(IntPtr.Zero,
            (UIntPtr)size,
            Libc.ProtectionFlags.ReadWrite,
            Libc.MMapFlags.Shared,
            fd,
            0);

        if (data == Libc.MAP_FAILED)
        {
            _logger.LogWarning("mmap failed: {Error}", Marshal.GetLastWin32Error());

            Libc.close(fd); // Close the file descriptor

            return null;
        }

        // Create a Wayland shared memory pool using the file descriptor
        // Destroy the pool as it's no longer needed (the buffer owns the memory now)
        using WlShmPool pool = _wlShm!.CreatePool(fd, size);

        Libc.close(fd); // Close the file descriptor (not needed after creating the pool)

        // Create a Wayland buffer from the shared memory pool
        WlBuffer buffer = pool.CreateBuffer(0, outputWidth, outputHeight, stride, WlShmFormat.Xrgb8888);

        pool.Destroy();

        return buffer;
    }

    private void RegistryOnGlobal(object? sender, WlRegistry.GlobalEventArgs e)
    {
        WlRegistry registry = (WlRegistry)sender!;

        _logger.LogDebug("Found: [{Interface}] [{Version}]", e.Interface, e.Version);

        if (e.Interface == WlInterface.WestonScreenshooter.Name)
        {
            _logger.LogInformation("Binding {Interface} {Version}", e.Interface, e.Version);

            _westonScreenshooter = registry.Bind<WestonScreenshooter>(e.Name, e.Interface, e.Version);

            return;
        }

        if (e.Interface == WlInterface.ZxdgOutputManagerV1.Name)
        {
            _logger.LogInformation("Binding {Interface} {Version}", e.Interface, e.Version);

            _outputManager = registry.Bind<ZxdgOutputManagerV1>(e.Name, e.Interface, e.Version);

            return;
        }

        if (e.Interface == WlInterface.WlShm.Name)
        {
            _logger.LogInformation("Binding {Interface} {Version}", e.Interface, e.Version);

            _wlShm = registry.Bind<WlShm>(e.Name, e.Interface, e.Version);

            return;
        }

        if (e.Interface == WlInterface.WlOutput.Name)
        {
            _logger.LogInformation("Binding {Interface} {Version}", e.Interface, e.Version);

            WlOutput wlOutput = registry.Bind<WlOutput>(e.Name, e.Interface, e.Version);

            WaylandScreen output = new(wlOutput);
            _screens.Add(output);

            return;
        }

        if (e.Interface == WlInterface.ZwlrScreencopyManagerV1.Name)
        {
            _logger.LogInformation("Binding {Interface} {Version}", e.Interface, e.Version);

            _zwlrScreencopyManager = registry.Bind<ZwlrScreencopyManagerV1>(e.Name, e.Interface, e.Version);
        }
    }

    private void WestonScreenshooterOnDone(object? sender, WestonScreenshooter.DoneEventArgs e)
    {
        _screenshotDone = true;
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
        lock (_screenBoundsLock)
        {
            _currentScreen!.Buffer ??= CreateBuffer(
                _currentScreen.Width,
                _currentScreen.Height,
                out _currentScreen.Data);

            if (_currentScreen.Buffer == null)
            {
                _logger.LogWarning("Buffer not created. Returning current frame...");

                return _currentFrame!;
            }

            if (_westonScreenshooter != null)
            {
                _westonScreenshooter!.TakeShot(_currentScreen.Output, _currentScreen.Buffer!);
            }
            else
            {
                ZwlrScreencopyFrameV1 frame = _zwlrScreencopyManager!.CaptureOutputRegion(0, _currentScreen.Output,
                    _currentScreen.OffsetX, _currentScreen.OffsetY, _currentScreen.Width, _currentScreen.Height);

                _logger.LogDebug("Frame captured: {Id}", frame.GetId());
                
                frame.BufferDone += FrameOnBufferDone;
                frame.Ready += FrameOnReady;
                frame.Copy(_currentScreen.Buffer);
            }

            bool done = _screenshotDone = false;

            while (!done && _wlDisplay.Dispatch() != -1)
            {
                done = _screenshotDone;
            }
        }

        SKBitmap bitmap = new();

        try
        {
            // Create an SKImageInfo object with the width, height, and pixel format of the image
            SKImageInfo imageInfo = new(CurrentScreenBounds.Width,
                CurrentScreenBounds.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);

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

        return bitmap;
    }

    private void FrameOnReady(object? sender, ZwlrScreencopyFrameV1.ReadyEventArgs e)
    {
        _logger.LogDebug("On Frame ready with {@Args}", e);
    }

    private void FrameOnBufferDone(object? sender, ZwlrScreencopyFrameV1.BufferDoneEventArgs e)
    {
        _logger.LogDebug("On Buffer Done with {@Args}", e);
        
        _screenshotDone = true;
    }
}