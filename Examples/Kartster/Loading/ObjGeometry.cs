using System.Collections.Generic;
using System.Numerics;

namespace Kartster.Loading;

/// <summary>
/// The positions and triangle indices of one OBJ file, as <see cref="ObjLoader"/> read them. The asset cache shares
/// it between every load of the same file, so it is only read: <see cref="AppendTo"/> copies it into the caller's
/// lists.
/// </summary>
public sealed class ObjGeometry(Vector3[] positions, int[] indices)
{
    //**********************************************************
    //** props
    //**********************************************************

    public int PositionCount => positions.Length;
    public int TriangleCount => indices.Length / 3;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Appends the positions and the indices, moved past the positions already in the list, so several files make one
    /// mesh.
    /// </summary>
    public void AppendTo(List<Vector3> targetPositions, List<int> targetIndices)
    {
        var first = targetPositions.Count;
        targetPositions.AddRange(positions);
        foreach (var index in indices)
            targetIndices.Add(first + index);
    }
}
