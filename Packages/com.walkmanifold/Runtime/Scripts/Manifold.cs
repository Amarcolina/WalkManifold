using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using static Unity.Mathematics.math;

namespace WalkManifold {
  using Internals;

  /// <summary>
  /// The Manifold is used to generate walkable surface information about the current Unity scene.  It
  /// runs physics queries and generate the walkable surface data structure from the results.  It provides
  /// a number of methods to control how the surface is generated, and to query the resulting surface.
  /// 
  /// The walkable surface is represented as a mesh, made up of vertices and polygons.  The polygons are
  /// called 'rings' and are organized into a grid shape.  The resolution of this grid is directly controlled
  /// by the CellSize in the Settings property.  The surface mesh is made of polygons and not triangles or
  /// quads, as each ring can have a different number of vertices depending on where it lies on the surface
  /// mesh.  Rings near the edge of the mesh can have more or less than four vertices to better fit the
  /// shape of the walkable surface.
  /// 
  /// When the Manifold is Updated, only a subset of the entire walkable surface is generated.
  /// </summary>
  public class Manifold {

    /// <summary>
    /// The current settings used to generate the walkable surface.  Can be changed in between calls to
    /// Update.
    /// </summary>
    public ManifoldSettings Settings { get; set; }

    /// <summary>
    /// The list of vertices in the walkable surface mesh.  This list is comprised of 'pole vertices',
    /// which are vertices that lie exactly on grid coordinates, and 'edge vertices', which are vertices
    /// that were generated near the edges of the mesh to smooth out the border.  All 'pole vertices'
    /// are stored before all 'edge vertices' in this list.
    /// </summary>
    public NativeList<float3> Vertices;

    /// <summary>
    /// The list of colliders that were used to generate the 'pole vertices' during the update of
    /// the walkable surface mesh.  There are exactly the same number of colliders in this list
    /// as there are 'pole vertices' in the Vertices list, and they are in the same order.  You
    /// can use this list to assign a collider to a ring.
    /// </summary>
    public List<Collider> VerticesColliders = new List<Collider>();

    /// <summary>
    /// The number of 'pole vertices' that were generated during a walkable surface update.
    /// </summary>
    public int PoleVerticesCount;

    /// <summary>
    /// The list of all rings that were generated during the walkable surface update.  A ring in
    /// this case contains some ring metadata, like the cell it lives in, as well as a slice into
    /// the RingData list.
    /// </summary>
    public NativeList<Ring> Rings;

    /// <summary>
    /// Maps a mesh edge to the ring that contains that edge. You can reverse the edge direction
    /// to find the connecting ring along an adjacent edge.
    /// </summary>
    public NativeHashMap<int2, int> EdgeToRing;

    /// <summary>
    /// Maps a given cell to all of the rings that exist inside that cell.  The mapped value is the
    /// index of the ring in the Rings list.
    /// </summary>
    public NativeMultiHashMap<int2, int> CellToRingIndices;

    /// <summary>
    /// Maps a given pole coordinate to the pole structure that was generated at that location.  A pole
    /// is just a slice view into the Vertices list of all vertices that are part of that Pole.
    /// </summary>
    public NativeHashMap<int2, Pole> CellToPole;

    /// <summary>
    /// Will return false after this class is disposed.
    /// </summary>
    public bool IsCreated => Vertices.IsCreated;

    /// <summary>
    /// Constructs a new Manifold instance using the provided Settings.  A call to Update is not affected
    /// by any previous calls to Update, the Update is always atemporal.  As such, there is no need to
    /// construct a new Manifold class with different settings, you can simply change the Settings property
    /// before Update, allowing you to re-use the Manifold instance for different use-cases if you desire.
    /// </summary>
    public Manifold(ManifoldSettings settings) {
      Settings = settings;

      Vertices = new NativeList<float3>(256, Allocator.Persistent);
      Rings = new NativeList<Ring>(256, Allocator.Persistent);

      EdgeToRing = new NativeHashMap<int2, int>(256, Allocator.Persistent);
      CellToRingIndices = new NativeMultiHashMap<int2, int>(256, Allocator.Persistent);
      CellToPole = new NativeHashMap<int2, Pole>(256, Allocator.Persistent);
      _trueEdgeCache = new NativeHashMap<int3, int>(256, Allocator.Persistent);
    }

