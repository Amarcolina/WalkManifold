using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Unity.Mathematics;

namespace WalkManifold {

  [ExecuteAlways]
  public class ManifoldDebugView : ManifoldBehaviour {

    public const int REACHABLE_SUBMESH = 0;
    public const int EDGE_SUBMESH = 1;
    public const int UNREACHABLE_SUBMESH = 2;

    public static readonly float AsyncRestartTime = 1;

    [Header("Grid")]
    [Tooltip("If checked, all unreachable areas will be shown in a separate color, allowing " +
             "you to easily isolate the areas reachable from the current cursor position.")]
    public bool IsolateReachable;

    [Tooltip("If checked, the preview grid will be built incrementally over multiple frames " +
             "to allow for larger preview areas with lower per-frame cost.\n\nIMPORTANT: " +
             "because colliders can move frame-to-frame, the resulting grid might not be " +
             "completely accurate, and should be used for visualization only.")]
    public bool UseAsync = false;

    [Tooltip("The size of each async update chunk, measured in cells.  Large chunk sizes " +
             "can allow the update to complete more quickly at the cost of greater per-frame " +
             "cost.")]
    public int AsyncChunkSize = 48;

    [Tooltip("When not in async mode, this value limits the maximum size of the grid.  This " +
             "is to prevent accidentally trying to visualize such a large grid that it would " +
             "lock the editor, preventing any interaction to fix the issue.")]
    public int MaxSyncResolution = 256;

    [Header("Runtime")]
    [Tooltip("If checked, the grid will be drawn in the Game View instead of just as a Gizmo.")]
    public bool ShowInGameView;

    [Tooltip("The material to use to draw the grid in the Game View.")]
    public Material GameViewMat;

    [Header("Visuals")]
    [Tooltip("A vertical offset to apply to the grid when visualizing it, to prevent clipping.")]
    public float VerticalOffset = 0.05f;

    [Tooltip("The main color of the preview grid.")]
    [ColorUsage(showAlpha: true, hdr: true)]
    public Color GizmoColor = new Color(1, 1, 1, 0.2f);

    [Tooltip("The edge color of the preview grid.")]
    [ColorUsage(showAlpha: true, hdr: true)]
    public Color EdgeColor = new Color(0, 1, 0, 1);

    [Tooltip("The color used to show the unreachable part of the grid, if 'Isolate Reachable'" +
             "is enabled.")]
    [ColorUsage(showAlpha: true, hdr: true)]
    public Color UnreachableColor = new Color(1, 0, 0, 0.13f);

    private float _asyncStartTime;
    private Vector3 _asyncStartPosition;
    private Vector3 _asyncStartScale;
    private Task _updateTask = Task.CompletedTask;
    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    private static List<int> _reachableIndices = new List<int>();
    private static List<int> _edgeIndices = new List<int>();
    private static List<int> _unreachableIndices = new List<int>();

    private Mesh _gizmoMesh;
    private MaterialPropertyBlock _block;

    public bool IsUpdating => !_updateTask.IsCompleted;

    protected override void OnEnable() {
      base.OnEnable();

      _block = new MaterialPropertyBlock();

      _gizmoMesh = new Mesh();
      _gizmoMesh.subMeshCount = 3;
      _gizmoMesh.name = "Manifold Debug Mesh";
      _gizmoMesh.hideFlags = HideFlags.HideAndDontSave;
    }

    protected override void OnDisable() {
      if (!_updateTask.IsCompleted) {
        _cancellationTokenSource.Cancel();
      }

      DestroyImmediate(_gizmoMesh);
      _gizmoMesh = null;

      base.OnDisable();
    }

    private void LateUpdate() {
      if (!IsManifoldCreated) {
        return;
      }

      transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);

      if (UseAsync) {
        //We cancel and restart an async task if it has been too long since we started, but only
        //if the position or scale has changed.  If the position and scale has not changed, the 
        //user is free to wait as long as they like for a large visualization to finish.
        //
        //This prevents the issue where the user created too large of a grid, and then made it small
        //again.  We don't want to wait for the previous large grid to finish because that might
        //take ages, so we cancel so we can start the new small query right away for better feedback
        if (!_updateTask.IsCompleted &&
            (transform.position != _asyncStartPosition || transform.localScale != _asyncStartScale) &&
            (Time.realtimeSinceStartup - _asyncStartTime) > AsyncRestartTime) {
          _cancellationTokenSource.Cancel();
          _cancellationTokenSource = new CancellationTokenSource();
        }

        if (_updateTask.IsCompleted) {
          _updateTask = UpdateSurfaceAsync(_cancellationTokenSource.Token);
        }
      } else {
        UpdateSurface();
      }

      if (GameViewMat != null && ShowInGameView && _gizmoMesh.vertexCount != 0) {
        DrawMesh(REACHABLE_SUBMESH, GizmoColor);
        DrawMesh(EDGE_SUBMESH, EdgeColor);
        if (IsolateReachable) {
          DrawMesh(UNREACHABLE_SUBMESH, UnreachableColor);
        }
      }
    }

    private void DrawMesh(int submesh, Color color) {
      _block.SetColor("_Color", color);
      Graphics.DrawMesh(_gizmoMesh, Matrix4x4.Translate(Vector3.up * VerticalOffset), GameViewMat, gameObject.layer, null, submesh, _block);
    }

