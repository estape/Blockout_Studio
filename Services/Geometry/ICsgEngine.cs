namespace UE5BlockoutStudio.Services.Geometry;

public interface ICsgEngine
{
    (float[] positions, float[] normals, float[] uvs, int[] indices) Subtract(
        float[] subPos, float[]? subNorm, float[]? subUV, int[]? subIdx,
        float[] cutPos, float[]? cutNorm, float[]? cutUV, int[]? cutIdx);
}