    /// <summary>
    /// Disposes the instance, freeing the internal resources.
    /// </summary>
    public void Dispose() {
      Vertices.Dispose();
      Rings.Dispose();

      EdgeToRing.Dispose();
      CellToRingIndices.Dispose();
      CellToPole.Dispose();
      _trueEdgeCache.Dispose();

      VerticesColliders.Clear();
      VerticesColliders = null;
    }

    /// <summary>
    /// Clears the current surface.  Can be done if you are going to manually do an update
    /// to the surface rather than using the Update method.
    /// </summary>
    public void Clear() {
      CurrentUpdateStep = UpdateStep.Cleared;

      Vertices.Clear();
      Rings.Clear();

      EdgeToRing.Clear();
      CellToRingIndices.Clear();
      CellToPole.Clear();
      _trueEdgeCache.Clear();

      VerticesColliders.Clear();
    }

    /// <summary>
    /// Returns the cell coordinate for a provided 3d world coordinate, given the
    /// current Settings.
    /// </summary>
    public int2 GetCell(float3 pos) {
      return (int2)floor(pos.xz / _settings.CellSize);
    }

    /// <summary>
    /// Returns whether or not the two vertices form an edge that is shared by two rings. Returns
    /// true only if there are two rings that both utilize this edge. Can be used to create edge
    /// effects in a visualization mesh.
    /// </summary>
    public bool IsSharedEdge(int v0, int v1) {
      return EdgeToRing.ContainsKey(new int2(v0, v1)) && EdgeToRing.ContainsKey(new int2(v1, v0));
    }

    #region UPDATE

    /// <summary>
    /// Returns the current Update step, which can be used if you are doing a manual
    /// Update rather than using the built-in Update method.
    /// </summary>
    public UpdateStep CurrentUpdateStep { get; private set; }

    /// <summary>
    /// Returns whether or not the Manifold is currently in the middle of an update.
    /// If this is true, then certain operations like updating the settings cannot take
    /// place.
    /// </summary>
    public bool IsUpdating => CurrentUpdateStep != UpdateStep.Cleared && CurrentUpdateStep != UpdateStep.Complete;

    /// <summary>
    /// Update the walkable surface given the target bounds.  The resulting surface mesh
    /// is guaranteed to be valid for every point inside of the bounds, but might be
    /// missing at any point outside of the bounds.
    /// </summary>
    public void Update(Bounds bounds) {
      Update(bounds.min, bounds.max);
    }

    /// <summary>
    /// Update the walkable surface given the target bounds.  The resulting surface mesh
    /// is guaranteed to be valid for every point inside of the bounds, but might be
    /// missing at any point outside of the bounds.
    /// </summary>
    public void Update(float3 boundsMin, float3 boundsMax) {
      int2 minCell = GetCell(boundsMin);
      int2 maxCell = GetCell(boundsMax) + 1;

      Update(minCell, maxCell, boundsMin.y, boundsMax.y);
    }

    /// <summary>
    /// Update the walkable surface given the precides minimum and maximum cell indices,
    /// plus the minimum and maximum floor heights.  The resulting surface will cover every
    /// specified cell, from the minimum (inclusive) to the maxumum (exclusive).  The
    /// surface will also contain all valid surfaces between the min and max floor height.
    /// </summary>
    public void Update(int2 updateRangeMin, int2 updateRangeMax, float floorMin, float floorMax) {
      using (new ProfilerScope("Manifold.Update")) {
        if (Settings == null) {
          throw new InvalidOperationException("Cannot Update a Manifold while it has an unassigned Settings property.");
        }
        _settings = Settings.GetValueType();

        if (!Physics.autoSyncTransforms && Settings.SyncPhysicsOnUpdate) {
          using (new ProfilerScope("Sync Transforms")) {
            Physics.SyncTransforms();
          }
        }

        Clear();

        PartialUpdateCreatePoles(updateRangeMin, updateRangeMax + 1, floorMin, floorMax);

        var partialRings = new NativeList<PartialRing>(32, Allocator.TempJob);
        PartialUpdateCreatePartialRings(updateRangeMin, updateRangeMax, partialRings);

        PartialUpdateReconstructRings(partialRings.AsArray());
        partialRings.Dispose();

        PartialUpdateConnectEdges();
      }
    }

