using System;
using Unity.Mathematics;

namespace WalkManifold.Internals {

  /// <summary>
  /// Represents a ring of vertices on the walkable surface. A ring exists inside
  /// of a cell, and always must contain at least one 'pole' vertex, which lies exactly
  /// on the cell corner.
  /// 
  /// A ring can contain as few as 3 vertices (for truncated corners) or as many as 6
  /// verticdes (for diagonals), but most contain 4 vertices as most are regular Complete
  /// rings with all four vertices being pole vertices.
  /// </summary>
  [Serializable]
  public struct Ring {

    public int Count;
    public int2 Cell;
    public bool IsMarked;

    public unsafe fixed int Indices[6];

    public void Add(int index) {
      unsafe {
        Indices[Count++] = index;
      }
    }

    public int2 GetEdge(int index) {
      unsafe {
        return new int2(this[index], this[(index + 1) % Count]);
      }
    }

    public int this[int index] {
      get {
        unsafe {
          return Indices[index];
        }
      }
      set {
        unsafe {
          Indices[index] = value;
        }
      }
    }
  }
}
