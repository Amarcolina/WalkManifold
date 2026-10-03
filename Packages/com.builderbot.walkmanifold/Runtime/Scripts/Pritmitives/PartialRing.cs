using System;
using Unity.Mathematics;

namespace WalkManifold.Internals {

  /// <summary>
  /// A partial ring is generated during the manifold reconstruction process, and
  /// represents the part of a ring only including the pole vertices. A partial
  /// ring is then transformed into a complete ring, usually through reconstruction
  /// of features such as corners or edges.
  /// </summary>
  [Serializable]
  public struct PartialRing {

    /// <summary>
    /// The vertex indices of the partial ring.  Any unassigned vertex will have a value
    /// of -1.  Vertices are stored such that V0 always contains a set vertex, and V3
    /// never contains a set vertex (unless the Type is Complete).  This aligns the vertices
    /// such that it makes it easier to operate on when doing reconstruction, as the vertices
    /// are always in expected locations.
    /// </summary>
    public int V0, V1, V2, V3;

    //The local corner positions of the vertices, aligned 1-to-1 with the vertex indices.
    //For example if V2 is located at the 0,0 corner of the cell, P2 will contain int2(0,0)
    public int2 P0, P1, P2, P3;

    //The cell coordinate this ring lives at
    public int2 Cell;

    //The type of ring this is, which describes how many vertices are assigned and what shape
    //they are in
    public PartialRingType Type;
  }
}