    /// <summary>
    /// Identical to the Update method, but performed in an async way.
    /// 
    /// IMPORTANT:  Because this method gathers information over multiple frames, any changes
    /// to the colliders in the scene frame-to-frame might cause the resulting surface to
    /// be skewed or incorrect.  This should only generally be used for a visualization purpose
    /// where you need to generate large surfaces, or if you can guarantee that there will be
    /// no changes to the colliders in between frames while this is updating.
    /// </summary>
    public async Task UpdateAsync(int2 updateRangeMin, int2 updateRangeMax,
                                  float floorMin, float floorMax,
                                  int cellBatchSize = 48,
                                  CancellationToken token = default) {
      if (Settings == null) {
        throw new InvalidOperationException("Cannot Update a Manifold while it has an unassigned Settings property.");
      }
      _settings = Settings.GetValueType();
      var partialRings = new NativeList<PartialRing>(32, Allocator.Persistent);

      try {
        if (!Physics.autoSyncTransforms && Settings.SyncPhysicsOnUpdate) {
          using (new ProfilerScope("Sync Transforms")) {
            Physics.SyncTransforms();
          }
        }

        Clear();

        for (int x = updateRangeMin.x; x <= updateRangeMax.x; x += cellBatchSize) {
          for (int y = updateRangeMin.y; y <= updateRangeMax.y; y += cellBatchSize) {
            if (x != updateRangeMin.x || y != updateRangeMin.y) {
              await Task.Yield();
              if (token.IsCancellationRequested) {
                if (IsCreated) {
                  Clear();
                }
                return;
              }
            }

            int2 chunkMin = new int2(x, y);
            int2 chunkMax = min(updateRangeMax + 1, chunkMin + cellBatchSize);

            int2 size = chunkMax - chunkMin;
            PartialUpdateCreatePoles(chunkMin, chunkMax, floorMin, floorMax);
          }
        }

        //Don't do partial ring creation async for the built-in async method,
        //it's so fast that it shouldn't be a big deal
        PartialUpdateCreatePartialRings(updateRangeMin, updateRangeMax, partialRings);

        int sliceBatchSize = max(1, cellBatchSize * cellBatchSize / (1 + _settings.ReconstructionIterations));
        for (int i = 0; i < partialRings.Length; i += sliceBatchSize) {
          if (i != 0) {
            await Task.Yield();
            if (token.IsCancellationRequested) {
              if (IsCreated) {
                Clear();
              }
              return;
            }
          }

          int endIndex = min(partialRings.Length, i + sliceBatchSize);
          PartialUpdateReconstructRings(partialRings.AsArray().Slice(i, endIndex - i));
        }

        PartialUpdateConnectEdges();
      } catch (Exception e) {
        Debug.LogException(e);
        throw e;
      } finally {
        partialRings.Dispose();
      }
    }

    /// <summary>
    /// You can use the Partial Update methods to manually trigger an Update in a custom way specific
    /// to your needs.
    /// 
    /// This is the First step of an Update, and this method can be called multiple times with
    /// different non-overlapping regions to generate multiple poles.  The floor min/max is not used
    /// when considering whether or not two regions are overlapping, for any given pole coordinate,
    /// it must be created only once.
    /// </summary>
    public void PartialUpdateCreatePoles(int2 updateRangeMin, int2 updateRangeMax, float floorMin, float floorMax) {
      using (new ProfilerScope("Create Poles")) {
        AssertUpdateOrder(UpdateStep.CreatePoles);

        for (int x = updateRangeMin.x; x < updateRangeMax.x; x++) {
          for (int y = updateRangeMin.y; y < updateRangeMax.y; y++) {
            CreatePole(new int2(x, y), floorMin, floorMax);
          }
        }

        PoleVerticesCount = Vertices.Length;
      }
    }

