using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace WalkManifold.Internals {

  [BurstCompile]
  public struct FindClosestRingJob : IJob {

    [ReadOnly]
    public NativeArray<float3> Vertices;
    [ReadOnly]
    public NativeArray<Ring> Rings;

    public NativeArray<int> ClosestRingIndexResult;

    public float3 Position;

    public void Execute() {
      ClosestRingIndexResult[0] = -1;
      float smallestDistance = float.MaxValue;

      for (int i = 0; i < Rings.Length; i++) {
        var ring = Rings[i];

        float3 boundsMin = float.MaxValue;
        float3 boundsMax = float.MinValue;
        for (int j = 0; j < ring.Count; j++) {
          boundsMin = min(boundsMin, Vertices[ring[j]]);
          boundsMax = max(boundsMax, Vertices[ring[j]]);
        }

        float3 boundsCenter = (boundsMax + boundsMin) / 2;
        float3 boundsExtents = (boundsMin - boundsMin) / 2;

        float distance = length(max(0, abs(Position - boundsCenter) - boundsExtents));
        if (distance < smallestDistance) {
          ClosestRingIndexResult[0] = i;
          smallestDistance = distance;
        }
      }
    }
  }
}
