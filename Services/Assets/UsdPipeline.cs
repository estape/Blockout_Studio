using System.Globalization;
using System.Text;

namespace BlockoutStudio.Services.Assets;

/// <summary>
/// Pipeline for importing and exporting Universal Scene Description ASCII (.usda) files with Z-Up Centimeters metric scale.
/// </summary>
public class UsdPipeline : IAssetPipeline
{
    /// <inheritdoc/>
    public string FormatExtension => ".usda";

    /// <inheritdoc/>
    public string FormatDescription => "Universal Scene Description ASCII (.usda)";

    /// <inheritdoc/>
    public string ExportToString(ModelData model)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        sb.AppendLine("#usda 1.0");
        sb.AppendLine("(");
        sb.AppendLine($"    defaultPrim = \"{Sanitize(model.SceneName)}\"");
        sb.AppendLine("    metersPerUnit = 0.01"); // 1 unit = 1 cm (Metric standard)
        sb.AppendLine("    upAxis = \"Z\"");
        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine($"def Xform \"{Sanitize(model.SceneName)}\"");
        sb.AppendLine("{");

        foreach (var part in model.Parts)
        {
            var partName = Sanitize(part.Name);
            sb.AppendLine($"    def Mesh \"{partName}\"");
            sb.AppendLine("    {");
            sb.AppendLine("        uniform token subdivisionScheme = \"none\"");

            var pos = part.Positions;
            var indices = part.Indices;
            var uvs = part.UVs;
            var norms = part.Normals;

            // Compute face counts and face indices
            int triangleCount = (indices != null && indices.Length > 0) ? indices.Length / 3 : pos.Length / 9;

            // faceVertexCounts: all 3s for triangles
            sb.Append("        int[] faceVertexCounts = [");
            for (int t = 0; t < triangleCount; ++t)
            {
                sb.Append(t == 0 ? "3" : ", 3");
            }
            sb.AppendLine("]");

            // faceVertexIndices
            sb.Append("        int[] faceVertexIndices = [");
            if (indices != null && indices.Length > 0)
            {
                for (int i = 0; i < indices.Length; ++i)
                {
                    sb.Append(i == 0 ? $"{indices[i]}" : $", {indices[i]}");
                }
            }
            else
            {
                int vCount = pos.Length / 3;
                for (int i = 0; i < vCount; ++i)
                {
                    sb.Append(i == 0 ? $"{i}" : $", {i}");
                }
            }
            sb.AppendLine("]");

            // Points (Engine coordinate mapping: X forward = -posZ, Y right = posX, Z up = posY)
            sb.Append("        point3f[] points = [");
            int vertCount = pos.Length / 3;
            for (int i = 0; i < vertCount; ++i)
            {
                float ux = -pos[i * 3 + 2];
                float uy = pos[i * 3];
                float uz = pos[i * 3 + 1];

                if (i > 0) sb.Append(", ");
                sb.Append($"({ux.ToString("F3", inv)}, {uy.ToString("F3", inv)}, {uz.ToString("F3", inv)})");
            }
            sb.AppendLine("]");

            // Normals
            if (norms != null && norms.Length > 0)
            {
                sb.AppendLine("        normal3f[] primvars:normals = [");
                int normCount = norms.Length / 3;
                for (int i = 0; i < normCount; ++i)
                {
                    float unx = -norms[i * 3 + 2];
                    float uny = norms[i * 3];
                    float unz = norms[i * 3 + 1];

                    sb.Append(i == 0 ? "            " : ", ");
                    sb.Append($"({unx.ToString("F3", inv)}, {uny.ToString("F3", inv)}, {unz.ToString("F3", inv)})");
                    if ((i + 1) % 6 == 0 && i + 1 < normCount) sb.AppendLine();
                }
                sb.AppendLine();
                sb.AppendLine("        ] (");
                sb.AppendLine("            interpolation = \"vertex\"");
                sb.AppendLine("        )");
            }

            // UVs
            if (uvs != null && uvs.Length > 0)
            {
                sb.AppendLine("        float2[] primvars:st = [");
                int uvCount = uvs.Length / 2;
                for (int i = 0; i < uvCount; ++i)
                {
                    float u = uvs[i * 2];
                    float v = uvs[i * 2 + 1];

                    sb.Append(i == 0 ? "            " : ", ");
                    sb.Append($"({u.ToString("F4", inv)}, {v.ToString("F4", inv)})");
                    if ((i + 1) % 8 == 0 && i + 1 < uvCount) sb.AppendLine();
                }
                sb.AppendLine();
                sb.AppendLine("        ] (");
                sb.AppendLine("            interpolation = \"vertex\"");
                sb.AppendLine("        )");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <inheritdoc/>
    public ModelData ImportFromString(string content)
    {
        // Basic USDA structural parser for point3f and faceVertexIndices
        var model = new ModelData { SceneName = "Imported_USDA" };
        var part = new ModelMeshPart { Name = "USDA_Mesh" };
        var positions = new List<float>();
        var indices = new List<int>();
        var inv = CultureInfo.InvariantCulture;

        using var reader = new StringReader(content);
        string? line;
        bool inPoints = false;
        bool inIndices = false;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();

            if (line.Contains("point3f[] points"))
            {
                inPoints = true;
                continue;
            }
            if (inPoints)
            {
                if (line.Contains(']')) inPoints = false;
                var tuples = line.Split('(', ')');
                foreach (var tuple in tuples)
                {
                    var coords = tuple.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (coords.Length == 3 &&
                        float.TryParse(coords[0], NumberStyles.Float, inv, out var x) &&
                        float.TryParse(coords[1], NumberStyles.Float, inv, out var y) &&
                        float.TryParse(coords[2], NumberStyles.Float, inv, out var z))
                    {
                        positions.Add(y);
                        positions.Add(z);
                        positions.Add(-x);
                    }
                }
            }

            if (line.Contains("int[] faceVertexIndices"))
            {
                inIndices = true;
                var afterBracket = line.Substring(line.IndexOf('[') + 1);
                var endIdx = afterBracket.IndexOf(']');
                var tokenStr = endIdx >= 0 ? afterBracket.Substring(0, endIdx) : afterBracket;
                ExtractIndices(tokenStr, indices);
                if (endIdx >= 0) inIndices = false;
                continue;
            }
            if (inIndices)
            {
                if (line.Contains(']'))
                {
                    inIndices = false;
                    ExtractIndices(line.Substring(0, line.IndexOf(']')), indices);
                }
                else
                {
                    ExtractIndices(line, indices);
                }
            }
        }

        part.Positions = [.. positions];
        part.Indices = indices.Count > 0 ? [.. indices] : null;
        model.Parts.Add(part);
        return model;
    }

    /// <summary>
    /// Parses a comma-delimited sequence of integer indices from USDA token strings.
    /// </summary>
    private static void ExtractIndices(string str, List<int> list)
    {
        var tokens = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in tokens)
        {
            if (int.TryParse(t.Trim(), out var idx)) list.Add(idx);
        }
    }

    /// <summary>
    /// Normalizes names into valid OpenUSD identifier tokens.
    /// </summary>
    private static string Sanitize(string name)
    {
        var cleaned = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_') cleaned.Append(c);
            else cleaned.Append('_');
        }
        var res = cleaned.ToString();
        return string.IsNullOrEmpty(res) ? "Node" : (char.IsDigit(res[0]) ? "_" + res : res);
    }
}
