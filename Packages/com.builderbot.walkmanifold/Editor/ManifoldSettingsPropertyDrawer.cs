using UnityEngine;
using UnityEditor;

namespace WalkManifold {

  [CustomPropertyDrawer(typeof(ManifoldSettings))]
  public class ManifoldSettingsPropertyDrawer : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      if (!property.hasMultipleDifferentValues && property.objectReferenceValue == null) {
        var guids = AssetDatabase.FindAssets("t:ManifoldSettings");
        if (guids.Length == 1) {
          property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ManifoldSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
      }

      EditorGUI.PropertyField(position, property, label, includeChildren: true);
    }
  }
}
