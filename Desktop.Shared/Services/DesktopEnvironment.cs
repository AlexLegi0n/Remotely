﻿using Remotely.Desktop.Shared.Native.Linux;
using System.Security.Principal;
using Desktop.Native.Linux;

namespace Desktop.Shared.Services;

public interface IDesktopEnvironment
{
    bool IsElevated { get; }
    bool IsDebug { get; }
}

internal class DesktopEnvironment : IDesktopEnvironment
{
    public bool IsDebug
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public bool IsElevated
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            if (OperatingSystem.IsLinux())
            {
                return Libc.geteuid() == 0;
            }
            return false;
        }
    }
}
