using System;

namespace WalkManifold.Internals {

  [Serializable]
  public struct PoleWithCorner {
    public int Start;
    public int Count;
    public int Corner;
    public int End => Start + Count;

    public int this[int index] => Start + index;
  }
}
