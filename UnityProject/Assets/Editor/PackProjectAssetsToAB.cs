// Editor/PackProjectAssetsToAB.cs
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PackProjectAssetsToAB
{
    private const string OutputDir = "Assets/AssetBundles";

    [MenuItem("Tools/AssetBundles/Pack Selected Project Assets → Single AB (LZ4)")]
    public static void Pack_LZ4()
    {
        var opts = BuildAssetBundleOptions.ChunkBasedCompression;
        PackSelectedProjectAssetsToSingleAB(opts, "LZ4");
    }

    [MenuItem("Tools/AssetBundles/Pack Selected Project Assets → Single AB (LZMA)")]
    public static void Pack_LZMA()
    {
        // LZMA 就是不加壓縮選項（= None），但仍保留 Deterministic
        var opts = BuildAssetBundleOptions.None;
        PackSelectedProjectAssetsToSingleAB(opts, "LZMA");
    }

    [MenuItem("Tools/AssetBundles/Pack Selected Project Assets → Single AB (Uncompressed)")]
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
