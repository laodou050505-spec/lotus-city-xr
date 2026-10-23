// v4：一念莲城 Arsenal 面板截图——验证波次计数器位置
// -executeMethod LCV4ArsenalCapture.Run
using System.IO;
using PicoTowerDefense;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LCV4ArsenalCapture
{
    const string ScenePath = "Assets/SpatialTowerDefense/Scenes/SpatialDefense.unity";
    const string OutDir    = "VisualQA/EN-v4";
    const int    W         = 1920;
    const int    H         = 1080;

    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 相机放置在面板正前方
        var camGO = new GameObject("~V4_LC_Cam");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.10f);
        cam.fieldOfView     = 30f;
        cam.nearClipPlane   = 0.01f;
        cam.farClipPlane    = 10f;

        // 控件根节点：(0,1.03,-1.40)；面板面朝 +Z（玩家在 z=0 处看过来）
        // 相机放在面板后方 z=-2.20，向 +Z 看——与玩家视角同侧
        cam.transform.position = new Vector3(0.30f, 0.97f, -2.20f);
        cam.transform.LookAt(new Vector3(0.30f, 0.97f, -1.40f));

        // 创建 Arsenal 根节点
        var root = new GameObject("~V4_ControlsRoot").transform;
        root.position = new Vector3(0f, 1.03f, -1.40f);

        // 只建第 4 张卡（index=3, x=0.30）
        const float width  = 0.18f;
        const float height = 0.36f;
        const float x      = 0.30f; // (3 - 1.5) * 0.20

        GeneratedLotusPanel.Create(root, "Arsenal Card 3", "Arsenal",
            new Vector2(width, height), new Vector3(x, 0f, 0f), 3);

        var digits = GeneratedLotusPanel.Digits(root, new Vector3(x, -0.155f, -0.003f), 0.029f);

        // 截三个波次状态
        string[] waveLabels = { "1/25", "10/25", "25/25" };
        foreach (var lbl in waveLabels)
        {
            digits.Set(lbl);
            RenderToCam(cam, $"{OutDir}/arsenal_wave_{lbl.Replace("/","of")}_v4.png");
            Debug.Log($"[v4 LC Capture] 已保存 {OutDir}/arsenal_wave_{lbl.Replace("/", "of")}_v4.png");
        }

        Object.DestroyImmediate(root.gameObject);
        Object.DestroyImmediate(camGO);
    }

    static void RenderToCam(Camera cam, string outPng)
    {
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing      = 4;
        cam.targetTexture    = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active  = null;
        cam.targetTexture     = null;
        File.WriteAllBytes(outPng, tex.EncodeToPNG());
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
