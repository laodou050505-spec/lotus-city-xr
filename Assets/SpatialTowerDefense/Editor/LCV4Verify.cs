// v4 ProjectSetup.Verify + 场景统计 — batchmode 入口
// -executeMethod LCV4Verify.Run
using System.IO;
using PicoTowerDefenseEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LCV4Verify
{
    public static void Run()
    {
        const string scene = "Assets/SpatialTowerDefense/Scenes/SpatialDefense.unity";
        EditorSceneManager.OpenScene(scene);
        var gos  = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        var cols  = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include);
        var rbs   = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include);
        var mbs   = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        string stats = $"=== LC 场景统计 v4 ===\nGameObjects={gos.Length}\nColliders={cols.Length}\nRigidbodies={rbs.Length}\nMonoBehaviours={mbs.Length}\n";
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/LC_scene_stats_v4.txt", stats);
        Debug.Log(stats);
        ProjectSetup.Verify();
        File.AppendAllText("Logs/LC_scene_stats_v4.txt", "Verify=PASS\n");
        Debug.Log("[v4 QA] ProjectSetup.Verify PASSED");
    }
}
