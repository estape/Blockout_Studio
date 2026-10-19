using System.Globalization;
using System.Text;

namespace BlockoutStudio.Services.Assets;

public class ObjPipeline : IAssetPipeline
{
    public string FormatExtension => ".obj";
    public string FormatDescription => "Wavefront OBJ (.obj)";

    public string ExportToString(ModelData model)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        sb.AppendLine("# Exported from Blockout Studio (.NET 10 Game Engine Core)");
        sb.AppendLine("# Coordinate System: Z-Up Centimeters (1 Unit = 1 cm)");
        sb.AppendLine();

        int vOffset = 1;
        int vtOffset = 1;
        int vnOffset = 1;

        foreach (var part in model.Parts)
        {
            sb.AppendLine($"o {part.Name}");
            sb.AppendLine($"g {part.Name}");

            var pos = part.Positions;
            var uvs = part.UVs;
            var norms = part.Normals;
            var indices = part.Indices;

            // Vertices
            for (int i = 0; i < pos.Length; i += 3)
            {
                // Three.js X (Right), Y (Up), Z (Backwards) -> Engine: X (Forward), Y (Right), Z (Up)
                float uX = -pos[i + 2];
                float uY = pos[i];
                float uZ = pos[i + 1];
                sb.AppendLine($"v {uX.ToString("F4", inv)} {uY.ToString("F4", inv)} {uZ.ToString("F4", inv)}");
            }

            // UVs
            if (uvs != null && uvs.Length > 0)
            {
                for (int i = 0; i < uvs.Length; i += 2)
                {
                    sb.AppendLine($"vt {uvs[i].ToString("F4", inv)} {uvs[i + 1].ToString("F4", inv)}");
                }
            }

            // Normals
            if (norms != null && norms.Length > 0)
            {
                for (int i = 0; i < norms.Length; i += 3)
                {
                    float unX = -norms[i + 2];
                    float unY = norms[i];
                    float unZ = norms[i + 1];
                    sb.AppendLine($"vn {unX.ToString("F4", inv)} {unY.ToString("F4", inv)} {unZ.ToString("F4", inv)}");
                }
            }

            bool hasUv = uvs != null && uvs.Length > 0;
            bool hasNorm = norms != null && norms.Length > 0;

            // Faces
            if (indices != null && indices.Length > 0)
            {
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int i0 = indices[i] + vOffset;
                    int i1 = indices[i + 1] + vOffset;
                    int i2 = indices[i + 2] + vOffset;

                    if (hasUv && hasNorm)
                    {
                        sb.AppendLine($"f {i0}/{i0}/{i0} {i1}/{i1}/{i1} {i2}/{i2}/{i2}");
                    }
                    else if (hasNorm)
                    {
                        sb.AppendLine($"f {i0}//{i0} {i1}//{i1} {i2}//{i2}");
                    }
                    else
                    {
                        sb.AppendLine($"f {i0} {i1} {i2}");
                    }
                }
            }
            else
            {
                int vCount = pos.Length / 3;
                for (int i = 0; i < vCount; i += 3)
                {
                    int i0 = vOffset + i;
                    int i1 = vOffset + i + 1;
                    int i2 = vOffset + i + 2;

                    if (hasUv && hasNorm)
                    {
                        sb.AppendLine($"f {i0}/{i0}/{i0} {i1}/{i1}/{i1} {i2}/{i2}/{i2}");
                    }
                    else
                    {
                        sb.AppendLine($"f {i0} {i1} {i2}");
                    }
                }
            }

            vOffset += pos.Length / 3;
            if (hasUv) vtOffset += uvs!.Length / 2;
            if (hasNorm) vnOffset += norms!.Length / 3;
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public ModelData ImportFromString(string content)
    {
        var model = new ModelData();
        var part = new ModelMeshPart { Name = "Imported_Mesh" };
        var positions = new List<float>();
        var normals = new List<float>();
        var uvs = new List<float>();
        var indices = new List<int>();

        var inv = CultureInfo.InvariantCulture;

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.StartsWith('#') || string.IsNullOrEmpty(line)) continue;

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) continue;

            if (tokens[0] == "v" && tokens.Length >= 4)
            {
                if (float.TryParse(tokens[1], NumberStyles.Float, inv, out var x) &&
                    float.TryParse(tokens[2], NumberStyles.Float, inv, out var y) &&
                    float.TryParse(tokens[3], NumberStyles.Float, inv, out var z))
                {
                    // Convert Z-Up coordinate back to Three.js
                    positions.Add(y);
                    positions.Add(z);
                    positions.Add(-x);
                }
            }
            else if (tokens[0] == "vt" && tokens.Length >= 3)
            {
                if (float.TryParse(tokens[1], NumberStyles.Float, inv, out var u) &&
                    float.TryParse(tokens[2], NumberStyles.Float, inv, out var v))
                {
                    uvs.Add(u);
                    uvs.Add(v);
                }
            }
            else if (tokens[0] == "vn" && tokens.Length >= 4)
            {
                if (float.TryParse(tokens[1], NumberStyles.Float, inv, out var nx) &&
                    float.TryParse(tokens[2], NumberStyles.Float, inv, out var ny) &&
                    float.TryParse(tokens[3], NumberStyles.Float, inv, out var nz))
                {
                    normals.Add(ny);
                    normals.Add(nz);
                    normals.Add(-nx);
                }
            }
            else if (tokens[0] == "f" && tokens.Length >= 4)
            {
                // Triangulate face
                var faceIndices = new List<int>();
                for (int i = 1; i < tokens.Length; ++i)
                {
                    var vertToken = tokens[i].Split('/')[0];
                    if (int.TryParse(vertToken, out var vIdx))
                    {
                        faceIndices.Add(vIdx > 0 ? vIdx - 1 : (positions.Count / 3) + vIdx);
                    }
                }

                for (int t = 1; t < faceIndices.Count - 1; ++t)
                {
                    indices.Add(faceIndices[0]);
                    indices.Add(faceIndices[t]);
                    indices.Add(faceIndices[t + 1]);
                }
            }
        }

        part.Positions = [.. positions];
        part.Normals = normals.Count > 0 ? [.. normals] : null;
        part.UVs = uvs.Count > 0 ? [.. uvs] : null;
        part.Indices = indices.Count > 0 ? [.. indices] : null;
        model.Parts.Add(part);

        return model;
    }
}
