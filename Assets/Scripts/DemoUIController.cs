using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WalkManifold;

public class DemoUIController : MonoBehaviour {

  public GameObject PlayerRoot;
  public GameObject PropertiesRoot;
  public GameObject[] Objects;
  public ManifoldDebugView PlayerGridView;

  private void Update() {
    if (Input.GetKeyDown(KeyCode.G)) {
      PlayerGridView.ShowInGameView = !PlayerGridView.ShowInGameView;
    }

    if (Input.GetKeyDown(KeyCode.Space)) {
      PlayerRoot.SetActive(!PlayerRoot.activeSelf);
      PropertiesRoot.SetActive(!PropertiesRoot.activeSelf);
    }
  }

  public void Switch(int index) {
    foreach (var obj in Objects) {
      obj.SetActive(false);
    }

    Objects[index].SetActive(true);
  }
}
