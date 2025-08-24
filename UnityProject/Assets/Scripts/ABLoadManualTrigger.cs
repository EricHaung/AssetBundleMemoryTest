// Assets/Scripts/ABLoadManualTrigger.cs （非 Editor 腳本）
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;

public class ABLoadManualTrigger : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField]
    private ConfigDrivenMultiBundleLoader loader;  // 指到場上的 Loader
    private string onGUIButtonText = "Load AssetBundles (manual)";
    private float beforeTotalMB;
    private float afterTotalMB;
    private float deltaTotalMB;

    private float beforeMonoMB;
    private float afterMonoMB;
    private float deltaMonoMB;

    private float beforeGfxMB;
    private float afterGfxMB;
    private float deltaGfxMB;

    private bool _isRunning;

    // 給 UI Button 綁定的事件
    public void OnClickLoad()
    {
        if (_isRunning || loader == null) return;
        StartCoroutine(RunAndMeasure());
    }

    IEnumerator RunAndMeasure()
    {
        _isRunning = true;

        // 1) 前測
        yield return Resources.UnloadUnusedAssets(); // 讓數字乾淨一些
        Measure(out beforeTotalMB, out beforeMonoMB, out beforeGfxMB);

        // 2) 執行載入（呼叫你 Loader 的公開方法）
        yield return loader.StartLoading();

        // 3) 後測
        yield return Resources.UnloadUnusedAssets(); // 載入後也清一次，觀察純增量
        Measure(out afterTotalMB, out afterMonoMB, out afterGfxMB);

        // 4) 差值
        deltaTotalMB = afterTotalMB - beforeTotalMB;
        deltaMonoMB = afterMonoMB - beforeMonoMB;
        deltaGfxMB = afterGfxMB - beforeGfxMB;

        Debug.Log($"[AB][Mem] Total: {beforeTotalMB:F1} → {afterTotalMB:F1} (Δ {deltaTotalMB:F1} MB)");
        Debug.Log($"[AB][Mem]  Mono: {beforeMonoMB:F1} → {afterMonoMB:F1} (Δ {deltaMonoMB:F1} MB)");
        Debug.Log($"[AB][Mem]   Gfx: {beforeGfxMB:F1} → {afterGfxMB:F1} (Δ {deltaGfxMB:F1} MB)");

        _isRunning = false;
    }

    static void Measure(out float totalMB, out float monoMB, out float gfxMB)
    {
        // 這幾個 API 在不同平台回報略有差異，但足夠對比前後變化
        long total = Profiler.GetTotalAllocatedMemoryLong();   // 包含原生堆
        long mono = Profiler.GetMonoUsedSizeLong();           // C# Heap
        long gfx = Profiler.GetAllocatedMemoryForGraphicsDriver();
        totalMB = total / (1024f * 1024f);
        monoMB = mono / (1024f * 1024f);
        gfxMB = gfx / (1024f * 1024f);
    }

    void OnGUI()
    {
        const int w = 560, h = 100;

        // 定義一個大字體的 style
        GUIStyle bigButton = new GUIStyle(GUI.skin.button);
        bigButton.fontSize = 24;

        GUIStyle bigLabel = new GUIStyle(GUI.skin.label);
        bigLabel.fontSize = 24;
        bigLabel.alignment = TextAnchor.MiddleLeft;

        // 按鈕
        var rect = new Rect(20, 20, w, h);
        if (GUI.Button(rect, _isRunning ? "Loading..." : onGUIButtonText, bigButton))
        {
            OnClickLoad();
        }

        // 顯示上次結果
        var info = $"Total Δ {deltaTotalMB:F1} MB | Mono Δ {deltaMonoMB:F1} MB | Gfx Δ {(deltaGfxMB > 0 ? deltaGfxMB.ToString("F1") : "N/A")} MB";
        GUI.Label(new Rect(20, 20 + h + 16, w, 48), info, bigLabel);
    }
}
