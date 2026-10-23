using System.Numerics;

namespace BlockoutStudio.Models.Assets;

/// <summary>
/// Geometry and spatial transform buffers for a discrete sub-mesh or mesh part.
/// </summary>
public class ModelMeshPart
{
    /// <summary>Sub-mesh part name.</summary>
    public string Name { get; set; } = "SM_Part";

    /// <summary>Assigned material slot name.</summary>
    public string MaterialName { get; set; } = "M_Default";

    /// <summary>Flat vertex position buffer [X, Y, Z, ...].</summary>
    public float[] Positions { get; set; } = [];

    /// <summary>Optional flat normal buffer [NX, NY, NZ, ...].</summary>
    public float[]? Normals { get; set; }

    /// <summary>Optional flat UV coordinate buffer [U, V, ...].</summary>
    public float[]? UVs { get; set; }

    /// <summary>Optional triangle index buffer.</summary>
    public int[]? Indices { get; set; }

    /// <summary>Spatial translation offset.</summary>
    public Vector3 Translation { get; set; } = Vector3.Zero;

    /// <summary>Euler rotation angles in degrees.</summary>
    public Vector3 RotationDegrees { get; set; } = Vector3.Zero;

    /// <summary>Scale vector multiplier.</summary>
    public Vector3 Scale { get; set; } = Vector3.One;
}

/// <summary>
/// Composite model hierarchy containing multiple mesh parts and scene metadata.
/// </summary>
public class ModelData
{
    /// <summary>Logical scene or asset group name.</summary>
    public string SceneName { get; set; } = "Blockout_Scene";

    /// <summary>List of constituent sub-mesh parts.</summary>
    public List<ModelMeshPart> Parts { get; set; } = [];
}


