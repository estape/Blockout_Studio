using System.Globalization;
using System.Numerics;
using System.Text;

namespace UE5BlockoutStudio.Services.Geometry;

public class UsdPrim
{
    public string Name { get; set; } = "Prim";
    public string Specifier { get; set; } = "def"; // "def", "over", "class"
    public string TypeName { get; set; } = "Xform"; // "Xform", "Mesh", "Scope"
    public string? ReferencePath { get; set; }
    public string? PayloadPath { get; set; }
    public List<string> Sublayers { get; set; } = new();
    public Dictionary<string, string> VariantSets { get; set; } = new(); // VariantSetName -> ActiveVariant
    public Vector3 Translation { get; set; } = Vector3.Zero;
    public Vector3 RotationDegrees { get; set; } = Vector3.Zero;
    public Vector3 Scale { get; set; } = Vector3.One;
    public string? MaterialBinding { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public List<UsdPrim> Children { get; set; } = new();
}

public class UsdStage
{
    public string StageName { get; set; } = "SceneStage";
    public float MetersPerUnit { get; set; } = 0.01f; // 1 unit = 1 cm (UE5 standard)
    public string UpAxis { get; set; } = "Z";
    public List<string> Sublayers { get; set; } = new();
    public List<UsdPrim> RootPrims { get; set; } = new();
}

public class UsdStageService : IUsdStageService
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public UsdStage CreateStage(string stageName = "Level_Stage", float metersPerUnit = 0.01f, string upAxis = "Z")
    {
        return new UsdStage
        {
            StageName = stageName,
            MetersPerUnit = metersPerUnit,
            UpAxis = upAxis
        };
    }

    public UsdPrim AddReference(
        UsdStage stage,
        string primName,
        string assetVirtualOrDiskPath,
        Vector3 translation,
        Vector3 rotationDeg,
        Vector3 scale,
        string? materialBinding = null)
    {
        var prim = new UsdPrim
        {
            Name = SanitizeIdentifier(primName),
            Specifier = "def",
            TypeName = "Xform",
            ReferencePath = assetVirtualOrDiskPath,
            Translation = translation,
            RotationDegrees = rotationDeg,
            Scale = scale,
            MaterialBinding = materialBinding
        };

        stage.RootPrims.Add(prim);
        return prim;
    }

    public UsdPrim AddPayload(
        UsdStage stage,
        string primName,
        string assetVirtualOrDiskPath,
        Vector3 translation,
        Vector3 rotationDeg,
        Vector3 scale)
    {
        var prim = new UsdPrim
        {
            Name = SanitizeIdentifier(primName),
            Specifier = "def",
            TypeName = "Xform",
            PayloadPath = assetVirtualOrDiskPath,
            Translation = translation,
            RotationDegrees = rotationDeg,
            Scale = scale
        };
        stage.RootPrims.Add(prim);
        return prim;
    }

    public void AddVariantSet(
        UsdPrim prim,
        string variantSetName,
        Dictionary<string, string> variants,
        string defaultVariant)
    {
        if (variants != null && variants.Count > 0)
        {
            prim.VariantSets[variantSetName] = defaultVariant ?? variants.Keys.FirstOrDefault() ?? "";
        }
    }

    public void AddOverOpinion(
        UsdStage stage,
        string primName,
        Vector3? translation = null,
        Vector3? rotationDeg = null,
        Vector3? scale = null,
        string? materialOverride = null)
    {
        ApplyOverOpinion(stage, primName, translation, rotationDeg, scale, materialOverride);
    }

