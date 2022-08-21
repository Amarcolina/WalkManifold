using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour {

  public Vector3 Axis;
  public float Rate;

  void Update() {
    transform.Rotate(Axis, Rate * Time.deltaTime);
  }
}
