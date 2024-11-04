using System.Runtime.InteropServices;
using Desktop.Native.Linux;
using WaylandSharp;

namespace Remotely.Desktop.Linux.Services.Models;

internal sealed class WaylandScreen(WlOutput output) : IDisposable
{
    public IntPtr Data;

    public int Width { get; set; }
    public int Height { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public string Name { get; set; } = string.Empty;

    public WlOutput Output { get; } = output;
    public WlBuffer? Buffer { get; private set; }
    public ZxdgOutputV1? ZxdgOutputV1 { get; set; }

    public void Dispose()
    {
        Output.Dispose();
        Buffer?.Dispose();
        ZxdgOutputV1?.Dispose();

        Marshal.FreeHGlobal(Data);
    }

    public bool InitializeBuffer(WlShm wlShm, int width, int height, int stride, WlShmFormat format)
    {
        // Calculate the total size of the buffer
        int size = stride * height;

        // Create an anonymous file descriptor for the shared memory buffer
        int fd = Libc.OpenAnonymousFile(size);

        if (fd < 0)
        {
            Data = IntPtr.Zero;

            return false;
        }

        // Map the shared memory buffer into the process address space
        Data = Libc.mmap(IntPtr.Zero,
            (UIntPtr)size,
            Libc.ProtectionFlags.ReadWrite,
            Libc.MMapFlags.Shared,
            fd,
            0);

        if (Data == Libc.MAP_FAILED)
        {
            Libc.close(fd); // Close the file descriptor

            return false;
        }

        // Create a Wayland shared memory pool using the file descriptor
        // Destroy the pool as it's no longer needed (the buffer owns the memory now)
        using WlShmPool pool = wlShm.CreatePool(fd, size);

        Libc.close(fd); // Close the file descriptor (not needed after creating the pool)

        // Create a Wayland buffer from the shared memory pool
        WlBuffer buffer = pool.CreateBuffer(0, width, height, stride, format);

        pool.Destroy();

        Buffer = buffer;

        return true;
    }

    public bool InitializeBuffer(WlShm wlShm, int stride, WlShmFormat format)
    {
        // Calculate the total size of the buffer
        int size = stride * Height;

        // Create an anonymous file descriptor for the shared memory buffer
        int fd = Libc.OpenAnonymousFile(size);

        if (fd < 0)
        {
            Data = IntPtr.Zero;

            return false;
        }

        // Map the shared memory buffer into the process address space
        Data = Libc.mmap(IntPtr.Zero,
            (UIntPtr)size,
            Libc.ProtectionFlags.ReadWrite,
            Libc.MMapFlags.Shared,
            fd,
            0);

        if (Data == Libc.MAP_FAILED)
        {
            Libc.close(fd); // Close the file descriptor

            return false;
        }

        // Create a Wayland shared memory pool using the file descriptor
        // Destroy the pool as it's no longer needed (the buffer owns the memory now)
        using WlShmPool pool = wlShm.CreatePool(fd, size);

        Libc.close(fd); // Close the file descriptor (not needed after creating the pool)

        // Create a Wayland buffer from the shared memory pool
        WlBuffer buffer = pool.CreateBuffer(0, Width, Height, stride, format);

        pool.Destroy();

        Buffer = buffer;

        return true;
    }
}