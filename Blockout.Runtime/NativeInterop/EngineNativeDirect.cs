using System.Runtime.InteropServices;

namespace Blockout.Runtime.NativeInterop;

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
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NativeWindowDescriptor
{
    public NativePlatformType PlatformType;
    public uint Padding;
    public IntPtr DisplayHandle;
    public IntPtr WindowHandle;
    public uint Width;
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

/// <summary>
/// Direct P/Invoke bindings linking directly to EngineNative.dll with zero runtime overhead.
/// </summary>
public static partial class EngineNativeDirect
{
    private const string LibraryName = "EngineNative";

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_GetVersion")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int GetVersion();

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_InitEngine")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int InitEngine(nint osWindowHandle, int width, int height);

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_InitRendererDescriptor")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int InitRendererDescriptor(ref NativeWindowDescriptor descriptor);

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_InitRenderer")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int InitRenderer(nint nativeWindowHandle, uint initialWidth, uint initialHeight);

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_ShutdownEngine")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void ShutdownEngine();

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_IsRendererRunning")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int IsRendererRunning();

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_SetViewportScissor")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void SetViewportScissor(int x, int y, int width, int height);

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_SetTargetFps")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void SetTargetFps(int fps);
}

