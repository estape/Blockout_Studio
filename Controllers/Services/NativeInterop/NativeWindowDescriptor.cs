using System.Runtime.InteropServices;

namespace BlockoutStudio.Services.NativeInterop;

/// <summary>
/// Platform identifier for cross-platform window handle and surface binding.
/// </summary>
public enum NativePlatformType : uint
{
    Unknown = 0,
    Win32   = 1,
    X11     = 2,
    Wayland = 3,
    macOS   = 4,
}

/// <summary>
/// Unified cross-platform window descriptor struct passed across the C-ABI to EngineNative.
/// Exactly 32 bytes with 64-bit natural alignment.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NativeWindowDescriptor
{
    /// <summary>Underlying windowing system type (Win32, X11, Wayland, macOS).</summary>
    public NativePlatformType PlatformType;

    /// <summary>Explicit 32-bit padding ensuring DisplayHandle lands on an 8-byte boundary.</summary>
    public uint Padding;

    /// <summary>Win32: IntPtr.Zero | X11: Display* | Wayland: struct wl_display*</summary>
    public IntPtr DisplayHandle;

    /// <summary>Win32: HWND | X11: Window XID | Wayland: struct wl_surface*</summary>
    public IntPtr WindowHandle;

    /// <summary>Target surface width in physical pixels.</summary>
    public uint Width;

    /// <summary>Target surface height in physical pixels.</summary>
    public uint Height;

    public NativeWindowDescriptor(NativePlatformType platformType, IntPtr displayHandle, IntPtr windowHandle, uint width, uint height)
    {
        PlatformType = platformType;
        Padding = 0;
        DisplayHandle = displayHandle;
        WindowHandle = windowHandle;
        Width = width;
        Height = height;
    }
}
