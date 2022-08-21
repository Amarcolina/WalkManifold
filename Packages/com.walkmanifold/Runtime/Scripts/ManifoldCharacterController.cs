using System;
using UnityEngine;
using Unity.Mathematics;

namespace WalkManifold {
  using Internals;

  public class ManifoldCharacterController : ManifoldBehaviour {

    public bool TranslateWithColliders = true;
    public bool RotateWithColliders = false;

    private Collider _currentFloor;
    private Vector3 _floorLocalPosition;
    private Vector3 _floorLocalForward;
    private Vector3 _worldSpaceForward;

    private PositionHistory _history;

    protected void Start() {
      _history = new PositionHistory(transform.position, 20, 1);
    }

    /// <summary>
    /// Can be called to reset the memory of the controller to only include the current
    /// position.  Can be useful if you want to guarantee that positional rewinding doesn't
    /// include previous positions.
    /// </summary>
    public void ResetPositionHistory() {
      _history.Reset(transform.position);
    }

    public void SimpleMove(Vector3 direction) {
      Move(direction * Time.deltaTime);
    }

    public void Move(Vector3 delta) {
      //Delta y is defined to be ignored, and so zero it out, just in case
      delta.y = 0;

      Vector3 src = transform.position;
      if (_currentFloor != null && TranslateWithColliders) {
        src = _currentFloor.transform.TransformPoint(_floorLocalPosition);
      }

      if (_currentFloor != null && RotateWithColliders) {
        float3 prevForward = _worldSpaceForward;
        float3 newForward = _currentFloor.transform.TransformDirection(_floorLocalForward);
        float rotation = Vector2.SignedAngle(newForward.xz, prevForward.xz);
        transform.Rotate(0, rotation, 0);
      }

      Vector3 dst = src + delta;

      ClosestPointResult result;

      if (!TryFindNextPosition(src, dst, extrude: 1, out result) &&
          !TryFindPositionInHistory(out result)) {
        //If all of this fails, we panic, as we have no idea where the player should be
        //on the surface anymore.  The user will need to manually place the character somewhere
        //valid.
        throw new InvalidOperationException("Could not find walkable surface.");
      }

      _currentFloor = result.Collider;
      transform.position = result.Position;
      _floorLocalPosition = _currentFloor.transform.InverseTransformPoint(result.Position);
      _floorLocalForward = _currentFloor.transform.InverseTransformDirection(transform.forward);
      _worldSpaceForward = transform.forward;

      if (_currentFloor.gameObject.isStatic && Vector3.Distance(_history.Buffer[0], transform.position) > Settings.CellSize) {
        _history.Push(transform.position);
      }
    }

    protected bool TryFindPositionInHistory(out ClosestPointResult result) {
      for (int i = 0; i < _history.Buffer.Length; i++) {
        if (TryFindNextPosition(_history.Buffer[i], _history.Buffer[i], extrude: 0, out result)) {
          return true;
        }
      }
      result = default;
      return false;
    }

    protected bool TryFindNextPosition(Vector3 from, Vector3 to, int extrude, out ClosestPointResult result) {
      float3 min = math.min(from, to);
      float3 max = math.max(from, to);

      float maxHeightDelta = (Vector3.Distance(to, from) / Settings.CellSize + 1) + Settings.StepHeight;
      min.y -= maxHeightDelta;
      max.y += maxHeightDelta;

      int2 minCell = Manifold.GetCell(min) - extrude;
      int2 maxCell = Manifold.GetCell(max) + extrude + 1;

      Manifold.Update(minCell, maxCell, min.y, max.y);

      int startingRing = Manifold.FindClosestRingIndex(from);

      if (startingRing < 0) {
        result = default;
        return false;
      }

      Manifold.MarkReachable(startingRing);

      result = Manifold.FindClosestPoint(to, onlyMarked: true);
      return true;
    }

    protected void OnDrawGizmosSelected() {
      if (Settings == null) {
        return;
      }

      if (_history != null) {
        Vector3 a = (Vector3.right + Vector3.forward) * Settings.CellSize * 0.25f;
        Vector3 b = (Vector3.left + Vector3.forward) * Settings.CellSize * 0.25f;
        foreach (var pos in _history.Buffer) {
          Gizmos.DrawLine(pos + a, pos - a);
          Gizmos.DrawLine(pos + b, pos - b);
        }
      }

      float radius = Settings.AgentRadius;
      float height = Settings.AgentHeight - radius * 2;
      float legHeight = Settings.StepHeight;

      Gizmos.matrix = Matrix4x4.Translate(transform.position + Vector3.up * radius);

      Gizmos.DrawWireSphere(Vector3.up * legHeight, radius);
      Gizmos.DrawWireSphere(Vector3.up * height, radius);

      Gizmos.DrawLine(Vector3.right * radius + Vector3.up * legHeight, Vector3.right * radius + Vector3.up * height);
      Gizmos.DrawLine(Vector3.left * radius + Vector3.up * legHeight, Vector3.left * radius + Vector3.up * height);
      Gizmos.DrawLine(Vector3.forward * radius + Vector3.up * legHeight, Vector3.forward * radius + Vector3.up * height);
      Gizmos.DrawLine(Vector3.back * radius + Vector3.up * legHeight, Vector3.back * radius + Vector3.up * height);
    }
  }
}
