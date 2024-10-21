using System.Runtime.InteropServices;

namespace Desktop.Native.Linux;

public class Libc
{
  public const int PROT_READ = 0x1;
    public const int PROT_WRITE = 0x2;
    public const int O_NONBLOCK = 4;

    public const int O_CREAT = 0x40;
    public const int O_RDWR = 0x2;

    public static IntPtr MAP_FAILED = new IntPtr(-1);
    
    [DllImport("libc", SetLastError = true)]
    public static extern uint geteuid();

    [DllImport("libc", SetLastError = true)]
    public static extern int open(string pathname, int flags);

    [DllImport("libc", SetLastError = true)]
    public static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    public static extern IntPtr mmap(IntPtr addr,
        UIntPtr length,
        ProtectionFlags prot,
        MMapFlags flags,
        int fd,
        long offset);

    [DllImport("libc", SetLastError = true)]
    public static extern int munmap(IntPtr addr, UIntPtr length);

    [DllImport("libc", SetLastError = true)]
    public static extern int mprotect(IntPtr addr, UIntPtr len, ProtectionFlags prot);

    [DllImport("libc", SetLastError = true)]
    public static extern int shm_open(string name, int oflag, int mode);

    [DllImport("libc", SetLastError = true)]
    public static extern int shm_unlink(string name);

    [DllImport("libc", SetLastError = true)]
    public static extern IntPtr shmat(int shmid, IntPtr shmaddr, int shmflg);

    [DllImport("libc", SetLastError = true)]
    public static extern int shmdt(IntPtr shmaddr);
    
    public static int OpenAnonymousFile(int size)
    {
        string template = "/tmp/wayland_shm_XXXXXX";
        int fd = mkstemp(template);
        if (fd < 0)
        {
            Console.WriteLine($"Creating an anonymous file failed: {Marshal.GetLastWin32Error()}");
            return -1;
        }

        if (ftruncate(fd, size) < 0)
        {
            Console.WriteLine($"Truncating file failed: {Marshal.GetLastWin32Error()}");
            close(fd);
            return -1;
        }

        return fd;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int mkstemp(string template);

    [DllImport("libc", SetLastError = true)]
    private static extern int ftruncate(int fd, int length);
    
    [Flags]
    public enum ProtectionFlags : int
    {
        None = 0,
        Read = PROT_READ,
        Write = PROT_WRITE,
        ReadWrite = Read | Write,
        NonBlock = O_NONBLOCK
    }

    [Flags]
    public enum MMapFlags
    {
        None = 0,
        Shared = 0x01,
        Private = 0x02,
        Fixed = 0x10,
    }
}
