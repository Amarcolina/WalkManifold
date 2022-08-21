using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace WalkManifold.Internals {

  [BurstCompile]
  public struct FindClosestPointJob : IJob {

    [ReadOnly]
    public NativeArray<float3> Vertices;
    [ReadOnly]
    public NativeArray<Ring> Rings;

    public NativeArray<ClosestPointResultNative> Result;

    public float3 Position;
    public float CellSize;
    public bool OnlyMarked;
    public int PoleVerticesCount;

    public void Execute() {
      ClosestPointResultNative result = default;
      result.Position = float.MaxValue;

      var positionCell = (int2)floor(Position.xz / CellSize);

      for (int ringI = 0; ringI < Rings.Length; ringI++) {
        var ring = Rings[ringI];
        if (OnlyMarked && !ring.IsMarked) {
          continue;
        }

        if (ring.Cell.Equals(positionCell) &&
            InterpolateRingHeight(ring, Position, out var interiorPoint) &&
            distancesq(interiorPoint.xz, Position.xz) < distancesq(result.Position.xz, Position.xz)) {
          result.Position = interiorPoint;
          result.RingIndex = ringI;
        }

        for (int j = 0; j < ring.Count; j++) {
          var edge = ring.GetEdge(j);

          var v0 = Vertices[edge.x];
          var v1 = Vertices[edge.y];

          float3 point;

          float d = dot(v1 - v0, v1 - v0);
          if (d < Mathf.Epsilon) {
            point = v0;
          } else {
            float t = dot(Position - v0, v1 - v0) / d;
            point = lerp(v0, v1, saturate(t));
          }

          if (distancesq(point.xz, Position.xz) < distancesq(result.Position.xz, Position.xz)) {
            result.Position = point;
            result.RingIndex = ringI;
          }
        }
      }

      result.ClosestPoleVertexIndex = CalculateColliderIndex(Position, Rings[result.RingIndex]);

      Result[0] = result;
    }

    public bool InterpolateRingHeight(Ring ring, float3 position, out float3 output) {
      float3 onRightEdge = default;
      float3 onLeftEdge = default;
      bool hasRightEdge = false;
      bool hasLeftEdge = false;

      for (int i = 0; i < ring.Count; i++) {
        var edge = ring.GetEdge(i);
        var v0 = Vertices[edge.x];
        var v1 = Vertices[edge.y];
        var edgeDir0to1 = v1 - v0;

        if (position.x < min(v0.x, v1.x) || position.x > max(v0.x, v1.x)) {
          continue;
        }

        if (dot(position.xz - v0.xz, new float2(-edgeDir0to1.z, edgeDir0to1.x)) < 0) {
          break;
        }

        if (v0.x < v1.x) {
          onRightEdge = lerp(v0, v1, invlerp(v0.x, v1.x, position.x));
          hasRightEdge = true;
        } else if (v0.x > v1.x) {
          onLeftEdge = lerp(v1, v0, invlerp(v1.x, v0.x, position.x));
          hasLeftEdge = true;
        }
      }

      if (!hasRightEdge || !hasLeftEdge) {
        output = default;
        return false;
      }

      float h = lerp(onRightEdge.y, onLeftEdge.y, invlerp(onRightEdge.z, onLeftEdge.z, position.z));
      output = new float3(position.x, h, position.z);

      return true;
    }

    public int CalculateColliderIndex(float3 position, Ring ring) {
      int bestVertIndex = -1;
      for (int i = 0; i < ring.Count; i++) {
        var vertIndex = ring[i];

        //Only pole vertices have collider info
        if (vertIndex >= PoleVerticesCount) {
          continue;
        }

        var vert = Vertices[vertIndex];
        if (bestVertIndex == -1 ||
            distancesq(vert, position) < distancesq(Vertices[bestVertIndex], position)) {
          bestVertIndex = vertIndex;
        }
      }

      return bestVertIndex;
    }

    public static float invlerp(float a, float b, float value) {
      float d = b - a;
      if (abs(d) < Mathf.Epsilon) {
        return 0;
      } else {
        return (value - a) / d;
      }
    }
  }
}