    /// <summary>
    /// You can use the Partial Update methods to manually trigger an Update in a custom way specific
    /// to your needs.
    /// 
    /// This is the Second step of an Update, and this method can be called multiple times with
    /// different non-overlapping regions to generate all rings for the surface.  All generated rings
    /// will be appended to the provided list, which will be used in later steps.
    /// </summary>
    public void PartialUpdateCreatePartialRings(int2 updateRangeMin, int2 updateRangeMax, NativeList<PartialRing> partialRings) {
      using (new ProfilerScope("Create Partial Rings")) {
        AssertUpdateOrder(UpdateStep.CreatePartialRings);

        new CreatePartialRingsJob() {
          Vertices = Vertices.AsArray(),
          CellToPoleIndex = CellToPole,
          Rings = Rings,
          PartialRings = partialRings,
          CellToRingIndices = CellToRingIndices,
          GeneratePartialRings = _settings.EdgeReconstruction,
          Min = updateRangeMin,
          Max = updateRangeMax,
          LegHeight = _settings.StepHeight
        }.Run();
      }
    }

    /// <summary>
    /// You can use the Partial Update methods to manually trigger an Update in a custom way specific
    /// to your needs.
    /// 
    /// This is the Third step of an Update, and this method can be called multiple times with
    /// different non-overlapping slices of the partial rings generated during step two.  This
    /// will populate the internal Rings structure with the generated rings.
    /// </summary>
    public void PartialUpdateReconstructRings(NativeSlice<PartialRing> partialRings) {
      using (new ProfilerScope("Reconstruct Rings")) {
        AssertUpdateOrder(UpdateStep.ReconstructRings);

        for (int i = 0; i < partialRings.Length; i++) {
          var partialRing = partialRings[i];

          Ring ring = new Ring() {
            Cell = partialRing.Cell,
            IsMarked = false,
            Count = 0
          };

          switch (partialRing.Type) {
            case PartialRingType.Corner:
              CompleteRingCorner(ref ring, partialRing);
              break;
            case PartialRingType.Edge:
              CompleteRingEdge(ref ring, partialRing);
              break;
            case PartialRingType.InvertedCorner:
              CompleteRingInvertedCorner(ref ring, partialRing);
              break;
            case PartialRingType.Diagonal:
              CompleteRingDiagonal(ref ring, partialRing);
              break;
            default:
              throw new Exception();
          }

          CellToRingIndices.Add(ring.Cell, Rings.Length);
          Rings.Add(ring);
        }
      }
    }

    /// <summary>
    /// You can use the Partial Update methods to manually trigger an Update in a custom way specific
    /// to your needs.
    /// 
    /// This is the Fourth and final step of an Update, and can only be called once.  This generates
    /// the edge connectivity structure that allows rings to find their neighbors.
    /// </summary>
    public void PartialUpdateConnectEdges() {
      using (new ProfilerScope("Connect Edges")) {
        AssertUpdateOrder(UpdateStep.ConnectEdges);

        new ConnectRingEdgesJob() {
          Rings = Rings,
          EdgeToRing = EdgeToRing
        }.Run();

        CurrentUpdateStep = UpdateStep.Complete;
      }
    }

    public void AssertHasBeenUpdated(string methodName) {
      if (CurrentUpdateStep != UpdateStep.Complete) {
        throw new InvalidOperationException($"{methodName}() cannot be called until the Manifold " +
                                             "has been updated with Update, or a Partial update " +
                                             "has been completed.");
      }
    }

    public void AssertUpdateOrder(UpdateStep step) {
      if (CurrentUpdateStep > step) {
        throw new InvalidOperationException("Incorrect partial update order.  Calls must be made in the following order:\n" +
                                            $"{nameof(Clear)}\n" +
                                            $"{nameof(PartialUpdateCreatePoles)}\n" +
                                            $"{nameof(PartialUpdateCreatePartialRings)}\n" +
                                            $"{nameof(PartialUpdateReconstructRings)}\n" +
                                            $"{nameof(PartialUpdateConnectEdges)}");
      }
      CurrentUpdateStep = step;
    }

