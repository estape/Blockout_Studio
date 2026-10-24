using System.Runtime.InteropServices;

namespace BlockoutStudio.Services.NativeInterop;

/// <summary>
/// Cross-platform interop helper resolving native display and surface handles from Photino's
/// WebKitGTK / GtkWindow pointer on Linux (X11 & Wayland).
/// </summary>
public static class LinuxWindowHelper
{
    private const string LibGtk = "libgtk-3.so.0";
    private const string LibGdk = "libgdk-3.so.0";
    private const string LibGObject = "libgobject-2.0.so.0";

    // GTK Widget Realization and Paint Properties
    [DllImport(LibGtk, EntryPoint = "gtk_widget_realize", SetLastError = false)]
    private static extern void GtkWidgetRealize(IntPtr widget);

    [DllImport(LibGtk, EntryPoint = "gtk_widget_get_window", SetLastError = false)]
    private static extern IntPtr GtkWidgetGetWindow(IntPtr widget);

    [DllImport(LibGtk, EntryPoint = "gtk_widget_set_app_paintable", SetLastError = false)]
    private static extern void GtkWidgetSetAppPaintable(IntPtr widget, int paintable);

    [DllImport(LibGtk, EntryPoint = "gtk_widget_set_double_buffered", SetLastError = false)]
    private static extern void GtkWidgetSetDoubleBuffered(IntPtr widget, int doubleBuffered);

    // GDK Display & Window Helpers
    [DllImport(LibGdk, EntryPoint = "gdk_display_get_default", SetLastError = false)]
    private static extern IntPtr GdkDisplayGetDefault();

    [DllImport(LibGdk, EntryPoint = "gdk_window_get_display", SetLastError = false)]
    private static extern IntPtr GdkWindowGetDisplay(IntPtr gdkWindow);

    // GLib / GObject Type Introspection
    [DllImport(LibGObject, EntryPoint = "g_type_name_from_instance", SetLastError = false)]
    private static extern IntPtr GTypeNameFromInstance(IntPtr instance);

    /// <summary>
    /// Extracts a fully populated NativeWindowDescriptor from a Photino GtkWindow pointer on Linux.
    /// Handles both native Wayland and X11/XWayland display servers.
    /// </summary>
    public static NativeWindowDescriptor ExtractDescriptor(IntPtr gtkWindow, uint width, uint height)
    {
        if (gtkWindow == IntPtr.Zero)
        {
            return new NativeWindowDescriptor(NativePlatformType.Unknown, IntPtr.Zero, IntPtr.Zero, width, height);
        }

        if (!OperatingSystem.IsLinux())
        {
            return new NativeWindowDescriptor(NativePlatformType.Unknown, IntPtr.Zero, gtkWindow, width, height);
        }

        try
        {
            // 1. Prevent GTK from clearing the background over the Vulkan swapchain
            TrySetGtkWidgetPaintProperties(gtkWindow);

            // 2. Ensure GtkWidget is realized so that the underlying GdkWindow exists
            IntPtr gdkWindow = TryGetGdkWindow(gtkWindow);
            if (gdkWindow == IntPtr.Zero)
            {
                return new NativeWindowDescriptor(NativePlatformType.X11, IntPtr.Zero, gtkWindow, width, height);
            }

            // 3. Query GdkDisplay
            IntPtr gdkDisplay = IntPtr.Zero;
            try
            {
                gdkDisplay = GdkWindowGetDisplay(gdkWindow);
            }
            catch
            {
                // Fallback
            }

            if (gdkDisplay == IntPtr.Zero)
            {
                try
                {
                    gdkDisplay = GdkDisplayGetDefault();
                }
                catch
                {
                    // Fallback
                }
            }

            // 4. Determine Wayland vs. X11 via GObject runtime type inspection
            string typeName = GetGObjectTypeName(gdkDisplay);

            if (typeName.Contains("Wayland", StringComparison.OrdinalIgnoreCase))
            {
                IntPtr wlDisplay = GetWaylandDisplay(gdkDisplay);
                IntPtr wlSurface = GetWaylandSurface(gdkWindow);
                return new NativeWindowDescriptor(NativePlatformType.Wayland, wlDisplay, wlSurface, width, height);
            }
            else
            {
                // X11 / XWayland
                IntPtr x11Display = GetX11Display(gdkDisplay);
                IntPtr x11Window = GetX11WindowXid(gdkWindow);
                if (x11Window == IntPtr.Zero) x11Window = gtkWindow;
                return new NativeWindowDescriptor(NativePlatformType.X11, x11Display, x11Window, width, height);
            }
        }
        catch
        {
            // Fail-safe fallback to generic X11 descriptor with provided window handle
            return new NativeWindowDescriptor(NativePlatformType.X11, IntPtr.Zero, gtkWindow, width, height);
        }
    }

