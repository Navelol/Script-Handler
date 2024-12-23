using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class SimpleHighlighterMissingScripts : EditorWindow
{
    // Stores references to scene objects with missing scripts
    private static List<GameObject> objectsWithMissing = new List<GameObject>();
    // Tracks whether each object is checked
    private static Dictionary<int, bool> selectionDict = new Dictionary<int, bool>();

    [MenuItem("Tools/Evan's Script Handler/Find Missing Scripts")]
    public static void ShowWindow()
    {
        GetWindow<SimpleHighlighterMissingScripts>("Missing Scripts");
        RefreshList(); // Ensure this method exists or remove it if not used
    }




    private void OnGUI()
    {
        EditorGUILayout.LabelField("Objects with Missing Scripts (Scene Only)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Refresh", GUILayout.Height(25)))
        {
            RefreshList();
        }

        EditorGUILayout.Space();
        if (objectsWithMissing.Count == 0)
        {
            EditorGUILayout.LabelField("No missing scripts found.");
            return;
        }

        // Show each object
        foreach (GameObject obj in objectsWithMissing)
        {
            if (!obj) continue;

            int id = obj.GetInstanceID();
            EditorGUILayout.BeginHorizontal();

            // Clickable name to select/ping in the Hierarchy
            if (GUILayout.Button(obj.name, GUILayout.ExpandWidth(true)))
            {
                Selection.activeGameObject = obj;
                EditorGUIUtility.PingObject(obj);
            }

            // The checkbox
            bool oldValue = selectionDict[id];
            bool newValue = EditorGUILayout.Toggle(oldValue, GUILayout.Width(20));

            // If we just changed from unchecked to checked, highlight in the scene
            if (!oldValue && newValue)
            {
                Selection.activeGameObject = obj;
                EditorGUIUtility.PingObject(obj);
            }

            selectionDict[id] = newValue;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        // Select All button
        if (GUILayout.Button("Select All", GUILayout.Height(25)))
        {
            // We'll gather all objects into a list so we can multi-select them
            List<Object> toSelect = new List<Object>();
            foreach (GameObject obj in objectsWithMissing)
            {
                if (!obj) continue;
                int id = obj.GetInstanceID();
                // Check each box
                selectionDict[id] = true;
                // Add to selection list
                toSelect.Add(obj);
            }
            // Now select them in the scene
            Selection.objects = toSelect.ToArray();
        }

        // Deselect All button
        if (GUILayout.Button("Deselect All", GUILayout.Height(25)))
        {
            // Uncheck boxes
            foreach (GameObject obj in objectsWithMissing)
            {
                if (!obj) continue;
                int id = obj.GetInstanceID();
                selectionDict[id] = false;
            }
            // Clear selection in the scene
            Selection.objects = new Object[0];
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Button to remove missing scripts from all checked objects
        if (GUILayout.Button("Delete Selected Missing Scripts", GUILayout.Height(30)))
        {
            DeleteCheckedObjects();
        }
    }

    /// <summary>
    /// Finds all scene objects that have missing scripts.
    /// </summary>
    private static void RefreshList()
    {
        objectsWithMissing.Clear();
        selectionDict.Clear();

        // Grab all scene objects
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            // Skip assets/prefabs
            if (EditorUtility.IsPersistent(go)) continue;
            // Skip anything not in a valid scene
            if (!go.scene.IsValid()) continue;
            // Skip hidden objects
            if (go.hideFlags != HideFlags.None) continue;

            if (HasMissingScript(go))
            {
                objectsWithMissing.Add(go);
                selectionDict[go.GetInstanceID()] = false;
            }
        }
    }

    /// <summary>
    /// Returns true if 'go' has at least one missing script.
    /// </summary>
    private static bool HasMissingScript(GameObject go)
    {
        Component[] comps = go.GetComponents<Component>();
        foreach (Component c in comps)
        {
            if (c == null) return true;
        }
        return false;
    }

    /// <summary>
    /// Removes missing scripts from all checked objects.
    /// </summary>
    private static void DeleteCheckedObjects()
    {
        // We'll do a copy in case we modify the list while iterating
        var copy = new List<GameObject>(objectsWithMissing);

        foreach (GameObject obj in copy)
        {
            if (!obj) continue;
            int id = obj.GetInstanceID();

            // If user has it checked
            if (selectionDict.ContainsKey(id) && selectionDict[id])
            {
                Undo.RegisterCompleteObjectUndo(obj, "Remove Missing Scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                Debug.Log($"Removed missing scripts from: {obj.name}", obj);
            }
        }

        // Refresh after removals
        RefreshList();
    }
}
