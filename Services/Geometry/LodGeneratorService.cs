using System.Diagnostics;
using System.Numerics;

namespace BlockoutStudio.Services.Geometry;

/// <summary>
/// Mesh decimation service generating Level of Detail (LOD) representations using spatial grid clustering.
/// </summary>
public class LodGeneratorService : ILodGeneratorService
{
    /// <inheritdoc/>
    public LodMeshSet GenerateLods(float[] positions, float[] normals, float[]? uvs, int[] indices)
    {
        var sw = Stopwatch.StartNew();
        var result = new LodMeshSet();

        // Ensure valid indices array
        int[] effectiveIndices = (indices != null && indices.Length > 0)
            ? (int[])indices.Clone()
            : GenerateSequentialIndices(positions.Length / 3);

        // LOD0: 100% full original mesh
        result.Lod0 = new LodLevelData
        {
            Level = 0,
            TargetPercentage = 1.0f,
            Positions = (float[])positions.Clone(),
            Normals = (float[])normals.Clone(),
            Uvs = uvs != null ? (float[])uvs.Clone() : null,
            Indices = effectiveIndices,
            ReductionRatio = 1.0f
        };

        // LOD1: Target 50% triangle count
        result.Lod1 = SimplifyMesh(positions, normals, uvs, effectiveIndices, 0.50f, level: 1);

        // LOD2: Target 20% triangle count
        result.Lod2 = SimplifyMesh(positions, normals, uvs, effectiveIndices, 0.20f, level: 2);

        sw.Stop();
        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    /// <inheritdoc/>
    public LodLevelData SimplifyMesh(
        float[] positions,
        float[] normals,
        float[]? uvs,
        int[] indices,
        float targetRatio,
        int level)
    {
        int origVertCount = positions.Length / 3;
        int origTriCount = indices.Length / 3;

        if (origTriCount <= 4 || origVertCount <= 4 || targetRatio >= 0.99f)
        {
            return new LodLevelData
            {
                Level = level,
                TargetPercentage = targetRatio,
                Positions = (float[])positions.Clone(),
                Normals = (float[])normals.Clone(),
                Uvs = uvs != null ? (float[])uvs.Clone() : null,
                Indices = (int[])indices.Clone(),
                ReductionRatio = 1.0f
            };
        }

        // 1. Calculate Bounding Box
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        for (int i = 0; i < origVertCount; i++)
        {
            var p = new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        Vector3 extent = max - min;
        float maxExtent = MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z));
        if (maxExtent < 1e-4f) maxExtent = 1.0f;

        // 2. Perform Grid-Clustering Decimation with adaptive subdivision
        int targetTriangles = Math.Max(2, (int)MathF.Round(origTriCount * targetRatio));

        // Search for optimal grid divisions (divisions between 2 and 64)
        int bestDivisions = 8;
        int minDiv = 2;
        int maxDiv = 48;
        int bestTriDiff = int.MaxValue;
        LodLevelData? bestResult = null;

        for (int iter = 0; iter < 6; iter++)
        {
            int currentDiv = (minDiv + maxDiv) / 2;
            if (currentDiv < 2) currentDiv = 2;

            var candidate = ClusterGrid(positions, normals, uvs, indices, min, extent, currentDiv, level, targetRatio);
            int diff = Math.Abs(candidate.TriangleCount - targetTriangles);

            if (diff < bestTriDiff && candidate.TriangleCount > 0)
            {
                bestTriDiff = diff;
                bestDivisions = currentDiv;
                bestResult = candidate;
            }

            if (candidate.TriangleCount < targetTriangles)
            {
                // Too few triangles -> increase divisions for finer mesh
                minDiv = currentDiv + 1;
            }
            else if (candidate.TriangleCount > targetTriangles)
            {
                // Too many triangles -> decrease divisions for coarser mesh
                maxDiv = currentDiv - 1;
            }
            else
            {
                // Exact match!
                bestResult = candidate;
                break;
            }

            if (minDiv > maxDiv) break;
        }

        // If search didn't yield a valid simplification or produced too few triangles, fallback to initial division
        if (bestResult == null || bestResult.TriangleCount == 0)
        {
            int fallbackDiv = level == 1 ? 8 : 4;
            bestResult = ClusterGrid(positions, normals, uvs, indices, min, extent, fallbackDiv, level, targetRatio);
        }

        // If still empty (e.g. single cell collapse), keep at least original or half indices
        if (bestResult.TriangleCount == 0)
        {
            return new LodLevelData
            {
                Level = level,
                TargetPercentage = targetRatio,
                Positions = (float[])positions.Clone(),
                Normals = (float[])normals.Clone(),
                Uvs = uvs != null ? (float[])uvs.Clone() : null,
                Indices = (int[])indices.Clone(),
                ReductionRatio = 1.0f
            };
        }

        bestResult.ReductionRatio = (float)bestResult.Indices.Length / indices.Length;
        return bestResult;
    }

