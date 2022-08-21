using UnityEngine;
using Unity.Mathematics;

namespace WalkManifold.Internals {

  public struct ClosestPointResultNative {

    public float3 Position;
    public int RingIndex;
    public int ClosestPoleVertexIndex;
  }
}
