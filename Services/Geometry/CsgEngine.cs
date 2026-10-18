using System.Numerics;
using System.Text.Json;

namespace UE5BlockoutStudio.Services.Geometry;

public class CsgVertex
{
    public Vector3 Pos;
    public Vector3 Normal;
    public Vector2 UV;

    public CsgVertex(Vector3 pos, Vector3 normal, Vector2 uv)
    {
        Pos = pos;
        Normal = normal;
        UV = uv;
    }

    public CsgVertex Clone() => new(Pos, Normal, UV);

    public void Flip()
    {
        Normal = -Normal;
    }

    public static CsgVertex Interpolate(CsgVertex a, CsgVertex b, float t)
    {
        return new CsgVertex(
            Vector3.Lerp(a.Pos, b.Pos, t),
            Vector3.Normalize(Vector3.Lerp(a.Normal, b.Normal, t)),
            Vector2.Lerp(a.UV, b.UV, t)
        );
    }
}

public class CsgPlane
{
    public const float Epsilon = 1e-4f;
    public Vector3 Normal;
    public float W;

    public CsgPlane(Vector3 normal, float w)
    {
        Normal = normal;
        W = w;
    }

    public static CsgPlane FromPoints(Vector3 a, Vector3 b, Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a);
        if (n.LengthSquared() > 1e-8f)
        {
            n = Vector3.Normalize(n);
        }
        else
        {
            n = Vector3.UnitY;
        }
        return new CsgPlane(n, Vector3.Dot(n, a));
    }

    public CsgPlane Clone() => new(Normal, W);

    public void Flip()
    {
        Normal = -Normal;
        W = -W;
    }

    public void SplitPolygon(
        CsgPolygon polygon,
        List<CsgPolygon> coplanarFront,
        List<CsgPolygon> coplanarBack,
        List<CsgPolygon> front,
        List<CsgPolygon> back)
    {
        const int COPLANAR = 0;
        const int FRONT = 1;
        const int BACK = 2;
        const int SPANNING = 3;

        int polygonType = 0;
        var types = new List<int>();

        for (int i = 0; i < polygon.Vertices.Count; i++)
        {
            float t = Vector3.Dot(Normal, polygon.Vertices[i].Pos) - W;
            int type = (t < -Epsilon) ? BACK : (t > Epsilon) ? FRONT : COPLANAR;
            polygonType |= type;
            types.Add(type);
        }

        switch (polygonType)
        {
            case COPLANAR:
                if (Vector3.Dot(Normal, polygon.Plane.Normal) > 0)
                    coplanarFront.Add(polygon);
                else
                    coplanarBack.Add(polygon);
                break;

            case FRONT:
                front.Add(polygon);
                break;

            case BACK:
                back.Add(polygon);
                break;

            case SPANNING:
                var f = new List<CsgVertex>();
                var b = new List<CsgVertex>();
                for (int i = 0; i < polygon.Vertices.Count; i++)
                {
                    int j = (i + 1) % polygon.Vertices.Count;
                    int ti = types[i];
                    int tj = types[j];
                    var vi = polygon.Vertices[i];
                    var vj = polygon.Vertices[j];

                    if (ti != BACK) f.Add(vi);
                    if (ti != FRONT) b.Add(vi.Clone());

                    if ((ti | tj) == SPANNING)
                    {
                        float t = (W - Vector3.Dot(Normal, vi.Pos)) / Vector3.Dot(Normal, vj.Pos - vi.Pos);
                        var v = CsgVertex.Interpolate(vi, vj, t);
                        f.Add(v);
                        b.Add(v.Clone());
                    }
                }
                if (f.Count >= 3) front.Add(new CsgPolygon(f, polygon.Shared));
                if (b.Count >= 3) back.Add(new CsgPolygon(b, polygon.Shared));
                break;
        }
    }
}

public class CsgPolygon
{
    public List<CsgVertex> Vertices;
    public CsgPlane Plane;
    public int Shared;

    public CsgPolygon(List<CsgVertex> vertices, int shared = 0)
    {
        Vertices = vertices;
        Shared = shared;
        Plane = CsgPlane.FromPoints(vertices[0].Pos, vertices[1].Pos, vertices[2].Pos);
    }

    public CsgPolygon Clone()
    {
        var list = new List<CsgVertex>(Vertices.Count);
        foreach (var v in Vertices) list.Add(v.Clone());
        return new CsgPolygon(list, Shared);
    }

