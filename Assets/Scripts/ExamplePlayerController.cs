using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WalkManifold;

[RequireComponent(typeof(ManifoldCharacterController))]
public class ExamplePlayerController : MonoBehaviour {

  public bool UseCharacterController;
  public float WalkSpeed;
  public float SprintSpeed;
  public float MouseSensitivity;
  public Transform Head;

  private ManifoldCharacterController _controller;
  private CharacterController _cc;
  private Vector3 _rotation;

  private void Awake() {
    _controller = GetComponent<ManifoldCharacterController>();
    _cc = GetComponent<CharacterController>();
  }

  private void OnEnable() {
    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
  }

  private void OnDisable() {
    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;
  }

  private void LateUpdate() {
    if (Time.time < 1) return;
    if (!Application.isFocused) return;

    Vector3 moveDir = transform.right * Input.GetAxis("Horizontal") +
                      transform.forward * Input.GetAxis("Vertical");

    if (UseCharacterController) {
      _cc.SimpleMove(moveDir * (Input.GetKey(KeyCode.LeftShift) ? SprintSpeed : WalkSpeed));
    } else {
      _controller.SimpleMove(moveDir * (Input.GetKey(KeyCode.LeftShift) ? SprintSpeed : WalkSpeed));
    }

    transform.Rotate(0, Input.GetAxis("Mouse X") * MouseSensitivity, 0);

    _rotation.x = Mathf.Clamp(_rotation.x - Input.GetAxis("Mouse Y") * MouseSensitivity, -85, 85);
    Head.localEulerAngles = _rotation;
  }
}
