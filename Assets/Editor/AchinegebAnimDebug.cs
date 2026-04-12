#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AchinegebAnimDebug
{
    static readonly string[] AchinegebClipPaths =
    {
        "Assets/charecters/team/achinegeb/animations/idle.anim",
        "Assets/charecters/team/achinegeb/animations/walk.anim",
        "Assets/charecters/team/achinegeb/animations/falling.anim",
    };

    /// <summary>
    /// Round-trips editor curves so Unity serializes m_ClipBindingConstant.genericBindings again
    /// (required after YAML cleared bindings — reimport alone often leaves them empty).
    /// </summary>
    [MenuItem("Tools/Achinegeb/Rebuild clip bindings")]
    public static void RebuildClipBindings()
    {
        RebuildClipBindingsInternal();
        AssetDatabase.SaveAssets();
        Debug.Log("Achinegeb: rebuilt animation clip bindings (saved assets).");
    }

    /// <summary>For Unity -batchmode -executeMethod AchinegebAnimDebug.RebuildClipBindingsBatch</summary>
    public static void RebuildClipBindingsBatch()
    {
        RebuildClipBindingsInternal();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    static void RebuildClipBindingsInternal()
    {
        foreach (var path in AchinegebClipPaths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                Debug.LogWarning("Achinegeb: missing clip at " + path);
                continue;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }

            EditorUtility.SetDirty(clip);
        }

        AssetDatabase.ForceReserializeAssets(AchinegebClipPaths, ForceReserializeAssetsOptions.ReserializeAssets);
    }

    [MenuItem("Tools/Achinegeb/Reimport animation clips")]
    public static void ReimportClips()
    {
        foreach (var path in AchinegebClipPaths)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        Debug.Log("Reimported achinegeb animation clips.");
    }

    [MenuItem("Tools/Achinegeb/Dump player animator paths")]
    public static void DumpPlayerHierarchyMenu() => DumpPlayerHierarchy();

    public static void DumpPlayerHierarchy()
    {
        const string prefabPath = "Assets/charecters/team/achinegeb/achinegev_player 1.prefab";
        var instance = PrefabUtility.LoadPrefabContents(prefabPath);
        var sb = new StringBuilder();
        try
        {
            var anim = instance.GetComponentInChildren<Animator>(true);
            if (anim == null)
            {
                sb.AppendLine("No Animator found.");
            }
            else
            {
                var root = anim.transform;
                sb.AppendLine("Animator on: " + GetPath(instance.transform, root));
                LogRec(root, root, sb);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }

        var outPath = Path.Combine(Application.dataPath, "..", "achinegeb_hierarchy_dump.txt");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("Wrote " + outPath);
    }

    static string GetPath(Transform sceneRoot, Transform t)
    {
        if (t == sceneRoot) return t.name;
        return GetPath(sceneRoot, t.parent) + "/" + t.name;
    }

    static void LogRec(Transform animRoot, Transform t, StringBuilder sb, string indent = "")
    {
        string rel = AnimationUtility.CalculateTransformPath(t, animRoot);
        sb.AppendLine(indent + t.name + "  ==>  '" + rel + "'");
        foreach (Transform c in t)
            LogRec(animRoot, c, sb, indent + "  ");
    }
}
#endif
