using System.Numerics;

namespace BlockoutStudio.Services.Graphics;

/// <summary>
/// Represents a 3D triangle face within the scene geometry used for occlusion testing.
/// </summary>
public struct SceneTriangle
{
    /// <summary>First vertex of triangle.</summary>
    public Vector3 V0;
    /// <summary>Second vertex of triangle.</summary>
    public Vector3 V1;
    /// <summary>Third vertex of triangle.</summary>
    public Vector3 V2;
    /// <summary>Unit face normal vector calculated via cross product.</summary>
    public Vector3 Normal;

    /// <summary>
    /// Initializes a new instance of <see cref="SceneTriangle"/> from three vertices, computing its face normal.
    /// </summary>
    /// <param name="v0">First vertex.</param>
    /// <param name="v1">Second vertex.</param>
    /// <param name="v2">Third vertex.</param>
    public SceneTriangle(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        V0 = v0;
        V1 = v1;
        V2 = v2;
        Normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
    }
}

/// <summary>
/// Multi-threaded CPU ray-marched Ambient Occlusion baker using cosine-weighted hemisphere sampling.
/// </summary>
public static class VertexAoBaker
{
    /// <summary>Precomputed cosine-weighted hemisphere ray sample directions (32 samples).</summary>
    private static readonly Vector3[] HemisphereSamples = GenerateHemisphereSamples(32);

    /// <summary>
    /// Pre-generates stratified cosine-weighted directional samples over an upward hemisphere.
    /// </summary>
    /// <param name="count">Number of ray samples to generate.</param>
    /// <returns>Array of normalized hemisphere direction vectors.</returns>
    private static Vector3[] GenerateHemisphereSamples(int count)
    {
        var samples = new Vector3[count];
        var rand = new Random(42);
        for (int i = 0; i < count; i++)
        {
            float u = (float)rand.NextDouble();
            float v = (float)rand.NextDouble();

            float r = MathF.Sqrt(u);
            float theta = 2.0f * MathF.PI * v;

            float x = r * MathF.Cos(theta);
            float z = r * MathF.Sin(theta);
            float y = MathF.Sqrt(MathF.Max(0.0f, 1.0f - u)); // Upwards hemisphere

            samples[i] = Vector3.Normalize(new Vector3(x, y, z));
        }
        return samples;
    }

    /// <summary>
    /// Fast Möller–Trumbore ray-triangle intersection calculation.
    /// </summary>
    /// <param name="origin">Origin point of casting ray.</param>
    /// <param name="dir">Normalized ray direction vector.</param>
    /// <param name="v0">First vertex of triangle.</param>
    /// <param name="v1">Second vertex of triangle.</param>
    /// <param name="v2">Third vertex of triangle.</param>
    /// <param name="maxDist">Maximum ray horizon distance.</param>
    /// <param name="t">Output hit distance along ray.</param>
    /// <returns>True if the ray intersects the triangle within maxDist; otherwise, false.</returns>
    private static bool RayIntersectsTriangle(
        Vector3 origin, Vector3 dir,
        Vector3 v0, Vector3 v1, Vector3 v2,
        float maxDist, out float t)
    {
        t = 0;
        const float EPSILON = 1e-5f;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 h = Vector3.Cross(dir, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -EPSILON && a < EPSILON) return false;

        float f = 1.0f / a;
        Vector3 s = origin - v0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0f || u > 1.0f) return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(dir, q);

        if (v < 0.0f || u + v > 1.0f) return false;

        float hitT = f * Vector3.Dot(edge2, q);
        if (hitT > EPSILON && hitT < maxDist)
        {
            t = hitT;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Computes Raytraced Vertex Ambient Occlusion in parallel across CPU cores.
    /// </summary>
    /// <param name="positions">Flat array of vertex coordinates [x, y, z, ...].</param>
    /// <param name="normals">Flat array of surface normals.</param>
    /// <param name="sceneTriangles">Collection of all occluding triangles in the scene.</param>
    /// <param name="maxDistance">Occlusion horizon distance in centimeters (default 120cm).</param>
    /// <returns>RGB vertex colors array [r, g, b, r, g, b, ...] matching positions count.</returns>
    public static float[] BakeMeshAo(
        float[] positions,
        float[] normals,
        List<SceneTriangle> sceneTriangles,
        float maxDistance = 120.0f)
    {
        int vertCount = positions.Length / 3;
        float[] colors = new float[positions.Length];

        // Parallel processing across all available CPU cores (.NET 10 SIMD)
        Parallel.For(0, vertCount, i =>
        {
            var pos = new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
            var normal = (normals.Length > i * 3 + 2)
                ? new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2])
                : Vector3.UnitY;

            if (normal.LengthSquared() < 1e-4f) normal = Vector3.UnitY;
            else normal = Vector3.Normalize(normal);

            // Construct tangent space basis around the normal
            Vector3 tangent = MathF.Abs(normal.Y) < 0.99f
                ? Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitY))
                : Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitX));
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            // Offset ray origin slightly along normal to prevent self-intersection
            Vector3 rayOrigin = pos + normal * 0.5f;

            int hitCount = 0;
            int sampleCount = HemisphereSamples.Length;

            for (int s = 0; s < sampleCount; s++)
            {
                var localSample = HemisphereSamples[s];
                // Transform local hemisphere sample to world normal space
                Vector3 worldRayDir = Vector3.Normalize(
                    tangent * localSample.X +
                    normal * localSample.Y +
                    bitangent * localSample.Z);

                for (int t = 0; t < sceneTriangles.Count; t++)
                {
                    var tri = sceneTriangles[t];
                    if (RayIntersectsTriangle(rayOrigin, worldRayDir, tri.V0, tri.V1, tri.V2, maxDistance, out _))
                    {
                        hitCount++;
                        break; // occluded along this ray
                    }
                }
            }

            // Occlusion ratio
            float ao = 1.0f - ((float)hitCount / sampleCount);
            // Ambient curve (keep minimum ambient floor at 0.35 so blocks remain clearly visible in greybox)
            ao = MathF.Pow(ao, 1.25f);
            float finalShade = Math.Clamp(ao * 0.75f + 0.25f, 0.25f, 1.0f);

            colors[i * 3] = finalShade;
            colors[i * 3 + 1] = finalShade;
            colors[i * 3 + 2] = finalShade;
        });

        return colors;
    }
}
