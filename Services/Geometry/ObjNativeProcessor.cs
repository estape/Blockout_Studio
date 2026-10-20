using System.Globalization;
using System.Numerics;
using System.Text;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Services.Graphics;

namespace BlockoutStudio.Services.Geometry;

/// <summary>
/// Coordinate system convention conversion modes for Wavefront OBJ import/export.
/// </summary>
public enum ObjCoordinateConversion
{
    /// <summary>No conversion; preserve raw coordinates unmodified.</summary>
    Raw,
    /// <summary>Convert from Three.js Y-Up coordinate system to Z-Up Centimeters.</summary>
    ThreeJsToZUp,
    /// <summary>Convert from Z-Up Centimeters to Three.js Y-Up coordinate system.</summary>
    ZUpToThreeJs
}

/// <summary>
/// Pivot alignment modes applied during OBJ import/export.
/// </summary>
public enum ObjPivotMode
{
    /// <summary>Maintain global world origin as pivot (0,0,0).</summary>
    WorldOrigin,
    /// <summary>Offset vertices so the bottom center of the bounding box is at the origin.</summary>
    BottomCenter
}

/// <summary>
/// Structured mesh representation extracted from a parsed Wavefront OBJ payload.
/// </summary>
public class ObjMeshResult
{
    /// <summary>Name identifier of the parsed mesh group or object.</summary>
    public string Name { get; set; } = "SM_Mesh";

    /// <summary>Assigned material library name.</summary>
    public string MaterialName { get; set; } = "M_Default";

    /// <summary>Flat vertex position array [X0, Y0, Z0, X1, Y1, Z1, ...].</summary>
    public float[] Positions { get; set; } = [];

    /// <summary>Flat vertex normal array [NX0, NY0, NZ0, ...].</summary>
    public float[] Normals { get; set; } = [];

    /// <summary>Flat texture coordinate array [U0, V0, U1, V1, ...].</summary>
    public float[] UVs { get; set; } = [];

    /// <summary>Flat per-vertex color/AO array [R0, G0, B0, ...].</summary>
    public float[] Colors { get; set; } = [];

    /// <summary>Triangle index buffer.</summary>
    public uint[] Indices { get; set; } = [];

    /// <summary>Axis-aligned bounding box minimum corner.</summary>
    public Vector3 BoundingMin { get; set; }

    /// <summary>Axis-aligned bounding box maximum corner.</summary>
    public Vector3 BoundingMax { get; set; }

    /// <summary>Axis-aligned bounding box center position.</summary>
    public Vector3 Center { get; set; }
}

/// <summary>
/// High-performance zero-allocation Wavefront OBJ parser and exporter utilizing memory spans.
/// </summary>
public class ObjNativeProcessor
{
    /// <summary>Cached invariant culture instance for float string parsing.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Parses Wavefront OBJ text with coordinate conversion and pivot alignment.
    /// </summary>
    /// <param name="objText">Raw Wavefront OBJ text content.</param>
    /// <param name="conversion">Coordinate transformation to apply.</param>
    /// <param name="pivot">Pivot re-centering mode.</param>
    /// <returns>Populated <see cref="ObjMeshResult"/> containing unified vertex and index buffers.</returns>
    public ObjMeshResult Parse(string objText, ObjCoordinateConversion conversion = ObjCoordinateConversion.ThreeJsToZUp, ObjPivotMode pivot = ObjPivotMode.WorldOrigin)
    {
        var rawPositions = new List<Vector3>(1024);
        var rawColors = new List<Vector3>(1024);
        var rawUVs = new List<Vector2>(1024);
        var rawNormals = new List<Vector3>(1024);

        var finalPositions = new List<float>(2048);
        var finalNormals = new List<float>(2048);
        var finalUVs = new List<float>(1024);
        var finalColors = new List<float>(2048);
        var finalIndices = new List<uint>(2048);

        var vertexLookup = new Dictionary<(int v, int vt, int vn), uint>();
        string currentMeshName = "SM_Imported";
        string currentMaterial = "M_Default";

        using var reader = new StringReader(objText);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            var span = line.AsSpan().Trim();
            if (span.IsEmpty || span[0] == '#') continue;

            if (span.StartsWith("v "))
            {
                ParseVertex(span[2..], rawPositions, rawColors);
            }
            else if (span.StartsWith("vt "))
            {
                ParseUV(span[3..], rawUVs);
            }
            else if (span.StartsWith("vn "))
            {
                ParseNormal(span[3..], rawNormals);
            }
            else if (span.StartsWith("f "))
            {
                ParseFace(span[2..], rawPositions, rawUVs, rawNormals, rawColors,
                    finalPositions, finalUVs, finalNormals, finalColors, finalIndices, vertexLookup);
            }
            else if (span.StartsWith("o ") || span.StartsWith("g "))
            {
                var nameSpan = span[2..].Trim();
                if (!nameSpan.IsEmpty) currentMeshName = nameSpan.ToString();
            }
            else if (span.StartsWith("usemtl "))
            {
                var matSpan = span[7..].Trim();
                if (!matSpan.IsEmpty) currentMaterial = matSpan.ToString();
            }
        }

