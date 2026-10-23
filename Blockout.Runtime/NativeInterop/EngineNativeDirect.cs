using System.Runtime.InteropServices;

namespace Blockout.Runtime.NativeInterop;

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