    public UsdPrim ApplyOverOpinion(
        UsdStage stage,
        string primName,
        Vector3? translation = null,
        Vector3? rotationDeg = null,
        Vector3? scale = null,
        string? materialBinding = null)
    {
        var cleanName = SanitizeIdentifier(primName);
        var existing = stage.RootPrims.FirstOrDefault(p => p.Name == cleanName && p.Specifier == "over");

        if (existing == null)
        {
            existing = new UsdPrim
            {
                Name = cleanName,
                Specifier = "over",
                TypeName = ""
            };
            stage.RootPrims.Add(existing);
        }

        if (translation.HasValue) existing.Translation = translation.Value;
        if (rotationDeg.HasValue) existing.RotationDegrees = rotationDeg.Value;
        if (scale.HasValue) existing.Scale = scale.Value;
        if (materialBinding != null) existing.MaterialBinding = materialBinding;

        return existing;
    }

    public string ExportToUsda(UsdStage stage) => ExportToUsdaString(stage);

    public string ExportToUsdaString(UsdStage stage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#usda 1.0");
        sb.AppendLine("(");
        sb.AppendLine($"    defaultPrim = \"{SanitizeIdentifier(stage.StageName)}\"");
        sb.AppendLine($"    metersPerUnit = {stage.MetersPerUnit.ToString("F4", Inv)}");
        sb.AppendLine($"    upAxis = \"{stage.UpAxis}\"");

        if (stage.Sublayers.Count > 0)
        {
            sb.AppendLine("    subLayers = [");
            for (int i = 0; i < stage.Sublayers.Count; i++)
            {
                sb.AppendLine($"        @{stage.Sublayers[i]}@{(i < stage.Sublayers.Count - 1 ? "," : "")}");
            }
            sb.AppendLine("    ]");
        }

        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine($"def Xform \"{SanitizeIdentifier(stage.StageName)}\"");
        sb.AppendLine("{");

        foreach (var prim in stage.RootPrims)
        {
            WritePrim(sb, prim, "    ");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private void WritePrim(StringBuilder sb, UsdPrim prim, string indent)
    {
        var typeDecl = string.IsNullOrWhiteSpace(prim.TypeName) ? "" : $" {prim.TypeName}";
        var refDecl = !string.IsNullOrWhiteSpace(prim.ReferencePath) ? $" (references = @{prim.ReferencePath}@)" : "";
        var payloadDecl = !string.IsNullOrWhiteSpace(prim.PayloadPath) ? $" (payload = @{prim.PayloadPath}@)" : "";

        sb.AppendLine($"{indent}{prim.Specifier}{typeDecl} \"{prim.Name}\"{refDecl}{payloadDecl}");
        sb.AppendLine($"{indent}{{");

        // Transforms (UE5 xformOps)
        if (prim.Translation != Vector3.Zero || prim.RotationDegrees != Vector3.Zero || prim.Scale != Vector3.One)
        {
            sb.AppendLine($"{indent}    double3 xformOp:translate = ({prim.Translation.X.ToString("F3", Inv)}, {prim.Translation.Y.ToString("F3", Inv)}, {prim.Translation.Z.ToString("F3", Inv)})");
            sb.AppendLine($"{indent}    float3 xformOp:rotateXYZ = ({prim.RotationDegrees.X.ToString("F3", Inv)}, {prim.RotationDegrees.Y.ToString("F3", Inv)}, {prim.RotationDegrees.Z.ToString("F3", Inv)})");
            sb.AppendLine($"{indent}    float3 xformOp:scale = ({prim.Scale.X.ToString("F3", Inv)}, {prim.Scale.Y.ToString("F3", Inv)}, {prim.Scale.Z.ToString("F3", Inv)})");
            sb.AppendLine($"{indent}    uniform token[] xformOpOrder = [\"xformOp:translate\", \"xformOp:rotateXYZ\", \"xformOp:scale\"]");
        }

        // Material Override
        if (!string.IsNullOrWhiteSpace(prim.MaterialBinding))
        {
            sb.AppendLine($"{indent}    rel material:binding = <{prim.MaterialBinding}>");
        }

        // VariantSets
        if (prim.VariantSets.Count > 0)
        {
            foreach (var kvp in prim.VariantSets)
            {
                sb.AppendLine($"{indent}    variantSet \"{kvp.Key}\" = {{");
                sb.AppendLine($"{indent}        \"{kvp.Value}\" {{}}");
                sb.AppendLine($"{indent}    }}");
            }
        }

        foreach (var child in prim.Children)
        {
            WritePrim(sb, child, indent + "    ");
        }

        sb.AppendLine($"{indent}}}");
    }

    public UsdStage ParseUsda(string usdaText)
    {
        var stage = new UsdStage();
        using var reader = new StringReader(usdaText);
        string? line;
        UsdPrim? currentPrim = null;

        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("metersPerUnit"))
            {
                var valStr = ExtractValue(trimmed, '=');
                if (float.TryParse(valStr, NumberStyles.Float, Inv, out float mpu)) stage.MetersPerUnit = mpu;
            }
            else if (trimmed.StartsWith("upAxis"))
            {
                stage.UpAxis = ExtractQuotedString(trimmed);
            }
            else if (trimmed.StartsWith("def ") || trimmed.StartsWith("over "))
            {
                var spec = trimmed.StartsWith("def ") ? "def" : "over";
                var primName = ExtractQuotedString(trimmed);
                if (!string.IsNullOrWhiteSpace(primName))
                {
                    currentPrim = new UsdPrim
                    {
                        Name = primName,
                        Specifier = spec,
                        TypeName = trimmed.Contains("Mesh") ? "Mesh" : "Xform"
                    };

                    if (trimmed.Contains("references"))
                    {
                        currentPrim.ReferencePath = ExtractBetween(trimmed, '@', '@');
                    }

                    stage.RootPrims.Add(currentPrim);
                }
            }
            else if (currentPrim != null)
            {
                if (trimmed.StartsWith("double3 xformOp:translate") || trimmed.StartsWith("float3 xformOp:translate"))
                {
                    currentPrim.Translation = ParseVec3(trimmed);
                }
                else if (trimmed.StartsWith("float3 xformOp:rotateXYZ"))
                {
                    currentPrim.RotationDegrees = ParseVec3(trimmed);
                }
                else if (trimmed.StartsWith("float3 xformOp:scale"))
                {
                    currentPrim.Scale = ParseVec3(trimmed);
                }
                else if (trimmed.StartsWith("rel material:binding"))
                {
                    currentPrim.MaterialBinding = ExtractBetween(trimmed, '<', '>');
                }
            }
        }

        return stage;
    }

