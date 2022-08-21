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
  [Range(0, 1)]
  public float HeadDamping = 0.95f;

  private ManifoldCharacterController _controller;
  private CharacterController _cc;
  private Vector3 _rotation;
  private Vector3 _headOrigin;

  private void Awake() {
    _controller = GetComponent<ManifoldCharacterController>();
    _cc = GetComponent<CharacterController>();
    _headOrigin = Head.localPosition;
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

    Vector3 moveDir = transform.right * Input.GetAxis("Horizontal") +
                      transform.forward * Input.GetAxis("Vertical");

    Vector3 prevHeadPos = Head.position;
    Head.localPosition = _headOrigin;

    if (UseCharacterController) {
      _cc.SimpleMove(moveDir * (Input.GetKey(KeyCode.LeftShift) ? SprintSpeed : WalkSpeed));
    } else {
      _controller.SimpleMove(moveDir * (Input.GetKey(KeyCode.LeftShift) ? SprintSpeed : WalkSpeed));
    }

    Head.position = Vector3.Lerp(prevHeadPos, Head.position, 1.0f - HeadDamping);

    if (Application.isFocused) {
      transform.Rotate(0, Input.GetAxis("Mouse X") * MouseSensitivity, 0);

      _rotation.x = Mathf.Clamp(_rotation.x - Input.GetAxis("Mouse Y") * MouseSensitivity, -85, 85);
      Head.localEulerAngles = _rotation;
    }
  }
}