    public enum UpdateStep {
      Cleared,
      CreatePoles,
      CreatePartialRings,
      ReconstructRings,
      ConnectEdges,
      Complete
    }

    #endregion

    #region QUERY

    /// <summary>
    /// Given a world space position, find the index of the closest ring on the generated
    /// walk surface mesh.
    /// </summary>
    public int FindClosestRingIndex(float3 position) {
      AssertHasBeenUpdated(nameof(FindClosestRingIndex));

      var resultArr = new NativeArray<int>(1, Allocator.TempJob);

      new FindClosestRingJob() {
        Vertices = Vertices,
        Rings = Rings,
        Position = position,
        ClosestRingIndexResult = resultArr
      }.Run();

      var result = resultArr[0];
      resultArr.Dispose();

      return result;
    }

    /// <summary>
    /// Given a world space position, find the closest point on the generated walk surface.
    /// Can also specify to only consider marked rings.
    /// </summary>
    public ClosestPointResult FindClosestPoint(float3 position, bool onlyMarked = false) {
      AssertHasBeenUpdated(nameof(FindClosestPoint));

      var resultArr = new NativeArray<ClosestPointResultNative>(1, Allocator.TempJob);

      new FindClosestPointJob() {
        Vertices = Vertices,
        Rings = Rings,
        CellSize = _settings.CellSize,
        PoleVerticesCount = PoleVerticesCount,
        Position = position,
        OnlyMarked = onlyMarked,
        Result = resultArr,
      }.Run();

      var result = resultArr[0];
      resultArr.Dispose();

      return new ClosestPointResult() {
        Position = result.Position,
        Collider = VerticesColliders[result.ClosestPoleVertexIndex],
        ClosestPoleVertexIndex = result.ClosestPoleVertexIndex,
        RingIndex = result.RingIndex
      };
    }

    /// <summary>
    /// Given the index of a ring, mark it, and also mark any ring that is connected
    /// topologically to it.
    /// </summary>
    public void MarkReachable(int startingRingIndex) {
      AssertHasBeenUpdated(nameof(MarkReachable));

      new MarkReachableRingsJob() {
        Rings = Rings,
        EdgeToRing = EdgeToRing,
        StartingRingIndex = startingRingIndex
      }.Run();
    }

    #endregion

    #region IMPLEMENTATION

    private ManifoldSettings.ValueType _settings;

    private NativeHashMap<int3, int> _trueEdgeCache;

    private void CreatePole(int2 polePos, float floorMin, float floorMax) {
      if (CellToPole.ContainsKey(polePos)) {
        throw new InvalidOperationException("Can only call CreatePole once for a given pole position before Clearing the Manifold.");
      }

      Pole pole = new Pole() {
        Start = Vertices.Length,
        Count = 0
      };

      Vector3 rayOrigin = new Vector3(polePos.x, 0, polePos.y) * _settings.CellSize +
                          Vector3.up * floorMax;

      while (true) {
        if (rayOrigin.y < floorMin) {
          break;
        }

        //Move the ray up by the step height, to make sure we hit
        //any colliders near the walk surface.  The capsule check
        //will miss any surfaces in this space.
        rayOrigin.y += _settings.StepHeight;

        if (!Physics.Raycast(rayOrigin,
                             Vector3.down,
                             out var hit,
                             rayOrigin.y - floorMin,
                             _settings.RelevantLayers, 
                             QueryTriggerInteraction.Ignore)) {
          break;
        }

        rayOrigin.y = hit.point.y - _settings.AgentHeight;

        //If the distance is less than the step height, then the
        //hit position is too close to ceiling, just discard
        if (hit.distance < _settings.StepHeight) {
          continue;
        }

        //Also discard any surface with too steep of a surface
        if (hit.normal.y < _settings.SurfaceNormalYThreshold) {
          continue;
        }

        //Also discard if the point is not actually a walkable collider
        if ((_settings.WalkableLayers & (1 << hit.collider.gameObject.layer)) == 0) {
          continue;
        }

        //Also discard any surface that can't fit the agent capsule if they
        //stood on this point
        if (Physics.CheckCapsule(hit.point + _settings.CapsuleOffsetA,
                                 hit.point + _settings.CapsuleOffsetB,
                                 _settings.AgentRadius,
                                 _settings.RelevantLayers,
                                 QueryTriggerInteraction.Ignore)) {
          continue;
        }

        pole.Count++;
        Vertices.Add(hit.point);
        VerticesColliders.Add(hit.collider);
      }

      CellToPole[polePos] = pole;
    }

