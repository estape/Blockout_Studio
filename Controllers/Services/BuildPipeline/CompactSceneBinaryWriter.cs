using System.Numerics;
using System.Text;
using BlockoutStudio.Services.Blueprints.Runtime;

namespace BlockoutStudio.Services.BuildPipeline;

/// <summary>
/// Serializes editor actor hierarchies and scenes into high-density, flat binary Scene.bin files
/// for zero-allocation consumption by standalone runtime games.
/// </summary>
public static class CompactSceneBinaryWriter
{
    public const uint SceneMagic = 0x4253434E; // "BSCN"
    public const uint FormatVersion = 1;

    public static void WriteScene(Stream stream, IReadOnlyList<Actor> actors)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(actors);

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(SceneMagic);
        writer.Write(FormatVersion);
        writer.Write(actors.Count);

        foreach (var actor in actors)
        {
            writer.Write(actor.Id ?? string.Empty);
            writer.Write(actor.Name ?? "Actor");
            writer.Write(actor.Location.X);
            writer.Write(actor.Location.Y);
            writer.Write(actor.Location.Z);
            writer.Write(actor.Rotation.X);
            writer.Write(actor.Rotation.Y);
            writer.Write(actor.Rotation.Z);
            writer.Write(actor.Scale.X);
            writer.Write(actor.Scale.Y);
            writer.Write(actor.Scale.Z);

            // Filter component properties
            var componentProps = actor.Properties
                .Where(p => p.Key.StartsWith("Component_") && p.Value is IDictionary<string, string>)
                .ToList();

            writer.Write(componentProps.Count);
            foreach (var comp in componentProps)
            {
                var compType = comp.Key["Component_".Length..];
                writer.Write(compType);

                var dict = (IDictionary<string, string>)comp.Value!;
                writer.Write(dict.Count);
                foreach (var kvp in dict)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value ?? string.Empty);
                }
            }
        }

        writer.Flush();
    }

    public static byte[] WriteSceneToBytes(IReadOnlyList<Actor> actors)
    {
        using var ms = new MemoryStream();
        WriteScene(ms, actors);
        return ms.ToArray();
    }
}
