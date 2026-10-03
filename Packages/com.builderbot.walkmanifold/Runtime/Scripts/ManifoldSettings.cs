using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace WalkManifold {

  [CreateAssetMenu]
  public class ManifoldSettings : ScriptableObject {

    [Header("Agent Settings")]
    [Tooltip("The grid will only provide a surface where there is enough floor-to-ceiling headspace to " +
         "accomodate an agent of this height.")]
    [FormerlySerializedAs("AgentHeight")]
    [SerializeField]
    private float _agentHeight = 1f;
    public float AgentHeight {
      get => _agentHeight;
      set => _agentHeight = value;
    }

    [Tooltip("The grid will only provide a surface where there is enough left-to-right space to accomodate " +
             "an agent of this size.")]
    [FormerlySerializedAs("AgentRadius")]
    [SerializeField]
    private float _agentRadius = 0.2f;
    public float AgentRadius {
      get => _agentRadius;
      set => _agentRadius = value;
    }

    [Tooltip("The amount of vertical distance an agent can step up from one part of the manifold to another.  " +
             "any step that is higher than this will not be traversable.")]
    [FormerlySerializedAs("StepHeight")]
    [SerializeField]
    private float _stepHeight = 0.35f;
    public float StepHeight {
      get => _stepHeight;
      set => _stepHeight = value;
    }

    [Tooltip("The grid will only provide a surface where the angle is less than this value.")]
    [Range(0, 90)]
    [FormerlySerializedAs("MaxSurfaceAngle")]
    [SerializeField]
    private float _maxSurfaceAngle = 45f;
    public float MaxSurfaceAngle {
      get => _maxSurfaceAngle;
      set => _maxSurfaceAngle = value;
    }

    [Header("Grid Settings")]
    [Tooltip("The size in meters of each cell of the grid.  In general features smaller than this " +
             "size will not be represented accurately.  Larger values can lose important features, but " +
             "also help to smooth out movement along edges.")]
    [FormerlySerializedAs("CellSize")]
    [SerializeField]
    private float _cellSize = 0.1f;
    public float CellSize {
      get => _cellSize;
      set => _cellSize = value;
    }

    [Tooltip("Allows reconstruction of the edge of the grid to provide a more accurate surface.  If disabled " +
             "only fully occupied cells will be present in the grid.")]
    [FormerlySerializedAs("EdgeReconstruction")]
    [SerializeField]
    private bool _edgeReconstruction = true;
    public bool EdgeReconstruction {
      get => _edgeReconstruction;
      set => _edgeReconstruction = value;
    }

    [Tooltip("Allows reconstruction of the corners of the grid to provide a more accurate surface.  If disabled " +
             "corners will have a truncated appearance.")]
    [FormerlySerializedAs("CornerReconstruction")]
    [SerializeField]
    private bool _cornerReconstruction = true;
    public bool CornerReconstruction {
      get => _cornerReconstruction;
      set => _cornerReconstruction = value;
    }

    [Tooltip("The number of additional physics queries to be used during edge reconstruction.  More iterations " +
             "results in greater quality but increased cost.  Larger values have diminishing returns.")]
    [FormerlySerializedAs("ReconstructionIterations")]
    [SerializeField]
    private int _reconstructionIterations = 6;
    public int ReconstructionIterations {
      get => _reconstructionIterations;
      set => _reconstructionIterations = value;
    }

    [Header("Physics Settings")]
    [Tooltip("The physics layers the agent is allowed to walk on.")]
    [FormerlySerializedAs("WalkableLayers")]
    [SerializeField]
    private LayerMask _walkableLayers = 0xFFFF;
    public LayerMask WalkableLayers {
      get => _walkableLayers;
      set => _walkableLayers = value;
    }

    [Tooltip("The layers that block agent movement but cannot be walked on.")]
    [FormerlySerializedAs("BlockingLayers")]
    [SerializeField]
    private LayerMask _blockingLayers = 0xFFFF;
    public LayerMask BlockingLayers {
      get => _blockingLayers;
      set => _blockingLayers = value;
    }

    [Tooltip("If true, the manifold will call Physics.SyncTransforms before an update to ensure that all " +
             "collider information is up-to-date.  If disabled, you will need to take care that physics is " +
             "in the expected state before calling Update.")]
    [FormerlySerializedAs("SyncPhysicsOnUpdate")]
    [SerializeField]
    private bool _syncPhysicsOnUpdate = true;
    public bool SyncPhysicsOnUpdate {
      get => _syncPhysicsOnUpdate;
      set => _syncPhysicsOnUpdate = value;
    }

    public LayerMask RelevantLayers => WalkableLayers | BlockingLayers;

    private void OnValidate() {
      _reconstructionIterations = Mathf.Max(0, _reconstructionIterations);
      _cellSize = Mathf.Max(0.01f, _cellSize);
    }

    public void SetReconstructionIterationsFromFloat(float value) {
      _reconstructionIterations = Mathf.RoundToInt(value);
    }

    public ValueType GetValueType() {
      return new ValueType() {
        AgentHeight = _agentHeight,
        CapsuleOffsetA = Vector3.up * (_stepHeight + _agentRadius),
        CapsuleOffsetB = Vector3.up * (_agentHeight - _agentRadius),
        AgentRadius = _agentRadius,
        StepHeight = _stepHeight,
        MaxSurfaceAngle = _maxSurfaceAngle,
        SurfaceNormalYThreshold = Mathf.Cos(MaxSurfaceAngle * Mathf.Deg2Rad),
        CellSize = _cellSize,
        EdgeReconstruction = _edgeReconstruction,
        CornerReconstruction = _cornerReconstruction,
        ReconstructionIterations = _reconstructionIterations,
        WalkableLayers = _walkableLayers,
        BlockingLayers = _blockingLayers,
        RelevantLayers = _walkableLayers | _blockingLayers
      };
    }

    public struct ValueType {
      public float AgentHeight;
      public Vector3 CapsuleOffsetA, CapsuleOffsetB;
      public float AgentRadius;
      public float StepHeight;
      public float MaxSurfaceAngle;
      public float SurfaceNormalYThreshold; //If normal.y is greater than this value, normal is accepted
      public float CellSize;
      public bool EdgeReconstruction;
      public bool CornerReconstruction;
      public int ReconstructionIterations;
      public int WalkableLayers;
      public int BlockingLayers;
      public int RelevantLayers;
    }
  }
}
