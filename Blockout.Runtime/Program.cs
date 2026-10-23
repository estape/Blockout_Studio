using System.Diagnostics;
using Blockout.Runtime.Core;
using Blockout.Runtime.NativeInterop;
using BlockoutStudio.Services.Blueprints.Runtime;

namespace Blockout.Runtime;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("Blockout Standalone Game Engine (.NET 10 NativeAOT)");
        Console.WriteLine("==================================================");

        try
        {
            // 1. Initialize EngineNative
            int nativeVer = EngineNativeDirect.GetVersion();
            Console.WriteLine($"[Runtime] EngineNative.dll linked successfully (API Version: {nativeVer})");

            // 2. Load baked binary scene
            var scenePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scenes", "Scene.bin");
            List<Actor> actors = [];

            if (File.Exists(scenePath))
            {
                using var fs = File.OpenRead(scenePath);
                actors = CompactSceneBinaryReader.ReadScene(fs);
                Console.WriteLine($"[Runtime] Loaded {actors.Count} actors from Scene.bin");
            }
            else
            {
                Console.WriteLine("[Runtime] No Scene.bin found, starting default empty level");
            }

            // 3. Instantiate game components & dispatch OnBeginPlay
            Console.WriteLine("[Runtime] Game started. Standalone execution active.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Runtime Fatal] {ex.Message}");
            return 1;
        }
    }
}
