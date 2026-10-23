using System.Globalization;
using System.Text;
using System.Text.Json;
using TextCopy;

namespace BlockoutStudio.Services;

/// <summary>
/// Provides clipboard serialization for level geometry, generating actor graph text from scene objects and lights.
/// </summary>
public static class ClipboardExportService
{
    /// <summary>
    /// Generates Actor T3D clipboard plain text from the incoming JSON array of block/light objects
    /// and copies it to the system clipboard.
    /// Supports StaticMeshActors (Cube, Cylinder, Sphere, Plane) and LightActors (PointLight, SpotLight with Static or Stationary mobility).
    /// </summary>
    /// <param name="items">JsonElement containing array of scene actors and lights.</param>
    /// <returns>A tuple indicating success status, human-readable status message, and total actors serialized.</returns>
    public static (bool Success, string Message, int ActorCount) CopyToClipboard(JsonElement items)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Begin Map");
            sb.AppendLine("   Begin Level");

            int actorCount = 0;
            var inv = CultureInfo.InvariantCulture;

            foreach (var item in items.EnumerateArray())
            {
                actorCount++;
                var name = item.TryGetProperty("name", out var pName) ? pName.GetString() ?? $"SM_Actor_{actorCount}" : $"SM_Actor_{actorCount}";
                var shape = item.TryGetProperty("shapeType", out var pShape) ? pShape.GetString() ?? "box" : "box";

                double sx = item.TryGetProperty("sizeX", out var pSx) ? pSx.GetDouble() : 100.0;
                double sy = item.TryGetProperty("sizeY", out var pSy) ? pSy.GetDouble() : 100.0;
                double sz = item.TryGetProperty("sizeZ", out var pSz) ? pSz.GetDouble() : 100.0;

                // Three.js coordinates (cm)
                double posX = item.TryGetProperty("posX", out var pPx) ? pPx.GetDouble() : 0.0;
                double posY = item.TryGetProperty("posY", out var pPy) ? pPy.GetDouble() : 0.0; // Three.js Z
                double posZ = item.TryGetProperty("posZ", out var pPz) ? pPz.GetDouble() : 0.0; // Three.js Y (height)
                double rotZ = item.TryGetProperty("rotZ", out var pRz) ? pRz.GetDouble() : 0.0; // Yaw in degrees

                // ====================================================================
                // Coordinate Mapping (Three.js Y-Up to Engine Z-Up):
                // Three.js: X (Right), Y (Up), Z (Towards Viewer / Backwards)
                // World:    X (Forward), Y (Right), Z (Up)
                // Mapping:
                //   World X = -Three.js Z (posY)
                //   World Y =  Three.js X (posX)
                //   World Z =  Three.js Y (posZ)
                // ====================================================================
                double worldX = -posY;
                double worldY = posX;
                double worldZ = posZ;

                // Check for Light Actors (PointLight, SpotLight)
                if (shape.Equals("point_light", StringComparison.OrdinalIgnoreCase) ||
                    shape.Equals("spot_light", StringComparison.OrdinalIgnoreCase))
                {
                    var mobility = item.TryGetProperty("mobility", out var pMob) ? pMob.GetString() ?? "Static" : "Static";
                    double intensity = item.TryGetProperty("intensity", out var pInt) ? pInt.GetDouble() : 5000.0;
                    double radius = item.TryGetProperty("radius", out var pRad) ? pRad.GetDouble() : 1000.0;
                    string colorHex = item.TryGetProperty("color", out var pCol) ? pCol.GetString() ?? "#ffffff" : "#ffffff";

                    var (r, g, b) = ParseHexColor(colorHex);

                    if (shape.Equals("spot_light", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"      Begin Actor Class=/Script/Engine.SpotLight Name={name} Archetype=/Script/Engine.SpotLight'/Script/Engine.Default__SpotLight'");
                        sb.AppendLine($"         Begin Object Class=/Script/Engine.SpotLightComponent Name=SpotLightComponent0");
                        sb.AppendLine($"            Intensity={intensity.ToString("F1", inv)}");
                        sb.AppendLine($"            AttenuationRadius={radius.ToString("F1", inv)}");
                        sb.AppendLine($"            InnerConeAngle=20.000000");
                        sb.AppendLine($"            OuterConeAngle=44.000000");
                        sb.AppendLine($"            LightColor=(B={b},G={g},R={r},A=255)");
                        sb.AppendLine($"            Mobility={mobility}");
                        sb.AppendLine($"            RelativeLocation=(X={worldX.ToString("F3", inv)},Y={worldY.ToString("F3", inv)},Z={worldZ.ToString("F3", inv)})");
                        sb.AppendLine($"            RelativeRotation=(Pitch=0.000000,Yaw={rotZ.ToString("F3", inv)},Roll=0.000000)");
                        sb.AppendLine($"         End Object");
                        sb.AppendLine($"      End Actor");
                    }
                    else
                    {
                        sb.AppendLine($"      Begin Actor Class=/Script/Engine.PointLight Name={name} Archetype=/Script/Engine.PointLight'/Script/Engine.Default__PointLight'");
                        sb.AppendLine($"         Begin Object Class=/Script/Engine.PointLightComponent Name=PointLightComponent0");
                        sb.AppendLine($"            Intensity={intensity.ToString("F1", inv)}");
                        sb.AppendLine($"            AttenuationRadius={radius.ToString("F1", inv)}");
                        sb.AppendLine($"            LightColor=(B={b},G={g},R={r},A=255)");
                        sb.AppendLine($"            Mobility={mobility}");
                        sb.AppendLine($"            RelativeLocation=(X={worldX.ToString("F3", inv)},Y={worldY.ToString("F3", inv)},Z={worldZ.ToString("F3", inv)})");
                        sb.AppendLine($"         End Object");
                        sb.AppendLine($"      End Actor");
                    }
                    continue;
                }

                // StaticMesh Actors
                string meshPath;
                double scaleX;
                double scaleY;
                double scaleZ;

                if (shape.Equals("cylinder", StringComparison.OrdinalIgnoreCase))
                {
                    meshPath = "/Engine/BasicShapes/Cylinder.Cylinder";
                    scaleX = sx / 100.0;
                    scaleY = sy / 100.0;
                    scaleZ = sz / 100.0;
                    worldZ += sz / 2.0;
                }
                else if (shape.Equals("sphere", StringComparison.OrdinalIgnoreCase))
                {
                    meshPath = "/Engine/BasicShapes/Sphere.Sphere";
                    double diam = sx;
                    scaleX = diam / 100.0;
                    scaleY = diam / 100.0;
                    scaleZ = diam / 100.0;
                    worldZ += diam / 2.0;
                }
                else if (shape.Equals("plane", StringComparison.OrdinalIgnoreCase))
                {
                    meshPath = "/Engine/BasicShapes/Plane.Plane";
                    scaleX = sy / 100.0;
                    scaleY = sx / 100.0;
                    scaleZ = 1.0;
                }
                else
                {
                    // Box, Wall, Floor, Pillar, Cover, etc.
                    meshPath = "/Engine/BasicShapes/Cube.Cube";
                    scaleX = sy / 100.0;
                    scaleY = sx / 100.0;
                    scaleZ = sz / 100.0;
                    worldZ += sz / 2.0;
                }

                sb.AppendLine($"      Begin Actor Class=/Script/Engine.StaticMeshActor Name={name} Archetype=/Script/Engine.StaticMeshActor'/Script/Engine.Default__StaticMeshActor'");
                sb.AppendLine($"         Begin Object Class=/Script/Engine.StaticMeshComponent Name=StaticMeshComponent0");
                sb.AppendLine($"            StaticMesh=StaticMesh'{meshPath}'");
                sb.AppendLine($"            RelativeLocation=(X={worldX.ToString("F3", inv)},Y={worldY.ToString("F3", inv)},Z={worldZ.ToString("F3", inv)})");
                sb.AppendLine($"            RelativeRotation=(Pitch=0.000000,Yaw={rotZ.ToString("F3", inv)},Roll=0.000000)");
                sb.AppendLine($"            RelativeScale3D=(X={scaleX.ToString("F3", inv)},Y={scaleY.ToString("F3", inv)},Z={scaleZ.ToString("F3", inv)})");
                sb.AppendLine($"         End Object");
                sb.AppendLine($"      End Actor");
            }

            sb.AppendLine("   End Level");
            sb.AppendLine("End Map");

            var t3dText = sb.ToString();
            bool copied = false;
            Exception? lastEx = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    ClipboardService.SetText(t3dText);
                    copied = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Thread.Sleep(40);
                }
            }

            if (!copied && lastEx != null)
            {
                return (true, $"Generated scene text ({actorCount} actors). Clipboard was busy: {lastEx.Message}", actorCount);
            }

            return (true, $"Successfully copied {actorCount} actor(s) to system clipboard!", actorCount);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to copy to clipboard: {ex.Message}", 0);
        }
    }

    /// <summary>
    /// Parses a standard #RRGGBB hex color string into separate 8-bit RGB color bytes.
    /// </summary>
    /// <param name="hex">Hex color string (e.g. #ff8800).</param>
    /// <returns>A tuple of (R, G, B) byte values.</returns>
    private static (byte R, byte G, byte B) ParseHexColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex[0..2], NumberStyles.HexNumber);
                byte g = byte.Parse(hex[2..4], NumberStyles.HexNumber);
                byte b = byte.Parse(hex[4..6], NumberStyles.HexNumber);
                return (r, g, b);
            }
        }
        catch { }
        return (255, 255, 255);
    }
}
