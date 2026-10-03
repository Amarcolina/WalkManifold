using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace WalkManifold {
  using Internals;

  public class ManifoldDebugWindow : EditorWindow {

    private const string ICON_PATH = "Packages/com.walkmanifold/Editor/Icon 16x16.png";
    private const string SETTINGS_KEY = "WalkManifold_DebugWindowSettings";

    [Tooltip("The settings used for the preview.  Must be assigned for the preview to work.")]
    public ManifoldSettings Settings;

    [HideInInspector]
    public string SettingsGUID;

    [Header("Control Settings")]
    [Tooltip("The key to hold to trigger the preview.")]
    public KeyCode ShowKey = KeyCode.G;

    [Tooltip("The radius of the scene-pick radius, measured in Cells.")]
    public float CastRadius = 1f;

    [Tooltip("The size of the preview area.")]
    public Vector3 PreviewSize = Vector3.one * 0.5f;

    [Tooltip("The amount to scale the preview area when the mouse is scrolled.")]
    public float PreviewScaleDelta = 0.1f;

    [Header("Gizmo Settings")]
    [Tooltip("If checked, all unreachable areas will be shown in a separate color, allowing " +
             "you to easily isolate the areas reachable from the current cursor position.")]
    public bool IsolateReachable = false;

    [Tooltip("The main color of the preview grid.")]
    [ColorUsage(showAlpha: true, hdr: true)]
    public Color GizmoColor = new Color(1, 1, 1, 0.2f);

    [Tooltip("The edge color of the preview grid.")]
    [ColorUsage(showAlpha: true, hdr: true)]
    public Color EdgeColor = new Color(0, 1, 0, 1);

    [Tooltip("The color used to show the unreachable part of the grid, if 'Isolate Reachable'" +
             "is enabled.")]
    [ColorUsage(showAlpha: true, hdr: true)]
    public Color UnreachableColor = new Color(1, 0, 0, 0.13f);

    private bool _isPressingHotkey = false;
    private SerializedObject _sObj;
    private ManifoldDebugView _debugView;
    private Plane _placementPlane = new Plane(Vector3.up, Vector3.zero);

    private List<GameObject> _selectedObjs = new List<GameObject>();

    [MenuItem("Tools/Walk Manifold Debug")]
    static void Init() {
      GetWindow<ManifoldDebugWindow>().Show();
    }

    private void OnEnable() {
      SceneView.duringSceneGui += OnSceneGUI;

      string json = EditorUserSettings.GetConfigValue(SETTINGS_KEY);
      if (!string.IsNullOrEmpty(json)) {
        JsonUtility.FromJsonOverwrite(json, this);
        if (!string.IsNullOrEmpty(SettingsGUID)) {
          string path = AssetDatabase.GUIDToAssetPath(SettingsGUID);
          if (!string.IsNullOrEmpty(path)) {
            Settings = AssetDatabase.LoadAssetAtPath<ManifoldSettings>(path);
          }
        }
      }

      titleContent = new GUIContent("Walk Manifold", AssetDatabase.LoadAssetAtPath<Texture2D>(ICON_PATH));

      _sObj = new SerializedObject(this);
    }

    private void OnDisable() {
      SceneView.duringSceneGui -= OnSceneGUI;
      _sObj.Dispose();

      if (Settings != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Settings, out var guid, out long _)) {
        SettingsGUID = guid;
      }
      EditorUserSettings.SetConfigValue(SETTINGS_KEY, JsonUtility.ToJson(this, prettyPrint: false));
    }

    private void OnGUI() {
      EditorGUIUtility.wideMode = true;

      GUILayout.Space(6);

      using (new GUILayout.HorizontalScope(EditorStyles.helpBox)) {
        GUILayout.Label("This window allows you to visualize the Walk Manifold in your Scene.  Simply " +
                        "press the hotkey (G by default) when you are in the Scene View, and a preview " +
                        "of the walkable surface will be visualized wherever your mouse is.\n\n" +
                        "You can also scroll your mouse-wheel to increase or decrease the size of the " +
                        "preview area.\n\n" +
                        "Make sure to assign a Manifold Settings asset in order to begin preview.  This " +
                        "window allows you to control the various settings available to you.",
                        EditorStyles.wordWrappedLabel);
      }

      GUILayout.Space(12);

      _sObj.Update();

      var it = _sObj.GetIterator();
      it.NextVisible(true);
      while (it.NextVisible(enterChildren: false)) {
        EditorGUILayout.PropertyField(it, includeChildren: true);
      }

      if (Event.current.type == EventType.KeyDown &&
          Event.current.keyCode == ShowKey) {
        _isPressingHotkey = true;
      }

      if (Event.current.type == EventType.KeyUp &&
          Event.current.keyCode == ShowKey) {
        _isPressingHotkey = false;
      }

      _sObj.ApplyModifiedProperties();
    }

    private void OnSceneGUI(SceneView view) {
      if (Settings == null) {
        return;
      }

      if (Event.current.type == EventType.KeyDown &&
          Event.current.keyCode == ShowKey) {
        _isPressingHotkey = true;
      }

      if (Event.current.type == EventType.MouseLeaveWindow) {
        _isPressingHotkey = false;
      }

      if (Event.current.type == EventType.KeyUp &&
         Event.current.keyCode == ShowKey) {
        _isPressingHotkey = false;
      }

      if (!_isPressingHotkey) {
        if (_debugView != null) {
          DestroyImmediate(_debugView.gameObject);
        }
        return;
      }

      if (_debugView == null) {
        var obj = new GameObject("__GridDebug");
        obj.hideFlags = HideFlags.DontSave;
        _debugView = obj.AddComponent<ManifoldDebugView>();
        _debugView.UseAsync = true;
      }

      if (!_debugView.IsUpdating) {
        _debugView.Settings = Settings;
      }

      if (Event.current.type == EventType.MouseDown &&
          Event.current.button == 2) {
        Event.current.Use();
        _debugView.gameObject.hideFlags = HideFlags.None;
        Selection.activeObject = _debugView.gameObject;
        _debugView = null;
        return;
      }

      _selectedObjs.Clear();
      foreach (var obj in Selection.objects) {
        if (obj is GameObject gameObject && gameObject.scene.IsValid()) {
          _selectedObjs.Add(gameObject);
        }
      }

      if (Event.current.type == EventType.ScrollWheel) {
        Event.current.Use();
        Repaint();
        PreviewSize = max(_debugView.Settings.CellSize, PreviewSize * pow(1 + PreviewScaleDelta, Event.current.delta.y));
      }

      var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
      if (Physics.SphereCast(ray, _debugView.Settings.CellSize * CastRadius, out var hit, _debugView.Settings.RelevantLayers)) {
        _placementPlane = new Plane(Vector3.up, hit.point);
      }

      _placementPlane.Raycast(ray, out var placementDist);
      _debugView.transform.position = ray.GetPoint(placementDist);

      _debugView.IsolateReachable = IsolateReachable;
      _debugView.GizmoColor = GizmoColor;
      _debugView.EdgeColor = EdgeColor;
      _debugView.UnreachableColor = UnreachableColor;
      _debugView.transform.localScale = PreviewSize;

      var prevMat = Handles.matrix;
      Handles.matrix = _debugView.transform.localToWorldMatrix;
      Handles.DrawWireCube(Vector3.zero, Vector3.one);
      Handles.matrix = prevMat;
    }
  }
}
