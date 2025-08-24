// Editor/PackProjectAssetsToAB.cs
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PackProjectAssetsToAB
{
    [System.Serializable]
    private class GlobalConfig
    {
        public int version = 1;
        public List<string> bundle_urls = new List<string>();
        public List<AssetEntry> assets = new List<AssetEntry>();
    }

    [System.Serializable]
    private class AssetEntry
    {
        public string path;
        public string type;
    }

    private const string OutputDir = "Assets/AssetBundles";

    // [MenuItem("AssetBundles/Pack Selected Project Assets → Single AB (LZ4)")]
    // public static void Pack_LZ4()
    // {
    //     var opts = BuildAssetBundleOptions.ChunkBasedCompression;
    //     PackSelectedProjectAssetsToSingleAB(opts, "LZ4");
    // }

    // [MenuItem("AssetBundles/Pack Selected Project Assets → Single AB (LZMA)")]
    // public static void Pack_LZMA()
    // {
    //     // LZMA 就是不加壓縮選項（= None），但仍保留 Deterministic
    //     var opts = BuildAssetBundleOptions.None;
    //     PackSelectedProjectAssetsToSingleAB(opts, "LZMA");
    // }

    [MenuItem("AssetBundles/Pack Selected Project Assets → Single AB (Uncompressed)")]
    public static void Pack_Uncompressed()
    {
        var opts = BuildAssetBundleOptions.UncompressedAssetBundle;
        PackSelectedProjectAssetsToSingleAB(opts, "Uncompressed");
    }

    private static void PackSelectedProjectAssetsToSingleAB(BuildAssetBundleOptions options, string label)
    {
        var guids = Selection.assetGUIDs; // 只取 Project 視窗選取
        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog("No Project Assets",
                "請在『Project 視窗』選取資產或資料夾再執行。", "OK");
            return;
        }

        // 展開資料夾與資產
        var assetPaths = new HashSet<string>();
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            if (AssetDatabase.IsValidFolder(path))
            {
                var subGuids = AssetDatabase.FindAssets("", new[] { path });
                foreach (var sg in subGuids)
                {
                    var sp = AssetDatabase.GUIDToAssetPath(sg);
                    if (IsAssetFile(sp)) assetPaths.Add(sp);
                }
            }
            else
            {
                if (IsAssetFile(path)) assetPaths.Add(path);
            }
        }

        if (assetPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Nothing to pack",
                "選取內容沒有可打包的資產檔案。", "OK");
            return;
        }

        var bundleName = BuildBundleNameFromSelection(guids);
        var build = new AssetBundleBuild
        {
            assetBundleName = bundleName,
            assetNames = assetPaths.ToArray()
        };

        Directory.CreateDirectory(OutputDir);

        var target = EditorUserBuildSettings.activeBuildTarget;
        var manifest = BuildPipeline.BuildAssetBundles(
            OutputDir,
            new[] { build },
            options,
            target
        );

        AssetDatabase.Refresh();

        if (manifest == null)
        {
            EditorUtility.DisplayDialog("Build Failed",
                "BuildPipeline.BuildAssetBundles 回傳 null。", "OK");
            return;
        }

        var outPath = Path.Combine(OutputDir, bundleName).Replace("\\", "/");
        EditorUtility.DisplayDialog("Build Done",
            $"已輸出 AssetBundle：\n{outPath}\n\n平台：{target}\n壓縮：{label}", "OK");
        Debug.Log($"[AB] Built: {outPath}\n包含資產數：{assetPaths.Count}\n壓縮：{label}");

        // === 搬到專案上上層的 AssetBundles 目錄 ===
        string destDir = null;
        string destFile = null;
        try
        {
            string projectUpper = Path.GetFullPath(Path.Combine(Application.dataPath, "../..")); // 上上層
            destDir = Path.Combine(projectUpper, "AssetBundles");
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            string srcFile = Path.Combine(OutputDir, bundleName);
            destFile = Path.Combine(destDir, bundleName);

            File.Copy(srcFile, destFile, true);
            Debug.Log($"[AB] Copied bundle to: {destFile}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AB] Failed to copy bundle: {e}");
        }

        // === 從剛打好的 .ab 讀出內容清單，更新 config.json ===
        try
        {
            // 1) 列出這個 AB 內的所有資產
            var abFullPath = Path.Combine(OutputDir, bundleName);
            var ab = AssetBundle.LoadFromFile(abFullPath);
            if (ab == null)
            {
                Debug.LogWarning($"[AB] LoadFromFile failed for listing assets: {abFullPath}");
            }
            else
            {
                var names = ab.GetAllAssetNames(); // e.g. "assets/ui/prefabs/menuroot.prefab"
                                                   // 2) config.json 放在 AssetBundles 資料夾外面
                string configPath = Path.Combine(Path.GetDirectoryName(destDir)!, "config.json");

                // 3) 讀取或建立 config
                GlobalConfig cfg;
                if (File.Exists(configPath))
                {
                    try
                    {
                        var text = File.ReadAllText(configPath);
                        cfg = string.IsNullOrWhiteSpace(text)
                                ? new GlobalConfig()
                                : (JsonUtility.FromJson<GlobalConfig>(text) ?? new GlobalConfig());
                    }
                    catch
                    {
                        cfg = new GlobalConfig();
                    }
                }
                else
                {
                    cfg = new GlobalConfig();
                }

                // 3.1 確保 list 不為 null（避免 NRE）
                cfg.bundle_urls ??= new List<string>();
                cfg.assets ??= new List<AssetEntry>();

                // 4) version +1
                cfg.version = Math.Max(cfg.version + 1, 1);

                // 5) 追加 bundle URL（去重）
                var rawUrl = $"https://raw.githubusercontent.com/EricHaung/AssetBundleMemoryTest/main/AssetBundles/{bundleName}";
                if (!cfg.bundle_urls.Any(u => string.Equals(u, rawUrl, StringComparison.OrdinalIgnoreCase)))
                    cfg.bundle_urls.Add(rawUrl);

                // 6) 追加本次 AB 的 assets（path + type）
                foreach (var name in names)
                {
                    // 6.1 取得型別：直接從 AB 載出主物件看型別（避免 AssetDatabase 偵測失敗）
                    UnityEngine.Object obj = null;
                    try { obj = ab.LoadAsset(name); } catch { /* 忽略載入失敗 */ }
                    var typeName = obj != null ? obj.GetType().Name : "Object";

                    // 6.2 轉成你要的 path 格式：去掉 "assets/" 前綴與副檔名
                    var clean = name;
                    if (clean.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                        clean = clean.Substring("assets/".Length);
                    var ext = Path.GetExtension(clean);
                    if (!string.IsNullOrEmpty(ext)) clean = clean.Substring(0, clean.Length - ext.Length);

                    // 6.3 去重（null-safe）
                    bool exists = cfg.assets.Any(a =>
                        a != null &&
                        !string.IsNullOrEmpty(a.path) &&
                        a.path.Equals(clean, StringComparison.OrdinalIgnoreCase));

                    if (!exists)
                    {
                        cfg.assets.Add(new AssetEntry { path = clean, type = typeName });
                    }
                }

                // 7) 寫回 config.json（排序可選）
                cfg.assets = cfg.assets
                    .Where(a => a != null && !string.IsNullOrEmpty(a.path))
                    .OrderBy(a => a.path, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var pretty = JsonUtility.ToJson(cfg, true);
                File.WriteAllText(configPath, pretty);
                Debug.Log($"[AB] Updated config.json: {configPath}\n- version++\n- add url: {rawUrl}\n- add {names.Length} assets");

                ab.Unload(false);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AB] Failed to update config.json: {e}");
        }

        // === 刪除 Assets/AssetBundles 裡的本地副本，保持專案乾淨 ===
        try
        {
            string srcFile = Path.Combine(OutputDir, bundleName);
            if (File.Exists(srcFile))
            {
                File.Delete(srcFile);

                // 同名 .manifest 也順手清掉（若存在）
                string srcManifest = srcFile + ".manifest";
                if (File.Exists(srcManifest)) File.Delete(srcManifest);

                // Unity 會在輸出資料夾生成目錄主檔與其 .manifest，可選擇保留做除錯用
                // 若不想保留，也可刪掉：
                string dirMain = Path.Combine(OutputDir, Path.GetFileName(OutputDir));
                if (File.Exists(dirMain)) File.Delete(dirMain);
                string dirMainManifest = dirMain + ".manifest";
                if (File.Exists(dirMainManifest)) File.Delete(dirMainManifest);

                Debug.Log($"[AB] Removed local copy: {srcFile}");
                AssetDatabase.Refresh();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AB] Failed to clean local bundle: {e}");
        }
    }

    // 僅允許實際可打包的檔案（可依需求擴充）
    private static bool IsAssetFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;      // 腳本不打包
        if (path.StartsWith("Assets/Editor/", StringComparison.OrdinalIgnoreCase)) return false;

        string[] ok = {
            ".prefab",".png",".jpg",".jpeg",".tga",".psd",".bmp",".exr",
            ".mat",".shader",".shadervariants",".asset",
            ".controller",".anim",
            ".fbx",".obj",
            ".wav",".mp3",".ogg",
            ".txt",".bytes",".json",
            ".ttf",".otf",
            ".unity" // 場景也可打包（執行期用 SceneManager 載入）
        };
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ok.Contains(ext);
    }

    private static string BuildBundleNameFromSelection(string[] guids)
    {
        var firstPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        var baseName = Path.GetFileNameWithoutExtension(firstPath);
        baseName = Sanitize(baseName);
        var time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{baseName}_{time}.ab".ToLowerInvariant();
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(s.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "bundle" : cleaned;
    }
}
