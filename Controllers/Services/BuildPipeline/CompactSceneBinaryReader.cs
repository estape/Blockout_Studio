using System.Numerics;
using System.Text;
using BlockoutStudio.Services.Blueprints.Runtime;

namespace BlockoutStudio.Services.BuildPipeline;

/// <summary>
/// High-density zero-allocation binary reader for Scene.bin files.
/// </summary>
public static class CompactSceneBinaryReader
{
    public const uint SceneMagic = 0x4253434E; // "BSCN"
    public const uint FormatVersion = 1;

    public static List<Actor> ReadScene(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var magic = reader.ReadUInt32();
        if (magic != SceneMagic)
        {
            throw new InvalidDataException($"Invalid scene magic: 0x{magic:X8}, expected 0x{SceneMagic:X8}");
        }

        var version = reader.ReadUInt32();
        if (version != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported scene format version: {version}");
        }

        var actorCount = reader.ReadInt32();
        var actors = new List<Actor>(actorCount);

        for (int i = 0; i < actorCount; i++)
        {
            var id = reader.ReadString();
            var name = reader.ReadString();
            var posX = reader.ReadSingle();
            var posY = reader.ReadSingle();
            var posZ = reader.ReadSingle();
            var rotX = reader.ReadSingle();
            var rotY = reader.ReadSingle();
            var rotZ = reader.ReadSingle();
            var scaleX = reader.ReadSingle();
            var scaleY = reader.ReadSingle();
            var scaleZ = reader.ReadSingle();

            var actor = new Actor(
                id,
                name,
                new Vector3(posX, posY, posZ),
                new Vector3(rotX, rotY, rotZ),
                new Vector3(scaleX, scaleY, scaleZ));

            var compCount = reader.ReadInt32();
            for (int c = 0; c < compCount; c++)
            {
                var compType = reader.ReadString();
                var propCount = reader.ReadInt32();
                var props = new Dictionary<string, string>(propCount);
                for (int p = 0; p < propCount; p++)
                {
                    var k = reader.ReadString();
                    var v = reader.ReadString();
                    props[k] = v;
                }
                actor.Properties[$"Component_{compType}"] = props;
            }

            actors.Add(actor);
        }

        return actors;
    }
}

