using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace WalkManifold.Internals {

  [BurstCompile]
  public struct MarkReachableRingsJob : IJob {

    public NativeArray<Ring> Rings;

    [ReadOnly]
    public NativeHashMap<int2, int> EdgeToRing;

    public int StartingRingIndex;

    public void Execute() {
      var ringQueue = new NativeList<int>(16, Allocator.Temp);
      ringQueue.Add(StartingRingIndex);

      var startingRing = Rings[StartingRingIndex];
      startingRing.IsMarked = true;
      Rings[StartingRingIndex] = startingRing;

      while (ringQueue.Length > 0) {
        var ringI = ringQueue[0];
        ringQueue.RemoveAtSwapBack(0);

        var ring = Rings[ringI];

        for (int i = 0; i < ring.Count; i++) {
          if (!EdgeToRing.TryGetValue(ring.GetEdge(i).yx, out var neighborRingI)) {
            continue;
          }

          var neighborRing = Rings[neighborRingI];

          //If the ring has already been marked, skip it
          if (neighborRing.IsMarked) {
            continue;
          }

          //Mark the ring
          neighborRing.IsMarked = true;
          Rings[neighborRingI] = neighborRing;

          //Add the ring into the queue for further exploration
          ringQueue.Add(neighborRingI);
        }
      }
    }
  }
}
