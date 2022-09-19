using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using static Unity.Mathematics.math;

namespace WalkManifold.Internals {

  [BurstCompile]
  public struct CreatePartialRingsJob : IJob {

    public static readonly bool USE_SORTING_NETWORK = true;
    public static readonly int2[] LocalCoordinates = {
      new int2(0, 0),
      new int2(1, 0),
      new int2(1, 1),
      new int2(0, 1)
    };

    public static readonly PartialRingType[] MaskToPartialTypeLookup = {
      /* 0000 */ PartialRingType.Invalid,
      /* 0001 */ PartialRingType.Corner,
      /* 0010 */ PartialRingType.Corner,
      /* 0011 */ PartialRingType.Edge,
      /* 0100 */ PartialRingType.Corner,
      /* 0101 */ PartialRingType.Diagonal,
      /* 0110 */ PartialRingType.Edge,
      /* 0111 */ PartialRingType.InvertedCorner,
      /* 1000 */ PartialRingType.Corner,
      /* 1001 */ PartialRingType.Edge,
      /* 1010 */ PartialRingType.Diagonal,
      /* 1011 */ PartialRingType.InvertedCorner,
      /* 1100 */ PartialRingType.Edge,
      /* 1101 */ PartialRingType.InvertedCorner,
      /* 1110 */ PartialRingType.InvertedCorner,
      /* 1111 */ PartialRingType.Complete,
    };

    [ReadOnly]
    public NativeArray<float3> Vertices;
    [ReadOnly]
    public NativeHashMap<int2, Pole> CellToPoleIndex;

    public NativeList<Ring> Rings;

    public NativeList<PartialRing> PartialRings;
    public NativeMultiHashMap<int2, int> CellToRingIndices;

    public bool GeneratePartialRings;

    public int2 Min, Max;
    public float LegHeight;

    public void Execute() {
      var poles = new NativeArray<PoleWithCorner>(4, Allocator.Temp);
      var rings = new NativeArray<int>(4, Allocator.Temp);

      for (int x = Min.x; x < Max.x; x++) {
        for (int y = Min.y; y < Max.y; y++) {
          poles[0] = CellToPoleIndex[new int2(x + 0, y + 0)].AtCorner(0);
          poles[1] = CellToPoleIndex[new int2(x + 1, y + 0)].AtCorner(1);
          poles[2] = CellToPoleIndex[new int2(x + 1, y + 1)].AtCorner(2);
          poles[3] = CellToPoleIndex[new int2(x + 0, y + 1)].AtCorner(3);

          while (true) {
            //Use a sorting network to sort the poles in order based on the height of their highest points
            {
              //Sorting network compared to a regular sort is not a super important optimization, but it
              //sure was fun to implement!
              float4 heights = new float4(poles[0].Count == 0 ? float.MinValue : Vertices[poles[0].Start].y,
                                          poles[1].Count == 0 ? float.MinValue : Vertices[poles[1].Start].y,
                                          poles[2].Count == 0 ? float.MinValue : Vertices[poles[2].Start].y,
                                          poles[3].Count == 0 ? float.MinValue : Vertices[poles[3].Start].y);
              float4 indices = new float4(0, 1, 2, 3);

              bool4 compA = heights.xyxy > heights.zwzw;
              heights = select(heights, heights.zwxy, compA);
              indices = select(indices, indices.zwxy, compA);

              bool4 compB = heights.xxzz > heights.yyww;
              heights = select(heights, heights.yxwz, compB);
              indices = select(indices, indices.yxwz, compB);

              bool compC = heights.y > heights.z;
              indices = select(indices, indices.xzyw, compC);

              var p0 = poles[(int)indices.w];
              var p1 = poles[(int)indices.z];
              var p2 = poles[(int)indices.y];
              var p3 = poles[(int)indices.x];

              poles[0] = p0;
              poles[1] = p1;
              poles[2] = p2;
              poles[3] = p3;
            }

            //No more poles left to make rings from!
            if (poles[0].Count == 0) {
              break;
            }

            //We have a list of poles, and we want to try to make a ring using the top
            //vertex of each of the poles.  We start with the pole with the highest vertex,
            //then try to connect it to the next highest vertex, and continue until we
            //have considered all potential poles.

            rings[0] = -1;
            rings[1] = -1;
            rings[2] = -1;
            rings[3] = -1;

            var firstPole = poles[0];
            rings[firstPole.Corner] = firstPole[0];

            float prevHeight = Vertices[firstPole[0]].y;

            //Advance the pole by 1
            firstPole.Start++;
            firstPole.Count--;
            poles[0] = firstPole;

            for (int j = 1; j < 4; j++) {
              var nextPole = poles[j];
              if (nextPole.Count == 0) {
                break;
              }

              float nextHeight = Vertices[nextPole.Start].y;

              //Only consider the next vertex (which will always come below the previously
              //considered vertex) if there is at most LegHeight of distance between them.
              if ((prevHeight - nextHeight) > LegHeight) {
                //We break instead of continue.  Rings are sorted by heights, so all following
                //heights will be even lower and will never satisfy
                break;
              }

              rings[nextPole.Corner] = nextPole.Start;

              //Advance the pole by 1
              nextPole.Start++;
              nextPole.Count--;
              poles[j] = nextPole;

              prevHeight = nextHeight;
            }

            PartialRing partialRing;

            int mask = 0;
            mask |= rings[0] == -1 ? 0 : 0b0001;
            mask |= rings[1] == -1 ? 0 : 0b0010;
            mask |= rings[2] == -1 ? 0 : 0b0100;
            mask |= rings[3] == -1 ? 0 : 0b1000;
            partialRing.Type = MaskToPartialTypeLookup[mask];
            partialRing.Cell = new int2(x, y);

            //Align the ring based on the requirements of the PartialRing structure
            //This makes it easier later to do the reconstruction logic, as partial
            //rings are in an easy-to-reason-with state
            int Start = 0;
            for (int i = 0; i < 4; i++) {
              if (rings[i] >= 0 && rings[(i + 3) & 3] == -1) {
                Start = i;
                break;
              }
            }

            partialRing.V0 = rings[(Start + 0) & 3];
            partialRing.P0 = LocalCoordinates[(Start + 0) & 3];

            partialRing.V1 = rings[(Start + 1) & 3];
            partialRing.P1 = LocalCoordinates[(Start + 1) & 3];

            partialRing.V2 = rings[(Start + 2) & 3];
            partialRing.P2 = LocalCoordinates[(Start + 2) & 3];

            partialRing.V3 = rings[(Start + 3) & 3];
            partialRing.P3 = LocalCoordinates[(Start + 3) & 3];

            //Complete rings don't get added to the partial ring list, just add them into the
            //complete ring list right away.  This has the added benefit that all square rings
            //will always come before any other ring types in the ring list.
            if (partialRing.Type == PartialRingType.Complete) {
              var ring = new Ring() {
                Cell = new int2(x, y)
              };

              ring.Add(partialRing.V0);
              ring.Add(partialRing.V1);
              ring.Add(partialRing.V2);
              ring.Add(partialRing.V3);

              CellToRingIndices.Add(new int2(x, y), Rings.Length);
              Rings.Add(ring);
            } else if (GeneratePartialRings) {
              PartialRings.Add(partialRing);
            }
          }
        }
      }
    }
  }
}