    private static string SanitizeIdentifier(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            else sb.Append('_');
        }
        return sb.Length > 0 ? sb.ToString() : "Prim";
    }

    private static string ExtractValue(string line, char separator)
    {
        int idx = line.IndexOf(separator);
        return idx >= 0 ? line[(idx + 1)..].Trim().TrimEnd(';') : "";
    }

    private static string ExtractQuotedString(string line)
    {
        int first = line.IndexOf('"');
        if (first < 0) return "";
        int second = line.IndexOf('"', first + 1);
        return second > first ? line.Substring(first + 1, second - first - 1) : "";
    }

    private static string ExtractBetween(string line, char start, char end)
    {
        int s = line.IndexOf(start);
        if (s < 0) return "";
        int e = line.IndexOf(end, s + 1);
        return e > s ? line.Substring(s + 1, e - s - 1) : "";
    }

    private static Vector3 ParseVec3(string line)
    {
        int s = line.IndexOf('(');
        int e = line.IndexOf(')');
        if (s >= 0 && e > s)
        {
            var parts = line.Substring(s + 1, e - s - 1).Split(',');
            if (parts.Length >= 3 &&
                float.TryParse(parts[0], NumberStyles.Float, Inv, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, Inv, out float y) &&
                float.TryParse(parts[2], NumberStyles.Float, Inv, out float z))
            {
                return new Vector3(x, y, z);
            }
        }
        return Vector3.Zero;
    }
}
