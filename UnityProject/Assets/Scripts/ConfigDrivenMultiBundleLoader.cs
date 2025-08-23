using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

[System.Serializable]
public class AssetEntry { public string path; public string type; }

[System.Serializable]
public class BundleConfigV2
{
    public int version;                 // 用於 Unity 內建快取的 version
    public List<string> bundle_urls;    // 多個 bundle URL
    public List<AssetEntry> assets;     // 要載入的資產清單
}

public class ConfigDrivenMultiBundleLoader : MonoBehaviour
{
    // 固定從這個 GitHub 連結抓設定（按你的要求）
    [TextArea]
    public string configUrl = "https://raw.githubusercontent.com/EricHaung/AssetBundleMemoryTest/main/config.json";

    private readonly List<AssetBundle> _bundles = new();
    private readonly List<Object> _loadedAssets = new();

    private void Start()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // 1) 下載 config.json
        string jsonUrl = configUrl;
        using (var cfgReq = UnityWebRequest.Get(jsonUrl))
        {
            yield return cfgReq.SendWebRequest();
            if (cfgReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Config] Download failed: {cfgReq.error}\nURL: {jsonUrl}");
                yield break;
            }

            var cfg = JsonUtility.FromJson<BundleConfigV2>(cfgReq.downloadHandler.text);
            if (cfg == null || cfg.bundle_urls == null)
            {
                Debug.LogError("[Config] Invalid or empty config.json");
                yield break;
            }

            uint version = (cfg.version < 0) ? 0u : (uint)cfg.version;

            // 2) 逐個下載 bundle
            foreach (var rawUrl in cfg.bundle_urls)
            {
                var bundleUrl = rawUrl;
                using var abReq = UnityWebRequestAssetBundle.GetAssetBundle(bundleUrl, version, 0);
                yield return abReq.SendWebRequest();
                if (abReq.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Bundle] Download failed: {abReq.error}\nURL: {bundleUrl}");
                    continue;
                }
                var ab = DownloadHandlerAssetBundle.GetContent(abReq);
                if (ab == null)
                {
                    Debug.LogError($"[Bundle] GetContent returned null\nURL: {bundleUrl}");
                    continue;
                }
                _bundles.Add(ab);
                Debug.Log($"[Bundle] Loaded: {bundleUrl} | Assets: {ab.GetAllAssetNames().Length}");
            }

            if (_bundles.Count == 0)
            {
                Debug.LogWarning("[Bundle] No bundles loaded.");
                yield break;
            }

            // 3) 依 config.assets 逐一載入（跨多個 bundle 嘗試）
            if (cfg.assets != null)
            {
                foreach (var a in cfg.assets)
                {
                    // --- 場景分流 ---
                    if (string.Equals(a.type, "Scene", System.StringComparison.OrdinalIgnoreCase))
                    {
                        bool sceneLoaded = false;

                        foreach (var b in _bundles)
                        {
                            // 直接嘗試用指定的 path 載入
                            var loadOp = SceneManager.LoadSceneAsync(a.path, LoadSceneMode.Additive);
                            if (loadOp != null)
                            {
                                yield return loadOp;
                                if (loadOp.isDone)
                                {
                                    Debug.Log($"[Scene] Loaded: {a.path} (from bundle '{b.name}')");
                                    sceneLoaded = true;
                                    break;
                                }
                            }
                        }

                        if (!sceneLoaded)
                            Debug.LogWarning($"[Scene] Failed to load: {a.path}");

                        continue; // 處理下一個 asset
                    }

                    // --- 非場景：一般資產載入 ---
                    var t = TypeFromName(a.type);
                    UnityEngine.Object loaded = null;

                    foreach (var b in _bundles)
                    {
                        var op = b.LoadAssetAsync(a.path, t);
                        yield return op;

                        if (op.asset != null)
                        {
                            loaded = op.asset;
                            _loadedAssets.Add(loaded);
                            Debug.Log($"[Asset] Loaded: {a.path} ({a.type}) from bundle '{b.name}'");
                            InstantiateIfGameObject(loaded);
                            break;
                        }
                    }

                    if (loaded == null)
                    {
                        Debug.LogWarning($"[Asset] Not found in any bundle: {a.path} ({a.type})");
                    }
                }
            }
        }
    }

    private static System.Type TypeFromName(string n)
    {
        return n switch
        {
            // Unity 常見資產
            "GameObject" => typeof(GameObject),
            "Sprite" => typeof(Sprite),
            "TextAsset" => typeof(TextAsset),
            "Texture2D" => typeof(Texture2D),
            "Material" => typeof(Material),
            "Mesh" => typeof(Mesh),
            "Shader" => typeof(Shader),

            // 音效 / 視頻
            "AudioClip" => typeof(AudioClip),
            "AnimationClip" => typeof(AnimationClip),
            "AnimatorController" => typeof(UnityEditor.Animations.AnimatorController), // 注意：僅 Editor 可用
            "VideoClip" => typeof(UnityEngine.Video.VideoClip),

            // UI / 字型
            "Font" => typeof(Font),
            "TextMeshPro" => typeof(TMPro.TMP_FontAsset),  // 需要 TextMeshPro 套件
            "TMP_SpriteAsset" => typeof(TMPro.TMP_SpriteAsset),

            // 場景
            "Scene" => typeof(UnityEngine.SceneManagement.Scene), // 一般用 LoadSceneAsync，不直接載 Type

            // fallback
            _ => typeof(Object)
        };
    }


    private static void InstantiateIfGameObject(Object asset)
    {
        if (asset is GameObject go) Instantiate(go);
    }

    private void OnDestroy()
    {
        StartCoroutine(Cleanup());
    }

    private IEnumerator Cleanup()
    {
        // 1) 釋放 AssetBundle（false 以保留已實例化的物件）
        foreach (var b in _bundles)
        {
            if (b != null) b.Unload(false);
        }
        _bundles.Clear();

        // 2) 可選：釋放未被引用的資源
        yield return Resources.UnloadUnusedAssets();
    }
}
