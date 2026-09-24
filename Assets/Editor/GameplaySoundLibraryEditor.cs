using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameplaySoundLibrary))]
public sealed class GameplaySoundLibraryEditor : Editor
{
    private string search = "";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("Expand an action and drag audio into Clips. Add extra clips for random variations. Display names are safe to rename. Existing action keys must stay unchanged. Edit outside Play mode to keep your changes.", MessageType.Info);
        search = EditorGUILayout.TextField("Search actions", search);
        var sounds = serializedObject.FindProperty("sounds");
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool duplicate = false;
        for (int i = 0; i < sounds.arraySize; i++)
        {
            var row = sounds.GetArrayElementAtIndex(i);
            var name = row.FindPropertyRelative("name");
            var key = row.FindPropertyRelative("key");
            if (!string.IsNullOrWhiteSpace(key.stringValue) && !keys.Add(key.stringValue)) duplicate = true;
            string label = string.IsNullOrWhiteSpace(name.stringValue) ? "New Sound" : name.stringValue;
            if (!string.IsNullOrEmpty(search) && label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                key.stringValue.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            row.isExpanded = EditorGUILayout.Foldout(row.isExpanded, label, true);
            if (!row.isExpanded) continue;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(name, new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(key, new GUIContent("Action Key"));
            EditorGUILayout.PropertyField(row.FindPropertyRelative("variants"), new GUIContent("Clips"), true);
            if (GUILayout.Button("Remove this entry"))
            {
                sounds.DeleteArrayElementAtIndex(i);
                EditorGUI.indentLevel--;
                break;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }
        if (duplicate) EditorGUILayout.HelpBox("Duplicate action keys: only the first matching entry plays. Give new actions unique keys.", MessageType.Warning);
        if (GUILayout.Button("Add Sound"))
        {
            int index = sounds.arraySize++;
            var row = sounds.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("name").stringValue = "New Sound";
            row.FindPropertyRelative("key").stringValue = "";
            row.FindPropertyRelative("variants").ClearArray();
            row.isExpanded = true;
            search = "";
        }
        EditorGUILayout.HelpBox("New entries need a gameplay action that calls their Action Key. Adding clips to an existing action works immediately. To mute an action, clear its Clips instead of deleting its entry.", MessageType.None);
        serializedObject.ApplyModifiedProperties();
    }
}
