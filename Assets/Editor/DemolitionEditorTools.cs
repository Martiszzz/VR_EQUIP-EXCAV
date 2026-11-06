using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tools to streamline Level-0 demolition setup.
/// Access via Tools > Demolition menu.
/// </summary>
public static class DemolitionEditorTools
{
    // ── Add DemolitionBlock to selected objects ───────────────────────────
    [MenuItem("Tools/Demolition/Add DemolitionBlock to Selected")]
    static void AddBlockToSelected()
    {
        int added = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (!go.GetComponent<Rigidbody>())
            {
                Debug.LogWarning($"[Demolition] {go.name} has no Rigidbody – skipped.");
                continue;
            }
            if (!go.GetComponent<DemolitionBlock>())
            {
                Undo.AddComponent<DemolitionBlock>(go);
                added++;
            }
        }
        Debug.Log($"[Demolition] Added DemolitionBlock to {added} object(s).");
    }

    [MenuItem("Tools/Demolition/Add DemolitionBlock to Selected", true)]
    static bool ValidateAddBlockToSelected() => Selection.gameObjects.Length > 0;

    // ── Tag selected objects as HouseBlock ───────────────────────────────
    [MenuItem("Tools/Demolition/Tag Selected as HouseBlock")]
    static void TagSelected()
    {
        // Ensure the tag exists
        EnsureTagExists("HouseBlock");

        int tagged = 0;
        foreach (var go in Selection.gameObjects)
        {
            Undo.RecordObject(go, "Tag as HouseBlock");
            go.tag = "HouseBlock";
            tagged++;
        }
        Debug.Log($"[Demolition] Tagged {tagged} object(s) as HouseBlock.");
    }

    [MenuItem("Tools/Demolition/Tag Selected as HouseBlock", true)]
    static bool ValidateTagSelected() => Selection.gameObjects.Length > 0;

    // ── Remove DemolitionBlock from selected objects ──────────────────────
    [MenuItem("Tools/Demolition/Remove DemolitionBlock from Selected")]
    static void RemoveBlockFromSelected()
    {
        int removed = 0;
        foreach (var go in Selection.gameObjects)
        {
            var block = go.GetComponent<DemolitionBlock>();
            if (block) { Undo.DestroyObjectImmediate(block); removed++; }
        }
        Debug.Log($"[Demolition] Removed DemolitionBlock from {removed} object(s).");
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    static void EnsureTagExists(string tag)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
        Debug.Log($"[Demolition] Created tag '{tag}' in Tag Manager.");
    }
}
