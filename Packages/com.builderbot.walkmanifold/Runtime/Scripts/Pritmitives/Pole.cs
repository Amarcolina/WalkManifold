using System;

namespace WalkManifold.Internals {

  [Serializable]
  public struct Pole {
    public int Start;
    public int Count;
    public int End => Start + Count;

    public int this[int index] => Start + index;

    public PoleWithCorner AtCorner(int corner) {
      return new PoleWithCorner() {
        Start = Start,
        Count = Count,
        Corner = corner
      };
    }
  }
}
