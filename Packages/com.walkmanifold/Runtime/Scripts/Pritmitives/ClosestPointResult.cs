using UnityEngine;
using Unity.Mathematics;

namespace WalkManifold {

  public struct ClosestPointResult {

    public float3 Position;
    public Collider Collider;
    public int RingIndex;
    public int ClosestPoleVertexIndex;
  }

}
