// using WaylandSharp;
//
// namespace Remotely.Desktop.Linux.Services.Models;
//
// internal sealed class WaylandMouseProtocols : WaylandProtocols
// {
//     private ZwlrVirtualPointerManagerV1? _pointerManager;
//     private ZwlrVirtualPointerV1? _pointer;
//
//     public ZwlrVirtualPointerManagerV1 ZwlrVirtualPointerV1 => _pointerManager ??
//                                                                throw new NullReferenceException(
//                                                                    $"{WlInterface.ZwlrVirtualPointerV1.Name} interface has not been registered");
//
//     public override bool Bind(WlRegistry.GlobalEventArgs args, WlRegistry registry)
//     {
//         if (args.Interface == WlInterface.ZwlrVirtualPointerManagerV1.Name)
//         {
//             _pointerManager = registry.Bind<ZwlrVirtualPointerManagerV1>(args.Name, args.Interface, args.Version);
//             _pointer = _pointerManager!.CreateVirtualPointer(null!);
//
//             
//             OnBindCompleted(args);
//
//             return true;
//         }
//
//         return false;
//     }
//
//     protected override void Dispose(bool disposing)
//     {
//         if (!disposing)
//         {
//             return;
//         }
//
//         _pointerManager?.Dispose();
//         _pointer?.Dispose();
//     }
//
//     public void Motion(double x, double y)
//     {
//         _pointer?.Motion(1, x, y);
//     }
// }