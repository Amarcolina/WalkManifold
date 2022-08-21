using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WalkManifold.Internals;

public class PositionHistoryTest : MonoBehaviour {

  public PositionHistory History;

  void Start() {
    History = new PositionHistory(transform.position, 20, 1);
  }

  void Update() {
    History.Push(transform.position);
  }

  private void OnDrawGizmos() {
    Gizmos.color = Color.blue;
    for (int i = 1; i < History.Buffer.Length; i++) {
      Gizmos.DrawLine(History.Buffer[i - 1], History.Buffer[i]);
    }
    Gizmos.color = Color.white;
    foreach (var p in History.Buffer) {
      Gizmos.DrawSphere(p, 0.05f);
    }

  }
}
