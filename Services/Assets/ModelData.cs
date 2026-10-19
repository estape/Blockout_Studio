using System.Numerics;

namespace BlockoutStudio.Services.Assets;

public class ModelMeshPart
{
    public string Name { get; set; } = "SM_Part";
    public string MaterialName { get; set; } = "M_Default";
    public float[] Positions { get; set; } = []; // [X, Y, Z, ...]
    public float[]? Normals { get; set; }        // [NX, NY, NZ, ...]
    public float[]? UVs { get; set; }            // [U, V, ...]
    public int[]? Indices { get; set; }          // Triangle indices
    public Vector3 Translation { get; set; } = Vector3.Zero;
    public Vector3 RotationDegrees { get; set; } = Vector3.Zero;
    public Vector3 Scale { get; set; } = Vector3.One;
}

public class ModelData
{
    public string SceneName { get; set; } = "Blockout_Scene";
    public List<ModelMeshPart> Parts { get; set; } = [];
}
