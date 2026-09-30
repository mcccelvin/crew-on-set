using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BossDialogueText))]
public sealed class BossDialogueTextDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight + 4f + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("text"));
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var title = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(title, ObjectNames.NicifyVariableName(property.FindPropertyRelative("step").stringValue), EditorStyles.boldLabel);
        position.y += EditorGUIUtility.singleLineHeight + 4f;
        position.height -= EditorGUIUtility.singleLineHeight + 4f;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("text"), GUIContent.none);
        EditorGUI.EndProperty();
    }
}