    public void Flip()
    {
        Vertices.Reverse();
        foreach (var v in Vertices) v.Flip();
        Plane.Flip();
    }
}

public class CsgNode
{
    public CsgPlane? Plane;
    public CsgNode? Front;
    public CsgNode? Back;
    public List<CsgPolygon> Polygons = new();

    public CsgNode() { }

    public CsgNode(List<CsgPolygon> polygons)
    {
        Build(polygons);
    }

    public CsgNode Clone()
    {
        var node = new CsgNode();
        node.Plane = Plane?.Clone();
        node.Front = Front?.Clone();
        node.Back = Back?.Clone();
        foreach (var p in Polygons) node.Polygons.Add(p.Clone());
        return node;
    }

    public void Invert()
    {
        foreach (var poly in Polygons) poly.Flip();
        Plane?.Flip();
        Front?.Invert();
        Back?.Invert();
        (Front, Back) = (Back, Front);
    }

    public List<CsgPolygon> ClipPolygons(List<CsgPolygon> list)
    {
        if (Plane == null) return new List<CsgPolygon>(list);

        var f = new List<CsgPolygon>();
        var b = new List<CsgPolygon>();

        foreach (var poly in list)
        {
            Plane.SplitPolygon(poly, f, b, f, b);
        }

        if (Front != null) f = Front.ClipPolygons(f);
        if (Back != null) b = Back.ClipPolygons(b);
        else b.Clear();

        f.AddRange(b);
        return f;
    }

    public void ClipTo(CsgNode bsp)
    {
        Polygons = bsp.ClipPolygons(Polygons);
        Front?.ClipTo(bsp);
        Back?.ClipTo(bsp);
    }

    public List<CsgPolygon> AllPolygons()
    {
        var list = new List<CsgPolygon>(Polygons);
        if (Front != null) list.AddRange(Front.AllPolygons());
        if (Back != null) list.AddRange(Back.AllPolygons());
        return list;
    }

    public void Build(List<CsgPolygon> list)
    {
        if (list.Count == 0) return;

        Plane ??= list[0].Plane.Clone();

        var f = new List<CsgPolygon>();
        var b = new List<CsgPolygon>();

        foreach (var poly in list)
        {
            Plane.SplitPolygon(poly, Polygons, Polygons, f, b);
        }

        if (f.Count > 0)
        {
            Front ??= new CsgNode();
            Front.Build(f);
        }
        if (b.Count > 0)
        {
            Back ??= new CsgNode();
            Back.Build(b);
        }
    }
}

public class CsgEngine : ICsgEngine
{
    (float[] positions, float[] normals, float[] uvs, int[] indices) ICsgEngine.Subtract(
        float[] subPos, float[]? subNorm, float[]? subUV, int[]? subIdx,
        float[] cutPos, float[]? cutNorm, float[]? cutUV, int[]? cutIdx) =>
        Subtract(subPos, subNorm, subUV, subIdx, cutPos, cutNorm, cutUV, cutIdx);

    public static List<CsgPolygon> PolygonsFromMesh(float[] positions, float[]? normals, float[]? uvs, int[]? indices)
    {
        var polygons = new List<CsgPolygon>();
        int triCount = (indices != null && indices.Length > 0) ? indices.Length / 3 : positions.Length / 9;

        for (int i = 0; i < triCount; i++)
        {
            int i0, i1, i2;
            if (indices != null && indices.Length > 0)
            {
                i0 = indices[i * 3];
                i1 = indices[i * 3 + 1];
                i2 = indices[i * 3 + 2];
            }
            else
            {
                i0 = i * 3;
                i1 = i * 3 + 1;
                i2 = i * 3 + 2;
            }

            var p0 = new Vector3(positions[i0 * 3], positions[i0 * 3 + 1], positions[i0 * 3 + 2]);
            var p1 = new Vector3(positions[i1 * 3], positions[i1 * 3 + 1], positions[i1 * 3 + 2]);
            var p2 = new Vector3(positions[i2 * 3], positions[i2 * 3 + 1], positions[i2 * 3 + 2]);

            // Skip degenerate triangles
            var cross = Vector3.Cross(p1 - p0, p2 - p0);
            if (cross.LengthSquared() < 1e-6f) continue;

            var n0 = (normals != null && normals.Length > i0 * 3 + 2) ? new Vector3(normals[i0 * 3], normals[i0 * 3 + 1], normals[i0 * 3 + 2]) : Vector3.Normalize(cross);
            var n1 = (normals != null && normals.Length > i1 * 3 + 2) ? new Vector3(normals[i1 * 3], normals[i1 * 3 + 1], normals[i1 * 3 + 2]) : Vector3.Normalize(cross);
            var n2 = (normals != null && normals.Length > i2 * 3 + 2) ? new Vector3(normals[i2 * 3], normals[i2 * 3 + 1], normals[i2 * 3 + 2]) : Vector3.Normalize(cross);

            var uv0 = (uvs != null && uvs.Length > i0 * 2 + 1) ? new Vector2(uvs[i0 * 2], uvs[i0 * 2 + 1]) : Vector2.Zero;
            var uv1 = (uvs != null && uvs.Length > i1 * 2 + 1) ? new Vector2(uvs[i1 * 2], uvs[i1 * 2 + 1]) : Vector2.Zero;
            var uv2 = (uvs != null && uvs.Length > i2 * 2 + 1) ? new Vector2(uvs[i2 * 2], uvs[i2 * 2 + 1]) : Vector2.Zero;

            polygons.Add(new CsgPolygon(new List<CsgVertex>
            {
                new(p0, n0, uv0),
                new(p1, n1, uv1),
                new(p2, n2, uv2)
            }));
        }

        return polygons;
    }

