using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="Enemy"/> prefab has a <see cref="DeathParticles"/> child with <see cref="ParticleSystem"/>
/// wired to <c>deathParticles</c>. Modules are finalized at runtime by <see cref="Enemy"/> in <c>Awake</c>.
/// </summary>
public static class EnemyDeathParticlesPrefabSetup
{
    const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";

    [MenuItem("Tools/2D Platformer/Ensure Enemy Death Particles Child", false, 41)]
    public static void EnsureEnemyDeathParticlesChild()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            var enemy = root.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogError("Enemy component missing on prefab at " + EnemyPrefabPath);
                return;
            }

            var so = new SerializedObject(enemy);
            SerializedProperty prop = so.FindProperty("deathParticles");
            if (prop == null)
            {
                Debug.LogError("Could not find deathParticles on Enemy.");
                return;
            }

            var assigned = prop.objectReferenceValue as ParticleSystem;
            if (assigned != null && assigned.gameObject != null && assigned.transform.IsChildOf(root.transform))
            {
                EnsurePlayOnAwakeOff(assigned);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
                Debug.Log("Enemy prefab already has a child deathParticles reference; ensured playOnAwake is off.");
                return;
            }

            Transform childTr = root.transform.Find("DeathParticles");
            if (childTr != null)
            {
                if (childTr.TryGetComponent<ParticleSystem>(out var existingPs))
                {
                    prop.objectReferenceValue = existingPs;
                    EnsurePlayOnAwakeOff(existingPs);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(root);
                    PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
                    Debug.Log("Wired existing DeathParticles child to Enemy.deathParticles.");
                    return;
                }

                Object.DestroyImmediate(childTr.gameObject);
            }

            var go = new GameObject("DeathParticles");
            go.transform.SetParent(root.transform, false);
            go.layer = root.layer;
            var ps = go.AddComponent<ParticleSystem>();
            EnsurePlayOnAwakeOff(ps);
            prop.objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Debug.Log("Added DeathParticles child with ParticleSystem and assigned Enemy.deathParticles.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void EnsurePlayOnAwakeOff(ParticleSystem ps)
    {
        if (ps == null)
            return;
        var main = ps.main;
        main.playOnAwake = false;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        EditorUtility.SetDirty(ps);
    }

    /// <summary>For batch mode: <c>-executeMethod EnemyDeathParticlesPrefabSetup.BatchEnsure</c></summary>
    public static void BatchEnsure()
    {
        EnsureEnemyDeathParticlesChild();
        EditorApplication.Exit(0);
    }
}
