using WaylandSharp;

namespace Remotely.Desktop.Linux.Services.Models;

internal class ScreenshotDoneEventArgs
{
    public bool ScreenshotDone { get; init; }
}

internal class ScreenshotFailedEventArgs
{
    public string Message { get; init; } = null!;
}

internal sealed class WaylandScreenProtocols : WaylandProtocols
{
    private ZwlrScreencopyFrameV1? _frame;
    private List<WlOutput>? _wlOutputs;
    private WlShm? _wlShm;
    private ZxdgOutputManagerV1? _zxdgOutputManagerV1;

    public WlShm WlShm =>
        _wlShm ??
        throw new NullReferenceException($"{WlInterface.WlShm.Name} interface has not been registered");

    public ZxdgOutputManagerV1 ZxdgOutputManagerV1 =>
        _zxdgOutputManagerV1 ??
        throw new NullReferenceException($"{WlInterface.ZxdgOutputManagerV1.Name} interface has not been registered");

    public WestonScreenshooter? WestonScreenshooter { get; private set; }
    public ZwlrScreencopyManagerV1? ZwlrScreencopyManagerV1 { get; private set; }
    public IReadOnlyCollection<WlOutput> WlOutputs => _wlOutputs ?? [];
    public bool HasScreenCastingProtocol => WestonScreenshooter is not null || ZwlrScreencopyManagerV1 is not null;
    
    public string GetScreenCastingProtocol()=> WestonScreenshooter is not null ? WlInterface.WestonScreenshooter.Name : WlInterface.ZwlrScreencopyManagerV1.Name;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _wlShm?.Dispose();
            WestonScreenshooter?.Dispose();
            _wlOutputs?.ForEach(x => x.Dispose());
            ZwlrScreencopyManagerV1?.Dispose();
            _zxdgOutputManagerV1?.Dispose();
        }
    }

    public event EventHandler<ScreenshotDoneEventArgs>? ScreenshotDone;
    public event EventHandler<ScreenshotFailedEventArgs>? ScreenshotFailed;

    public override bool Bind(WlRegistry.GlobalEventArgs args, WlRegistry registry)
    {
        if (args.Interface == WlInterface.WestonScreenshooter.Name)
        {
            WestonScreenshooter = registry.Bind<WestonScreenshooter>(args.Name, args.Interface, args.Version);
            WestonScreenshooter.Done += (_, _) =>
            {
                ScreenshotDone?.Invoke(this, new ScreenshotDoneEventArgs { ScreenshotDone = true });
            };

            OnBindCompleted(args);

            return true;
        }

        if (args.Interface == WlInterface.ZxdgOutputManagerV1.Name)
        {
            _zxdgOutputManagerV1 = registry.Bind<ZxdgOutputManagerV1>(args.Name, args.Interface, args.Version);
            OnBindCompleted(args);

            return true;
        }

        if (args.Interface == WlInterface.WlShm.Name)
        {
            _wlShm = registry.Bind<WlShm>(args.Name, args.Interface, args.Version);
            OnBindCompleted(args);

            return true;
        }

        if (args.Interface == WlInterface.WlOutput.Name)
        {
            WlOutput wlOutput = registry.Bind<WlOutput>(args.Name, args.Interface, args.Version);

            _wlOutputs ??= [];
            _wlOutputs.Add(wlOutput);

            OnBindCompleted(args);

            return true;
        }

        if (args.Interface == WlInterface.ZwlrScreencopyManagerV1.Name)
        {
            ZwlrScreencopyManagerV1 = registry.Bind<ZwlrScreencopyManagerV1>(args.Name, args.Interface, args.Version);
            OnBindCompleted(args);

            return true;
        }

        return false;
    }

    public void TakeScreenShot(WaylandScreen screen)
    {
        if (WestonScreenshooter != null)
        {
            int stride = screen.Width * 4;

            if (!screen.InitializeBuffer(WlShm, stride, WlShmFormat.Xrgb8888))
            {
                ScreenshotFailed?.Invoke(this, new ScreenshotFailedEventArgs()
                {
                    Message = $"Failed to create buffer: {screen.Width}x{screen.Height}. Stride: {stride}. Format: {WlShmFormat.Xrgb8888}"
                });
            }


            WestonScreenshooter!.TakeShot(screen.Output, screen.Buffer!);
            
            return;
        }

        _frame?.Destroy();
        _frame = ZwlrScreencopyManagerV1!.CaptureOutput(1, screen.Output);

        _frame.Ready += (_, _) => ScreenshotDone?.Invoke(this, new ScreenshotDoneEventArgs { ScreenshotDone = true });
        _frame.Buffer += (_, args) =>
        {
            if (!screen.InitializeBuffer(WlShm, (int)args.Width,
                    (int)args.Height,
                    (int)args.Stride,
                    args.Format))
            {
                ScreenshotFailed?.Invoke(this, new ScreenshotFailedEventArgs()
                {
                    Message = $"Failed to create buffer: {args.Width}x{args.Height}. Stride: {args.Stride}. Format: {args.Format}"
                });
            }

            _frame.Copy(screen.Buffer!);
        };
    }
}