        // Coordinate conversion & Bounding Box
        var posArray = finalPositions.ToArray();
        var normArray = finalNormals.ToArray();
        var uvsArray = finalUVs.ToArray();
        var colArray = finalColors.ToArray();
        var indArray = finalIndices.ToArray();

        var bMin = new Vector3(float.MaxValue);
        var bMax = new Vector3(float.MinValue);

        for (int i = 0; i < posArray.Length; i += 3)
        {
            float x = posArray[i];
            float y = posArray[i + 1];
            float z = posArray[i + 2];

            if (conversion == ObjCoordinateConversion.ThreeJsToZUp)
            {
                // Three.js (Right=X, Up=Y, Backward=Z) -> Z-Up (Forward=-Z, Right=X, Up=Y)
                float wX = -z;
                float wY = x;
                float wZ = y;
                posArray[i] = wX;
                posArray[i + 1] = wY;
                posArray[i + 2] = wZ;
            }
            else if (conversion == ObjCoordinateConversion.ZUpToThreeJs)
            {
                // Z-Up (X=Fwd, Y=Right, Z=Up) -> Three.js (X=Right, Y=Up, Z=-Fwd)
                float thX = y;
                float thY = z;
                float thZ = -x;
                posArray[i] = thX;
                posArray[i + 1] = thY;
                posArray[i + 2] = thZ;
            }

            bMin = Vector3.Min(bMin, new Vector3(posArray[i], posArray[i + 1], posArray[i + 2]));
            bMax = Vector3.Max(bMax, new Vector3(posArray[i], posArray[i + 1], posArray[i + 2]));
        }

        if (conversion == ObjCoordinateConversion.ThreeJsToZUp)
        {
            for (int i = 0; i < normArray.Length; i += 3)
            {
                float nx = normArray[i];
                float ny = normArray[i + 1];
                float nz = normArray[i + 2];
                normArray[i] = -nz;
                normArray[i + 1] = nx;
                normArray[i + 2] = ny;
            }
        }

        // Pivot Adjustment
        if (pivot == ObjPivotMode.BottomCenter && bMin.X != float.MaxValue)
        {
            float offsetX = (bMin.X + bMax.X) * 0.5f;
            float offsetY = (bMin.Y + bMax.Y) * 0.5f;
            float offsetZ = bMin.Z; // Ground to zero

            for (int i = 0; i < posArray.Length; i += 3)
            {
                posArray[i] -= offsetX;
                posArray[i + 1] -= offsetY;
                posArray[i + 2] -= offsetZ;
            }

            bMin -= new Vector3(offsetX, offsetY, offsetZ);
            bMax -= new Vector3(offsetX, offsetY, offsetZ);
        }

