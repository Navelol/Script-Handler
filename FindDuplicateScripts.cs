using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;

public class FindInternalDuplicates : EditorWindow
{
    /// <summary>
    /// Represents one group of duplicate scripts *on the same GameObject*.
    /// E.g., If Bone.004 has 3 identical MyScript components, that's one group.
    /// We'll keep 1 and consider 2 duplicates.
    /// </summary>
    private class DuplicateGroup
    {
        public GameObject gameObject;
        public string scriptName;       // c.GetType().FullName
        public string signature;        // serialized property signature
        public List<int> componentIndices = new List<int>(); // which component indices
    }

    // We'll gather these groups across the scene
    private static List<DuplicateGroup> duplicateGroups = new List<DuplicateGroup>();

    // For checkboxes: each DuplicateGroup can be "selected for removal" or not
    private static Dictionary<DuplicateGroup, bool> selectionDict = new Dictionary<DuplicateGroup, bool>();

    [MenuItem("Tools/Evan's Script Handler/Find Internal Duplicates")]
    public static void ShowWindow()
    {
        GetWindow<FindInternalDuplicates>("Internal Duplicates");
        RefreshDuplicates();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Scripts duplicated on the *same* GameObject", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Refresh", GUILayout.Height(25)))
        {
            RefreshDuplicates();
        }

        EditorGUILayout.Space();

        if (duplicateGroups.Count == 0)
        {
            EditorGUILayout.LabelField("No internal duplicates found in the scene.");
            return;
        }

        foreach (var group in duplicateGroups)
        {
            if (!group.gameObject) continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"GameObject: {group.gameObject.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Script: {group.scriptName}");
            EditorGUILayout.LabelField($"Signature: {Truncate(group.signature, 120)}");
            EditorGUILayout.LabelField(
                $"Count: {group.componentIndices.Count} (1 is original, {group.componentIndices.Count - 1} duplicates)"
            );

            EditorGUILayout.Space(2);

            // Button to select/ping
            if (GUILayout.Button("Select This GameObject", GUILayout.Height(20)))
            {
                Selection.activeGameObject = group.gameObject;
                EditorGUIUtility.PingObject(group.gameObject);
            }

            // A toggle that controls whether we remove duplicates for this group or not
            bool oldVal = selectionDict[group];
            bool newVal = EditorGUILayout.ToggleLeft("Remove duplicates", oldVal);
            selectionDict[group] = newVal;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All"))
        {
            foreach (var g in duplicateGroups)
                selectionDict[g] = true;
        }
        if (GUILayout.Button("Deselect All"))
        {
            foreach (var g in duplicateGroups)
                selectionDict[g] = false;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (GUILayout.Button("Delete Selected Duplicates", GUILayout.Height(30)))
        {
            DeleteSelectedDuplicates();
        }
    }

    /// <summary>
    /// Scans the scene for each GameObject that has multiple identical copies of the *same* script.
    /// Groups them so you can optionally remove the extra copies (duplicates).
    /// </summary>
    private static void RefreshDuplicates()
    {
        duplicateGroups.Clear();
        selectionDict.Clear();

        // Grab all scene objects
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            // Skip prefabs/assets
            if (EditorUtility.IsPersistent(go)) continue;
            // Skip hidden or invalid
            if (!go.scene.IsValid()) continue;
            if (go.hideFlags != HideFlags.None) continue;

            // Build a dict for this GameObject: key = (scriptName + signature), val = indices
            var compDict = new Dictionary<string, List<int>>();
            Component[] comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue; // missing script, skip
                if (!(c is MonoBehaviour)) continue; // skip built-in types if you want

                string scriptName = c.GetType().FullName;
                string sig = GetSerializedSignature(c);

                string key = scriptName + "__" + sig;
                if (!compDict.ContainsKey(key))
                {
                    compDict[key] = new List<int>();
                }
                compDict[key].Add(i);
            }

            // Now for each key in compDict, if there's more than 1 index, we have duplicates
            foreach (var kvp in compDict)
            {
                var indices = kvp.Value;
                if (indices.Count > 1)
                {
                    // There's duplicates
                    // We'll store them as a group
                    string[] parts = kvp.Key.Split(new string[] { "__" }, 2, System.StringSplitOptions.None);
                    string extractedScriptName = parts[0];
                    string extractedSig = parts[1];

                    DuplicateGroup group = new DuplicateGroup
                    {
                        gameObject = go,
                        scriptName = extractedScriptName,
                        signature = extractedSig,
                        componentIndices = indices
                    };
                    duplicateGroups.Add(group);
                    selectionDict[group] = false; // default unchecked
                }
            }
        }
    }

    /// <summary>
    /// Serializes all public / [SerializeField] fields of this component to build a signature string.
    /// </summary>
    private static string GetSerializedSignature(Component comp)
    {
        SerializedObject so = new SerializedObject(comp);
        SerializedProperty prop = so.GetIterator();
        StringBuilder sb = new StringBuilder();
        sb.Append("{");

        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            if (prop.name == "m_Script")
            {
                enterChildren = false;
                continue;
            }

            sb.Append(prop.name).Append(":").Append(prop.propertyType).Append("=");

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    sb.Append(prop.intValue);
                    break;
                case SerializedPropertyType.Boolean:
                    sb.Append(prop.boolValue);
                    break;
                case SerializedPropertyType.Float:
                    sb.Append(prop.floatValue);
                    break;
                case SerializedPropertyType.String:
                    sb.Append(prop.stringValue);
                    break;
                case SerializedPropertyType.Color:
                    sb.Append(prop.colorValue);
                    break;
                case SerializedPropertyType.ObjectReference:
                    sb.Append(prop.objectReferenceValue ? prop.objectReferenceValue.name : "null");
                    break;
                case SerializedPropertyType.Enum:
                    sb.Append(prop.enumValueIndex);
                    break;
                case SerializedPropertyType.Vector2:
                    sb.Append(prop.vector2Value);
                    break;
                case SerializedPropertyType.Vector3:
                    sb.Append(prop.vector3Value);
                    break;
                case SerializedPropertyType.Vector4:
                    sb.Append(prop.vector4Value);
                    break;
                default:
                    sb.Append(prop.type);
                    break;
            }

            sb.Append(";");
            enterChildren = false;
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static string Truncate(string input, int maxLength)
    {
        if (input.Length <= maxLength) return input;
        return input.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Removes the extra copies from each selected group, keeping exactly 1. 
    /// If a group has N identical scripts, we remove N-1.
    /// </summary>
    private void DeleteSelectedDuplicates()
    {
        // We'll gather what to remove in a safe list
        List<DuplicateGroup> chosenGroups = new List<DuplicateGroup>();
        foreach (var group in duplicateGroups)
        {
            if (selectionDict.ContainsKey(group) && selectionDict[group])
            {
                chosenGroups.Add(group);
            }
        }

        if (chosenGroups.Count == 0)
        {
            Debug.Log("No duplicates selected for removal.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Remove Duplicates");

        foreach (var group in chosenGroups)
        {
            if (!group.gameObject) continue;
            Component[] comps = group.gameObject.GetComponents<Component>();
            // We only remove the "extra" ones from this group, leaving 1.

            // Sort componentIndices so we remove from the "highest" index first,
            // ensuring we don't shift the array unexpectedly.
            group.componentIndices.Sort();

            // We'll keep the first index in the sorted list as the "original" 
            // and remove the rest.
            for (int i = 1; i < group.componentIndices.Count; i++)
            {
                int compIndex = group.componentIndices[i];
                if (compIndex < comps.Length && comps[compIndex] != null)
                {
                    Undo.DestroyObjectImmediate(comps[compIndex]);
                    Debug.Log($"Removed duplicate script '{group.scriptName}' from {group.gameObject.name}.", group.gameObject);
                }
            }
        }

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        RefreshDuplicates();
    }
}