    private void UpdateSurface() {
      GetSurfaceParams(out var boundsMin, out var boundsMax, out var minCell, out var maxCell);

      var size = maxCell - minCell;
      if (size.x > MaxSyncResolution || size.y > MaxSyncResolution) {
        Debug.LogWarning($"Limiting the resolution of Manifold View {name} to " +
                         $"prevent editor locking.  Increase limit in Inspector if " +
                         $"desired, or use async mode.", this);
        size = math.min(size, MaxSyncResolution);
      }

      Manifold.Update(minCell, minCell + size, boundsMin.y, boundsMax.y);

      if (IsolateReachable) {
        var ring = Manifold.FindClosestRingIndex(transform.position);
        if (ring >= 0) {
          Manifold.MarkReachable(ring);
        }
      }

      UpdateMeshFromSurface();
    }

    private async Task UpdateSurfaceAsync(CancellationToken token) {
      _asyncStartTime = Time.realtimeSinceStartup;
      _asyncStartPosition = transform.position;
      _asyncStartScale = transform.localScale;

      GetSurfaceParams(out var boundsMin, out var boundsMax, out var minCell, out var maxCell);

      await Manifold.UpdateAsync(minCell, maxCell,
                                 boundsMin.y, boundsMax.y,
                                 AsyncChunkSize,
                                 token);

      if (token.IsCancellationRequested) {
        return;
      }

      if (IsolateReachable) {
        var ring = Manifold.FindClosestRingIndex(transform.position);
        if (ring >= 0) {
          Manifold.MarkReachable(ring);
        }
      }

      UpdateMeshFromSurface();

#if UNITY_EDITOR
      SceneView.RepaintAll();
      EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    private void UpdateMeshFromSurface() {
      if (Manifold.Rings.Length == 0 || Manifold.Vertices.Length == 0) {
        _gizmoMesh.Clear();
        return;
      }

      _gizmoMesh.Clear();
      _gizmoMesh.subMeshCount = 3;
      _gizmoMesh.SetVertices(Manifold.Vertices.AsArray());
      _gizmoMesh.SetNormals(Manifold.Vertices.AsArray());

      _reachableIndices.Clear();
      _edgeIndices.Clear();
      _unreachableIndices.Clear();

      foreach (var ring in Manifold.Rings) {
        for (int i = 0; i < ring.Count; i++) {
          var edge = ring.GetEdge(i);

          if (IsolateReachable && !ring.IsMarked) {
            if (!Manifold.IsSharedEdge(edge.x, edge.y) || edge.x < edge.y) {
              _unreachableIndices.Add(edge.x);
              _unreachableIndices.Add(edge.y);
            }
          } else if (!Manifold.IsSharedEdge(edge.x, edge.y)) {
            _edgeIndices.Add(edge.x);
            _edgeIndices.Add(edge.y);
          } else if (edge.x < edge.y) {
            _reachableIndices.Add(edge.x);
            _reachableIndices.Add(edge.y);
          }
        }
      }

      _gizmoMesh.SetIndices(_reachableIndices, MeshTopology.Lines, REACHABLE_SUBMESH);
      _gizmoMesh.SetIndices(_edgeIndices, MeshTopology.Lines, EDGE_SUBMESH);
      _gizmoMesh.SetIndices(_unreachableIndices, MeshTopology.Lines, UNREACHABLE_SUBMESH);
      _gizmoMesh.RecalculateBounds();
    }

    private void GetSurfaceParams(out float3 boundsMin, out float3 boundsMax, out int2 minCell, out int2 maxCell) {
      boundsMin = float.MaxValue;
      boundsMax = float.MinValue;
      for (int x = -1; x <= 1; x += 2) {
        for (int y = -1; y <= 1; y += 2) {
          for (int z = -1; z <= 1; z += 2) {
            float3 corner = transform.TransformPoint(new Vector3(x, y, z) * 0.5f);
            boundsMin = math.min(boundsMin, corner);
            boundsMax = math.max(boundsMax, corner);
          }
        }
      }

      minCell = Manifold.GetCell(boundsMin) - 1;
      maxCell = Manifold.GetCell(boundsMax) + 2;
    }

    private void OnDrawGizmosSelected() {
      Gizmos.matrix = transform.localToWorldMatrix;
      Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }

    private void OnDrawGizmos() {
      if (_gizmoMesh == null || _gizmoMesh.vertexCount == 0) {
        return;
      }

      //Currently showing in the game view also draws in the edit view, so supress
      //regular gizmos if this is enabled
      if (ShowInGameView) {
        return;
      }

      Gizmos.matrix = Matrix4x4.Translate(Vector3.up * VerticalOffset);

      Gizmos.color = GizmoColor;
      Gizmos.DrawWireMesh(_gizmoMesh, REACHABLE_SUBMESH);
      Gizmos.color = EdgeColor;
      Gizmos.DrawWireMesh(_gizmoMesh, EDGE_SUBMESH);

      if (IsolateReachable) {
        Gizmos.color = UnreachableColor;
        Gizmos.DrawWireMesh(_gizmoMesh, UNREACHABLE_SUBMESH);
      }
    }
  }
}
