using System;
using UnityEngine;

namespace WalkManifold {

  public class ManifoldBehaviour : MonoBehaviour {

    [SerializeField]
    private ManifoldSettings _settings;

    public ManifoldSettings Settings {
      get => _settings;
      set {
        if (value == null) {
          throw new ArgumentNullException("Settings");
        }

        _settings = value;
        InitIfNeeded();

        if (_manifold != null) {
          _manifold.Settings = _settings;
        }
      }
    }

    private Manifold _manifold;
    public Manifold Manifold {
      get {
        InitIfNeeded();
        if (_manifold == null) {
          throw new InvalidOperationException("Must assign a valid Settings asset before Manifold can be used.");
        }
        return _manifold;
      }
    }

    public bool IsManifoldCreated {
      get {
        InitIfNeeded();
        return _manifold != null;
      }
    }

    protected virtual void OnEnable() { }

    protected virtual void OnDisable() {
      _manifold?.Dispose();
      _manifold = null;
    }

    protected virtual void OnDestroy() {
      _manifold?.Dispose();
      _manifold = null;
    }

    protected virtual void OnValidate() {
      if (_manifold != null && _settings != null && !_manifold.IsUpdating) {
        _manifold.Settings = _settings;
      }
    }

    private void InitIfNeeded() {
      if (enabled && _manifold == null && _settings != null) {
        _manifold = new Manifold(Settings);
      }
    }
  }
}