    private void CompleteRingDiagonal(ref Ring ring, PartialRing partialRing) {
      int edge01 = GenerateTrueEdge(partialRing.V0, partialRing.P1 - partialRing.P0);
      int edge03 = GenerateTrueEdge(partialRing.V0, partialRing.P3 - partialRing.P0);

      int edge21 = GenerateTrueEdge(partialRing.V2, partialRing.P1 - partialRing.P2);
      int edge23 = GenerateTrueEdge(partialRing.V2, partialRing.P3 - partialRing.P2);

      ring.Add(partialRing.V0);
      ring.Add(edge01);
      ring.Add(edge21);
      ring.Add(partialRing.V2);
      ring.Add(edge23);
      ring.Add(edge03);
    }

    private void CompleteRingEdge(ref Ring ring, PartialRing partialRing) {
      partialRing.V3 = GenerateTrueEdge(partialRing.V0, partialRing.P3 - partialRing.P0);
      partialRing.V2 = GenerateTrueEdge(partialRing.V1, partialRing.P2 - partialRing.P1);

      ring.Add(partialRing.V3);
      ring.Add(partialRing.V0);
      ring.Add(partialRing.V1);
      ring.Add(partialRing.V2);
    }

    private void CompleteRingInvertedCorner(ref Ring ring, PartialRing partialRing) {
      var edge03 = GenerateTrueEdge(partialRing.V0, partialRing.P3 - partialRing.P0);
      var edge23 = GenerateTrueEdge(partialRing.V2, partialRing.P3 - partialRing.P2);

      ring.Add(edge23);
      ring.Add(edge03);
      ring.Add(partialRing.V0);
      ring.Add(partialRing.V1);
      ring.Add(partialRing.V2);
    }

    private void CompleteRingCorner(ref Ring ring, PartialRing partialRing) {
      partialRing.V3 = GenerateTrueEdge(partialRing.V0, partialRing.P3 - partialRing.P0);
      partialRing.V1 = GenerateTrueEdge(partialRing.V0, partialRing.P1 - partialRing.P0);

      ring.Add(partialRing.V3);
      ring.Add(partialRing.V0);
      ring.Add(partialRing.V1);

      if (!_settings.CornerReconstruction) {
        return;
      }

      float3 insetA = new float3(partialRing.P3 - partialRing.P0, 0).xzy;
      float3 insetB = new float3(partialRing.P1 - partialRing.P0, 0).xzy;

      float distAlongA = distance(Vertices[partialRing.V1], Vertices[partialRing.V0]);
      float distAlongB = distance(Vertices[partialRing.V3], Vertices[partialRing.V0]);

      float3 pA0 = Vertices[partialRing.V0] + insetA * 0.5f * distAlongB;
      float3 pA1 = pA0 + insetB * _settings.CellSize;

      float3 pB0 = Vertices[partialRing.V0] + insetB * 0.5f * distAlongA;
      float3 pB1 = pB0 + insetA * _settings.CellSize;

      float3 linePointA = Vertices[partialRing.V1];
      float3 linePointB = CalculateTrueEdge(pA0, pA1);

      float3 linePointC = Vertices[partialRing.V3];
      float3 linePointD = CalculateTrueEdge(pB0, pB1);

      float2 linePointA_2D = linePointA.xz;
      float2 linePointB_2D = linePointB.xz;

      float2 linePointC_2D = linePointC.xz;
      float2 linePointD_2D = linePointD.xz;

      //If the lines are within 3 degrees of being parallel, just ignore corner reconstruction, as the
      //resulting corner will likely not be super precise
      if (Vector2.Angle(linePointB_2D - linePointA_2D, linePointC_2D - linePointD_2D) > 3) {
        float2 intersect2D = lineLineIntersection(linePointA_2D, linePointB_2D, linePointC_2D, linePointD_2D);
        float alongFactor = length(intersect2D - linePointA_2D) / length(linePointA_2D - linePointB_2D);
        float3 intersect = new float3(intersect2D.x, lerp(linePointA.y, linePointB.y, alongFactor), intersect2D.y);

        float2 cellMin = (float2)partialRing.Cell * _settings.CellSize;
        float2 cellMax = cellMin + _settings.CellSize;

        float2 cornerAToC = intersect2D - Vertices[partialRing.V1].xz;
        float2 cornerCToB = Vertices[partialRing.V3].xz - intersect2D;

        //If the intersection point lies within the cell, as well as
        //if the intersection point keeps the ring convex, accept it
        if (all(intersect2D > cellMin) &&
            all(intersect2D < cellMax) &&
            dot(cornerAToC, new float2(cornerCToB.y, -cornerCToB.x)) > 0) {
          ring.Add(Vertices.Length);
          Vertices.Add(intersect);
        }
      }
    }

