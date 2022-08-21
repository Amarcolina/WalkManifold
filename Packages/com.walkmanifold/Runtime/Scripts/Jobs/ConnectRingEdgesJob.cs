using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace WalkManifold.Internals {

  [BurstCompile]
  public struct ConnectRingEdgesJob : IJob {

    [ReadOnly]
    public NativeArray<Ring> Rings;

    public NativeHashMap<int2, int> EdgeToRing;

    public void Execute() {
      for (int ringI = 0; ringI < Rings.Length; ringI++) {
        var ring = Rings[ringI];

        for (int i = 0; i < ring.Count; i++) {
          EdgeToRing.Add(ring.GetEdge(i), ringI);
        }
      }
    }
  }
}