    /// <summary>
    /// Partitions mesh vertices into a 3D uniform grid and replaces cell clusters with average centroids and normals.
    /// </summary>
    /// <param name="positions">Vertex position buffer.</param>
    /// <param name="normals">Vertex normal buffer.</param>
    /// <param name="uvs">Optional vertex UV buffer.</param>
    /// <param name="indices">Triangle indices.</param>
    /// <param name="min">Mesh AABB minimum boundary.</param>
    /// <param name="extent">Mesh AABB extent dimension vector.</param>
    /// <param name="divisions">Number of spatial cell subdivisions per axis.</param>
    /// <param name="level">Current LOD level being simplified.</param>
    /// <param name="targetRatio">Target triangle ratio.</param>
    /// <returns>Cluster-simplified <see cref="LodLevelData"/>.</returns>
    private static LodLevelData ClusterGrid(
        float[] positions,
        float[] normals,
        float[]? uvs,
        int[] indices,
        Vector3 min,
        Vector3 extent,
        int divisions,
        int level,
        float targetRatio)
    {
        int origVertCount = positions.Length / 3;
        float invDivX = divisions / MathF.Max(extent.X, 1e-4f);
        float invDivY = divisions / MathF.Max(extent.Y, 1e-4f);
        float invDivZ = divisions / MathF.Max(extent.Z, 1e-4f);

        // Map original vertex to cell key
        var vertToCluster = new int[origVertCount];
        var cellToClusterId = new Dictionary<long, int>();
        var clusterCentroids = new List<Vector3>();
        var clusterNormals = new List<Vector3>();
        var clusterUvs = uvs != null ? new List<Vector2>() : null;
        var clusterCounts = new List<int>();

        for (int i = 0; i < origVertCount; i++)
        {
            float px = positions[i * 3];
            float py = positions[i * 3 + 1];
            float pz = positions[i * 3 + 2];

            int cx = Math.Clamp((int)((px - min.X) * invDivX), 0, divisions - 1);
            int cy = Math.Clamp((int)((py - min.Y) * invDivY), 0, divisions - 1);
            int cz = Math.Clamp((int)((pz - min.Z) * invDivZ), 0, divisions - 1);

            long cellKey = ((long)cx << 32) | ((long)cy << 16) | (long)cz;

            if (!cellToClusterId.TryGetValue(cellKey, out int clusterId))
            {
                clusterId = clusterCentroids.Count;
                cellToClusterId[cellKey] = clusterId;

                clusterCentroids.Add(new Vector3(px, py, pz));
                var n = (normals.Length > i * 3 + 2)
                    ? new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2])
                    : Vector3.UnitY;
                clusterNormals.Add(n);

                if (clusterUvs != null)
                {
                    var uv = (uvs!.Length > i * 2 + 1)
                        ? new Vector2(uvs[i * 2], uvs[i * 2 + 1])
                        : Vector2.Zero;
                    clusterUvs.Add(uv);
                }

                clusterCounts.Add(1);
            }
            else
            {
                clusterCentroids[clusterId] += new Vector3(px, py, pz);
                if (normals.Length > i * 3 + 2)
                {
                    clusterNormals[clusterId] += new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]);
                }
                if (clusterUvs != null && uvs!.Length > i * 2 + 1)
                {
                    clusterUvs[clusterId] += new Vector2(uvs[i * 2], uvs[i * 2 + 1]);
                }
                clusterCounts[clusterId]++;
            }

            vertToCluster[i] = clusterId;
        }

        // Average cluster attributes
        int clusterCount = clusterCentroids.Count;
        var newPositions = new float[clusterCount * 3];
        var newNormals = new float[clusterCount * 3];
        var newUvs = clusterUvs != null ? new float[clusterCount * 2] : null;

        for (int c = 0; c < clusterCount; c++)
        {
            float count = clusterCounts[c];
            var avgPos = clusterCentroids[c] / count;
            var avgNorm = clusterNormals[c] / count;
            if (avgNorm.LengthSquared() > 1e-6f) avgNorm = Vector3.Normalize(avgNorm);
            else avgNorm = Vector3.UnitY;

            newPositions[c * 3] = avgPos.X;
            newPositions[c * 3 + 1] = avgPos.Y;
            newPositions[c * 3 + 2] = avgPos.Z;

            newNormals[c * 3] = avgNorm.X;
            newNormals[c * 3 + 1] = avgNorm.Y;
            newNormals[c * 3 + 2] = avgNorm.Z;

            if (newUvs != null)
            {
                var avgUv = clusterUvs![c] / count;
                newUvs[c * 2] = avgUv.X;
                newUvs[c * 2 + 1] = avgUv.Y;
            }
        }

        // Remap triangles and eliminate degenerate / duplicate triangles
        var newIndicesList = new List<int>();
        var seenTriangles = new HashSet<(int, int, int)>();

        int origTriCount = indices.Length / 3;
        for (int t = 0; t < origTriCount; t++)
        {
            int i0 = indices[t * 3];
            int i1 = indices[t * 3 + 1];
            int i2 = indices[t * 3 + 2];

            int c0 = vertToCluster[i0];
            int c1 = vertToCluster[i1];
            int c2 = vertToCluster[i2];

            // Collapse check: degenerate triangle
            if (c0 == c1 || c1 == c2 || c0 == c2) continue;

            // Normalized triangle tuple to prevent duplicates
            int minC = Math.Min(c0, Math.Min(c1, c2));
            int maxC = Math.Max(c0, Math.Max(c1, c2));
            int midC = c0 + c1 + c2 - minC - maxC;
            var triKey = (minC, midC, maxC);

            if (!seenTriangles.Add(triKey)) continue;

            newIndicesList.Add(c0);
            newIndicesList.Add(c1);
            newIndicesList.Add(c2);
        }

        // Compact only active referenced vertices
        var (compactPos, compactNorm, compactUv, compactIdx) = CompactMesh(
            newPositions, newNormals, newUvs, newIndicesList.ToArray());

        return new LodLevelData
        {
            Level = level,
            TargetPercentage = targetRatio,
            Positions = compactPos,
            Normals = compactNorm,
            Uvs = compactUv,
            Indices = compactIdx,
            ReductionRatio = (float)compactIdx.Length / indices.Length
        };
    }

    /// <summary>
    /// Compacts the vertex buffer by removing unused/unreferenced clustered vertices and rebinding triangle indices.
    /// </summary>
    /// <param name="positions">Flat position buffer.</param>
    /// <param name="normals">Flat normal buffer.</param>
    /// <param name="uvs">Optional flat UV buffer.</param>
    /// <param name="indices">Triangle indices.</param>
    /// <returns>Compacted position, normal, UV, and index arrays.</returns>
    private static (float[] Pos, float[] Norm, float[]? Uv, int[] Idx) CompactMesh(
        float[] positions,
        float[] normals,
        float[]? uvs,
        int[] indices)
    {
        var oldToNew = new Dictionary<int, int>();
        var newPositions = new List<float>();
        var newNormals = new List<float>();
        var newUvs = uvs != null ? new List<float>() : null;
        var newIndices = new int[indices.Length];

        for (int i = 0; i < indices.Length; i++)
        {
            int oldIdx = indices[i];
            if (!oldToNew.TryGetValue(oldIdx, out int newIdx))
            {
                newIdx = newPositions.Count / 3;
                oldToNew[oldIdx] = newIdx;

                newPositions.Add(positions[oldIdx * 3]);
                newPositions.Add(positions[oldIdx * 3 + 1]);
                newPositions.Add(positions[oldIdx * 3 + 2]);

                newNormals.Add(normals[oldIdx * 3]);
                newNormals.Add(normals[oldIdx * 3 + 1]);
                newNormals.Add(normals[oldIdx * 3 + 2]);

                if (newUvs != null && uvs != null && uvs.Length > oldIdx * 2 + 1)
                {
                    newUvs.Add(uvs[oldIdx * 2]);
                    newUvs.Add(uvs[oldIdx * 2 + 1]);
                }
            }
            newIndices[i] = newIdx;
        }

        return (
            newPositions.ToArray(),
            newNormals.ToArray(),
            newUvs?.ToArray(),
            newIndices
        );
    }

    /// <summary>
    /// Generates sequential 0-based indices for an unindexed vertex list.
    /// </summary>
    /// <param name="vertexCount">Total number of vertices.</param>
    /// <returns>Integer array [0, 1, 2, ..., vertexCount - 1].</returns>
    private static int[] GenerateSequentialIndices(int vertexCount)
    {
        var indices = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++) indices[i] = i;
        return indices;
    }
}
