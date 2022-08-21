using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveUpAndDown : MonoBehaviour {

    public float Height;
    public float Period;
    public float Offset;
    public Vector3 Direction = Vector3.up;

    private Vector3 _startingPos;

    void Awake() {
        _startingPos = transform.position;
    }

    void Update() {
        transform.position = _startingPos + Direction * (Mathf.Sin((Time.time / Period + Offset) * Mathf.PI * 2) + 1) * Height * 0.5f;
    }
}