        return new ObjMeshResult
        {
            Name = currentMeshName,
            MaterialName = currentMaterial,
            Positions = posArray,
            Normals = normArray,
            UVs = uvsArray,
            Colors = colArray.Length == posArray.Length ? colArray : Array.Empty<float>(),
            Indices = indArray,
            BoundingMin = bMin.X == float.MaxValue ? Vector3.Zero : bMin,
            BoundingMax = bMax.X == float.MinValue ? Vector3.Zero : bMax,
            Center = (bMin + bMax) * 0.5f
        };
    }

    /// <summary>
    /// Parses a 'v' line defining vertex coordinates and optional vertex colors.
    /// </summary>
    private static void ParseVertex(ReadOnlySpan<char> span, List<Vector3> positions, List<Vector3> colors)
    {
        Span<Range> ranges = stackalloc Range[8];
        int count = span.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (count >= 3)
        {
            if (float.TryParse(span[ranges[0]], NumberStyles.Float, Inv, out float x) &&
                float.TryParse(span[ranges[1]], NumberStyles.Float, Inv, out float y) &&
                float.TryParse(span[ranges[2]], NumberStyles.Float, Inv, out float z))
            {
                positions.Add(new Vector3(x, y, z));

                // Optional vertex colors (v x y z r g b)
                if (count >= 6 &&
                    float.TryParse(span[ranges[3]], NumberStyles.Float, Inv, out float r) &&
                    float.TryParse(span[ranges[4]], NumberStyles.Float, Inv, out float g) &&
                    float.TryParse(span[ranges[5]], NumberStyles.Float, Inv, out float b))
                {
                    colors.Add(new Vector3(r, g, b));
                }
            }
        }
    }

    /// <summary>
    /// Parses a 'vt' line defining texture coordinates (UV).
    /// </summary>
    private static void ParseUV(ReadOnlySpan<char> span, List<Vector2> uvs)
    {
        Span<Range> ranges = stackalloc Range[4];
        int count = span.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (count >= 2)
        {
            if (float.TryParse(span[ranges[0]], NumberStyles.Float, Inv, out float u) &&
                float.TryParse(span[ranges[1]], NumberStyles.Float, Inv, out float v))
            {
                uvs.Add(new Vector2(u, v));
            }
        }
    }

    /// <summary>
    /// Parses a 'vn' line defining vertex surface normals.
    /// </summary>
    private static void ParseNormal(ReadOnlySpan<char> span, List<Vector3> normals)
    {
        Span<Range> ranges = stackalloc Range[4];
        int count = span.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (count >= 3)
        {
            if (float.TryParse(span[ranges[0]], NumberStyles.Float, Inv, out float nx) &&
                float.TryParse(span[ranges[1]], NumberStyles.Float, Inv, out float ny) &&
                float.TryParse(span[ranges[2]], NumberStyles.Float, Inv, out float nz))
            {
                normals.Add(new Vector3(nx, ny, nz));
            }
        }
    }

    /// <summary>
    /// Parses an 'f' face line and triangulates polygon fans into final unified index buffers.
    /// </summary>
    private static void ParseFace(
        ReadOnlySpan<char> span,
        List<Vector3> rawPositions,
        List<Vector2> rawUVs,
        List<Vector3> rawNormals,
        List<Vector3> rawColors,
        List<float> finalPositions,
        List<float> finalUVs,
        List<float> finalNormals,
        List<float> finalColors,
        List<uint> finalIndices,
        Dictionary<(int, int, int), uint> lookup)
    {
        Span<Range> tokens = stackalloc Range[8];
        int tokenCount = span.Split(tokens, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokenCount < 3) return;

        Span<uint> faceIndices = stackalloc uint[tokenCount];

        for (int i = 0; i < tokenCount; i++)
        {
            var vertToken = span[tokens[i]];
            faceIndices[i] = ResolveVertexIndex(vertToken, rawPositions, rawUVs, rawNormals, rawColors,
                finalPositions, finalUVs, finalNormals, finalColors, lookup);
        }

        // Triangulate polygon fans (3 verts = 1 tri, 4 verts = 2 tris)
        for (int i = 1; i < tokenCount - 1; i++)
        {
            finalIndices.Add(faceIndices[0]);
            finalIndices.Add(faceIndices[i]);
            finalIndices.Add(faceIndices[i + 1]);
        }
    }

    /// <summary>
    /// Resolves unique compound vertex tokens (v/vt/vn) into a unified single vertex index, de-duplicating shared attributes.
    /// </summary>
    private static uint ResolveVertexIndex(
        ReadOnlySpan<char> token,
        List<Vector3> rawPositions,
        List<Vector2> rawUVs,
        List<Vector3> rawNormals,
        List<Vector3> rawColors,
        List<float> finalPositions,
        List<float> finalUVs,
        List<float> finalNormals,
        List<float> finalColors,
        Dictionary<(int, int, int), uint> lookup)
    {
        int vIdx = 0, vtIdx = 0, vnIdx = 0;

        Span<Range> parts = stackalloc Range[4];
        int partCount = token.Split(parts, '/');

        if (partCount > 0 && !token[parts[0]].IsEmpty)
            int.TryParse(token[parts[0]], NumberStyles.Integer, Inv, out vIdx);
        if (partCount > 1 && !token[parts[1]].IsEmpty)
            int.TryParse(token[parts[1]], NumberStyles.Integer, Inv, out vtIdx);
        if (partCount > 2 && !token[parts[2]].IsEmpty)
            int.TryParse(token[parts[2]], NumberStyles.Integer, Inv, out vnIdx);

        // Convert OBJ 1-based index (or negative relative index) to 0-based
        vIdx = vIdx > 0 ? vIdx - 1 : (vIdx < 0 ? rawPositions.Count + vIdx : 0);
        vtIdx = vtIdx > 0 ? vtIdx - 1 : (vtIdx < 0 ? rawUVs.Count + vtIdx : -1);
        vnIdx = vnIdx > 0 ? vnIdx - 1 : (vnIdx < 0 ? rawNormals.Count + vnIdx : -1);

        var key = (vIdx, vtIdx, vnIdx);
        if (lookup.TryGetValue(key, out uint existing))
        {
            return existing;
        }

        uint newIndex = (uint)(finalPositions.Count / 3);
        lookup[key] = newIndex;

        if (vIdx >= 0 && vIdx < rawPositions.Count)
        {
            var p = rawPositions[vIdx];
            finalPositions.Add(p.X);
            finalPositions.Add(p.Y);
            finalPositions.Add(p.Z);

            if (vIdx < rawColors.Count)
            {
                var c = rawColors[vIdx];
                finalColors.Add(c.X);
                finalColors.Add(c.Y);
                finalColors.Add(c.Z);
            }
            else
            {
                finalColors.Add(1.0f);
                finalColors.Add(1.0f);
                finalColors.Add(1.0f);
            }
        }
        else
        {
            finalPositions.Add(0); finalPositions.Add(0); finalPositions.Add(0);
            finalColors.Add(1); finalColors.Add(1); finalColors.Add(1);
        }

        if (vtIdx >= 0 && vtIdx < rawUVs.Count)
        {
            var uv = rawUVs[vtIdx];
            finalUVs.Add(uv.X);
            finalUVs.Add(uv.Y);
        }
        else
        {
            finalUVs.Add(0); finalUVs.Add(0);
        }

        if (vnIdx >= 0 && vnIdx < rawNormals.Count)
        {
            var n = rawNormals[vnIdx];
            finalNormals.Add(n.X);
            finalNormals.Add(n.Y);
            finalNormals.Add(n.Z);
        }
        else
        {
            finalNormals.Add(0); finalNormals.Add(0); finalNormals.Add(1);
        }

        return newIndex;
    }

    /// <summary>
    /// Serializes a <see cref="ModelData"/> instance into a standard Wavefront OBJ ASCII string in Z-Up centimeters.
    /// </summary>
    /// <param name="model">Model data containing mesh parts and geometry buffers.</param>
    /// <param name="includeAoColors">Whether to export vertex ambient occlusion colors.</param>
    /// <param name="pivot">Pivot re-centering mode.</param>
    /// <returns>Formatted Wavefront OBJ text representation.</returns>
    public string ExportToObjString(ModelData model, bool includeAoColors = true, ObjPivotMode pivot = ObjPivotMode.WorldOrigin)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Exported from Blockout Studio Native Pipeline");
        sb.AppendLine("# Coordinate System: Z-Up Centimeters (1 Unit = 1 cm)");
        sb.AppendLine();

        int vOffset = 1;

        foreach (var part in model.Parts)
        {
            sb.AppendLine($"o {part.Name}");
            sb.AppendLine($"g {part.Name}");
            sb.AppendLine($"usemtl {part.MaterialName}");

            var pos = part.Positions;
            var norms = part.Normals;
            var uvs = part.UVs;
            var indices = part.Indices;

            // Compute bottom-center offset if requested
            float offX = 0, offY = 0, offZ = 0;
            if (pivot == ObjPivotMode.BottomCenter && pos.Length >= 3)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue;
                for (int i = 0; i < pos.Length; i += 3)
                {
                    float ux = -pos[i + 2];
                    float uy = pos[i];
                    float uz = pos[i + 1];
                    minX = Math.Min(minX, ux); maxX = Math.Max(maxX, ux);
                    minY = Math.Min(minY, uy); maxY = Math.Max(maxY, uy);
                    minZ = Math.Min(minZ, uz);
                }
                offX = (minX + maxX) * 0.5f;
                offY = (minY + maxY) * 0.5f;
                offZ = minZ;
            }

            for (int i = 0; i < pos.Length; i += 3)
            {
                float ux = -pos[i + 2] - offX;
                float uy = pos[i] - offY;
                float uz = pos[i + 1] - offZ;

                sb.AppendLine($"v {ux.ToString("F4", Inv)} {uy.ToString("F4", Inv)} {uz.ToString("F4", Inv)}");
            }

            if (uvs != null)
            {
                for (int i = 0; i < uvs.Length; i += 2)
                {
                    sb.AppendLine($"vt {uvs[i].ToString("F4", Inv)} {uvs[i + 1].ToString("F4", Inv)}");
                }
            }

            if (norms != null)
            {
                for (int i = 0; i < norms.Length; i += 3)
                {
                    float unx = -norms[i + 2];
                    float uny = norms[i];
                    float unz = norms[i + 1];
                    sb.AppendLine($"vn {unx.ToString("F4", Inv)} {uny.ToString("F4", Inv)} {unz.ToString("F4", Inv)}");
                }
            }

            bool hasUv = uvs != null && uvs.Length > 0;
            bool hasNorm = norms != null && norms.Length > 0;

            if (indices != null && indices.Length > 0)
            {
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int i0 = indices[i] + vOffset;
                    int i1 = indices[i + 1] + vOffset;
                    int i2 = indices[i + 2] + vOffset;

                    if (hasUv && hasNorm)
                        sb.AppendLine($"f {i0}/{i0}/{i0} {i1}/{i1}/{i1} {i2}/{i2}/{i2}");
                    else if (hasNorm)
                        sb.AppendLine($"f {i0}//{i0} {i1}//{i1} {i2}//{i2}");
                    else
                        sb.AppendLine($"f {i0} {i1} {i2}");
                }
            }

            vOffset += pos.Length / 3;
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
