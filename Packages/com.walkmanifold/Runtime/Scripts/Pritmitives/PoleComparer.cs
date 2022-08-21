using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace WalkManifold.Internals {

  public struct PoleComparer : IComparer<PoleWithCorner> {

    public NativeArray<float3> Vertices;

    public int Compare(PoleWithCorner a, PoleWithCorner b) {
      if ((a.Count == 0) != (b.Count == 0)) {
        return b.Count.CompareTo(a.Count);
      }

      if (a.Count == 0 && b.Count == 0) {
        return 0;
      }

      return Vertices[b.Start].y.CompareTo(Vertices[a.Start].y);
    }
  }
}