    public static (float[] positions, float[] normals, float[] uvs, int[] indices) MeshFromPolygons(List<CsgPolygon> polygons)
    {
        var posList = new List<float>();
        var normList = new List<float>();
        var uvList = new List<float>();
        var idxList = new List<int>();

        int vertCounter = 0;

        foreach (var poly in polygons)
        {
            if (poly.Vertices.Count < 3) continue;

            // Convex polygon fan triangulation
            for (int i = 1; i < poly.Vertices.Count - 1; i++)
            {
                var v0 = poly.Vertices[0];
                var v1 = poly.Vertices[i];
                var v2 = poly.Vertices[i + 1];

                // Auto-box UV calculation (100 cm = 1 UV unit)
                var normal = poly.Plane.Normal;
                float nx = MathF.Abs(normal.X);
                float ny = MathF.Abs(normal.Y);
                float nz = MathF.Abs(normal.Z);

                void AddVertex(CsgVertex v)
                {
                    posList.Add(v.Pos.X);
                    posList.Add(v.Pos.Y);
                    posList.Add(v.Pos.Z);

                    normList.Add(normal.X);
                    normList.Add(normal.Y);
                    normList.Add(normal.Z);

                    float u, u_v;
                    if (ny >= nx && ny >= nz)
                    {
                        u = v.Pos.X / 100f;
                        u_v = v.Pos.Z / 100f;
                    }
                    else if (nx >= ny && nx >= nz)
                    {
                        u = v.Pos.Z / 100f;
                        u_v = v.Pos.Y / 100f;
                    }
                    else
                    {
                        u = v.Pos.X / 100f;
                        u_v = v.Pos.Y / 100f;
                    }
                    uvList.Add(u);
                    uvList.Add(u_v);

                    idxList.Add(vertCounter++);
                }

                AddVertex(v0);
                AddVertex(v1);
                AddVertex(v2);
            }
        }

        return (posList.ToArray(), normList.ToArray(), uvList.ToArray(), idxList.ToArray());
    }

    /// <summary>
    /// Performs Boolean Subtraction: Subject - Cutter (e.g. Wall minus Doorway or Wall minus Circular Tube)
    /// </summary>
    public static (float[] positions, float[] normals, float[] uvs, int[] indices) Subtract(
        float[] subPos, float[]? subNorm, float[]? subUV, int[]? subIdx,
        float[] cutPos, float[]? cutNorm, float[]? cutUV, int[]? cutIdx)
    {
        var subjectPolys = PolygonsFromMesh(subPos, subNorm, subUV, subIdx);
        var cutterPolys = PolygonsFromMesh(cutPos, cutNorm, cutUV, cutIdx);

        var a = new CsgNode(subjectPolys);
        var b = new CsgNode(cutterPolys);

        a.Invert();
        a.ClipTo(b);
        b.ClipTo(a);
        b.Invert();
        b.ClipTo(a);
        b.Invert();
        a.Build(b.AllPolygons());
        a.Invert();

        var resultPolys = a.AllPolygons();
        return MeshFromPolygons(resultPolys);
    }
}
