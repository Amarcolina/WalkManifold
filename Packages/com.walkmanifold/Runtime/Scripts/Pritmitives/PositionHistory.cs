using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using R = Unity.Mathematics.Random;

namespace WalkManifold.Internals {

  [System.Serializable]
  public class PositionHistory {

    public Vector3[] Buffer = new Vector3[256];
    public int[] Rollover = new int[256];
    public int Radix;
    public int CaryThreshold;

    public PositionHistory(Vector3 startPosition, int radix, int caryThreshold) {
      Radix = radix;
      CaryThreshold = caryThreshold;
      Reset(startPosition);
    }

    public void Reset(Vector3 position) {
      for (int i = 0; i < Buffer.Length; i++) {
        Buffer[i] = position;
        Rollover[i] = UnityEngine.Random.Range(0, Radix);
      }
    }

    public void Push(Vector3 position) {
      int shiftCount = 0;
      while (true) {
        int was = Rollover[shiftCount];
        int now = (was + 1) % Radix;
        Rollover[shiftCount] = now;

        if (was < CaryThreshold || shiftCount >= (Buffer.Length - 1)) {
          break;
        }

        shiftCount++;
      }

      for (int i = shiftCount; i > 0; i--) {
        Buffer[i] = Buffer[i - 1];
      }

      Buffer[0] = position;
    }
  }
}