    private int GenerateTrueEdge(int from, int2 direction) {
      if (_trueEdgeCache.TryGetValue(new int3(from, direction), out var existing)) {
        return existing;
      }

      float3 src = Vertices[from];
      float3 dst = src + new float3(direction.x, 0, direction.y) * _settings.CellSize;

      float3 newVert = CalculateTrueEdge(src, dst);

      int vertIndex = Vertices.Length;
      Vertices.Add(newVert);

      _trueEdgeCache[new int3(from, direction)] = vertIndex;

      return vertIndex;
    }

    private float3 CalculateTrueEdge(float3 src, float3 dst) {
      //If we never hit a point, we fall back to the start point, always guaranteed
      //to be part of the surface
      float3 trueEdgePoint = src;

      //Start the raycasts from above the sample positions
      src.y += _settings.StepHeight;
      dst.y += _settings.StepHeight;
      var delta = dst - src;

      Ray ray = new Ray(src, Vector3.down);

      var maxCastDistance = _settings.StepHeight * 2;

      float fraction = 0.5f;
      float stepSize = 0.25f;
      for (int i = 0; i < _settings.ReconstructionIterations; i++) {
        ray.origin = src + delta * fraction;

        //A sample is valid if
        // 1) the raycast hits
        // 2) it hits a surface that is not too steep
        // 3) it hits a surface that is walkable
        // 4) the hit point has enough headroom
        if (Physics.Raycast(ray, out var hit, maxCastDistance, _settings.RelevantLayers, QueryTriggerInteraction.Ignore) &&
            hit.normal.y >= _settings.SurfaceNormalYThreshold &&
            ((1 << hit.collider.gameObject.layer) & _settings.WalkableLayers) != 0 &&
            !Physics.CheckCapsule(hit.point + _settings.CapsuleOffsetA,
                                  hit.point + _settings.CapsuleOffsetB,
                                  _settings.AgentRadius,
                                  _settings.RelevantLayers, 
                                  QueryTriggerInteraction.Ignore)) {
          trueEdgePoint = hit.point;
          fraction += stepSize;
        } else {
          fraction -= stepSize;
        }

        stepSize /= 2;
      }

      return trueEdgePoint;
    }

    float2 lineLineIntersection(float2 A, float2 B, float2 C, float2 D) {
      float a1 = B.y - A.y;
      float b1 = A.x - B.x;
      float c1 = a1 * (A.x) + b1 * (A.y);

      float a2 = D.y - C.y;
      float b2 = C.x - D.x;
      float c2 = a2 * (C.x) + b2 * (C.y);

      float determinant = a1 * b2 - a2 * b1;

      if (determinant == 0) {
        return A;
      } else {
        float x = (b2 * c1 - b1 * c2) / determinant;
        float y = (a1 * c2 - a2 * c1) / determinant;
        return new float2(x, y);
      }
    }

    #endregion
  }
}