    private static void TrySetGtkWidgetPaintProperties(IntPtr widget)
    {
        try
        {
            GtkWidgetSetAppPaintable(widget, 1);
            GtkWidgetSetDoubleBuffered(widget, 0);
        }
        catch
        {
            // In headless/mock environments, ignore missing GTK symbols
        }
    }

    private static IntPtr TryGetGdkWindow(IntPtr widget)
    {
        try
        {
            IntPtr gdkWindow = GtkWidgetGetWindow(widget);
            if (gdkWindow == IntPtr.Zero)
            {
                GtkWidgetRealize(widget);
                gdkWindow = GtkWidgetGetWindow(widget);
            }
            return gdkWindow;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static string GetGObjectTypeName(IntPtr instance)
    {
        if (instance != IntPtr.Zero)
        {
            try
            {
                IntPtr namePtr = GTypeNameFromInstance(instance);
                if (namePtr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringAnsi(namePtr) ?? string.Empty;
                }
            }
            catch
            {
                // Ignore and fall back to environment inspection
            }
        }

        string session = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "";
        if (session.Equals("wayland", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Environment.GetEnvironmentVariable("GDK_BACKEND"), "x11", StringComparison.OrdinalIgnoreCase))
        {
            return "GdkWaylandDisplay";
        }

        return "GdkX11Display";
    }

    private static IntPtr GetX11Display(IntPtr gdkDisplay)
    {
        if (NativeLibrary.TryLoad(LibGdk, out IntPtr gdkLib) &&
            NativeLibrary.TryGetExport(gdkLib, "gdk_x11_display_get_xdisplay", out IntPtr proc))
        {
            var func = Marshal.GetDelegateForFunctionPointer<Func<IntPtr, IntPtr>>(proc);
            return func(gdkDisplay);
        }
        return IntPtr.Zero;
    }

    private static IntPtr GetX11WindowXid(IntPtr gdkWindow)
    {
        if (NativeLibrary.TryLoad(LibGdk, out IntPtr gdkLib) &&
            NativeLibrary.TryGetExport(gdkLib, "gdk_x11_window_get_xid", out IntPtr proc))
        {
            // Note: In Xlib, Window is typedef unsigned long (8 bytes on 64-bit Linux)
            var func = Marshal.GetDelegateForFunctionPointer<Func<IntPtr, IntPtr>>(proc);
            return func(gdkWindow);
        }
        return IntPtr.Zero;
    }

    private static IntPtr GetWaylandDisplay(IntPtr gdkDisplay)
    {
        if (NativeLibrary.TryLoad(LibGdk, out IntPtr gdkLib) &&
            NativeLibrary.TryGetExport(gdkLib, "gdk_wayland_display_get_wl_display", out IntPtr proc))
        {
            var func = Marshal.GetDelegateForFunctionPointer<Func<IntPtr, IntPtr>>(proc);
            return func(gdkDisplay);
        }
        return IntPtr.Zero;
    }

    private static IntPtr GetWaylandSurface(IntPtr gdkWindow)
    {
        if (NativeLibrary.TryLoad(LibGdk, out IntPtr gdkLib) &&
            NativeLibrary.TryGetExport(gdkLib, "gdk_wayland_window_get_wl_surface", out IntPtr proc))
        {
            var func = Marshal.GetDelegateForFunctionPointer<Func<IntPtr, IntPtr>>(proc);
            return func(gdkWindow);
        }
        return IntPtr.Zero;
    }
}
