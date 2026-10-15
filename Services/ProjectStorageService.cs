using System.Runtime.InteropServices;
using System.Text.Json;

namespace UE5BlockoutStudio.Services;

public static class ProjectStorageService
{
    private static readonly string DefaultProjectsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "UE5BlockoutStudio"
    );

    static ProjectStorageService()
    {
        if (!Directory.Exists(DefaultProjectsDir))
        {
            Directory.CreateDirectory(DefaultProjectsDir);
        }
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetSaveFileName([In, Out] OpenFileName ofn);

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class OpenFileName
    {
        public int structSize = 0;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string? filter = null;
        public string? customFilter = null;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public string? file = null;
        public int maxFile = 0;
        public string? fileTitle = null;
        public int maxFileTitle = 0;
        public string? initialDir = null;
        public string? title = null;
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        public string? defExt = null;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string? templateName = null;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }

    public static string? SaveProjectDialog(string jsonContent, string defaultFileName = "Level_Blockout.ueblock")
    {
        try
        {
            var ofn = new OpenFileName
            {
                structSize = Marshal.SizeOf(typeof(OpenFileName)),
                filter = "UE5 Blockout Project (*.ueblock)\0*.ueblock\0All Files (*.*)\0*.*\0",
                file = defaultFileName + new string('\0', 260),
                maxFile = 260,
                initialDir = DefaultProjectsDir,
                title = "Save UE5 Blockout Project",
                defExt = "ueblock",
                flags = 0x00000002 | 0x00000004 // OFN_OVERWRITEPROMPT | OFN_HIDEREADONLY
            };

            if (GetSaveFileName(ofn))
            {
                var chosenPath = ofn.file?.TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(chosenPath))
                {
                    File.WriteAllText(chosenPath, jsonContent);
                    return chosenPath;
                }
            }
        }
        catch
        {
            // Fallback to saving in default directory
            var fallback = Path.Combine(DefaultProjectsDir, defaultFileName);
            File.WriteAllText(fallback, jsonContent);
            return fallback;
        }

        return null;
    }

    public static (string? Path, string? Content) LoadProjectDialog()
    {
        try
        {
            var ofn = new OpenFileName
            {
                structSize = Marshal.SizeOf(typeof(OpenFileName)),
                filter = "UE5 Blockout Project (*.ueblock)\0*.ueblock\0JSON Files (*.json)\0*.json\0All Files (*.*)\0*.*\0",
                file = new string('\0', 260),
                maxFile = 260,
                initialDir = DefaultProjectsDir,
                title = "Open UE5 Blockout Project",
                flags = 0x00001000 | 0x00000800 // OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST
            };

            if (GetOpenFileName(ofn))
            {
                var chosenPath = ofn.file?.TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(chosenPath) && File.Exists(chosenPath))
                {
                    var text = File.ReadAllText(chosenPath);
                    return (chosenPath, text);
                }
            }
        }
        catch
        {
            // ignored
        }

        return (null, null);
    }
}

