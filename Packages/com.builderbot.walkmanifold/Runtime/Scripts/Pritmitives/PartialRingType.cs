
namespace WalkManifold.Internals {

  public enum PartialRingType {
    //No poles at all
    Invalid,
    //All 4 corners have poles
    Complete,
    //Only 1 corner has a pole
    Corner,
    //Two adjacent corners have poles
    Edge,
    //Only one corner is missing a pole
    InvertedCorner,
    //Opposite corners have poles
    Diagonal
  }
}
