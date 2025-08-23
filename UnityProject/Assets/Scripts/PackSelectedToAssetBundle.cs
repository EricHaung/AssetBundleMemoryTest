using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PackSelectedToAssetBundle
{
    private const string OutputDir = "Assets/AssetBundles";
    private const string TempPrefabDir = "Assets/Temp/ABPack";

    [MenuItem("Tools/AssetBundles/Pack Selected To AB (LZ4)")]
    public static void PackSelectedToAB()
    {
        var selection = Selection.objects;
        if (selection == null || selection.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "請先選取要打包的物件（可在 Scene 或 Project 視窗選）", "OK");
            return;
        }

        // 1) 取得可用的資產路徑；若是場景物件（非資產），則轉成臨時 Prefab
        EnsureDir(TempPrefabDir);
        var assetPaths = new List<string>();
        foreach (var obj in selection)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
            {
                // 直接是 Project 內資產
                assetPaths.Add(path);
                continue;
            }

            // 不是資產，多半是場景中的物件 → 嘗試找對應 Prefab，否則存成臨時 Prefab
            var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj);
            var prefabSrc = original != null ? original : obj;

            var prefabName = Sanitize(obj.name) + ".prefab";
            var savePath = Path.Combine(TempPrefabDir, prefabName).Replace("\\", "/");

            // 避免覆蓋同名，必要時加尾碼
            savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

            var go = (obj as GameObject) ?? (original as GameObject);
            if (go == null)
            {
                Debug.LogWarning($"[Skip] 非 GameObject 無法轉 Prefab：{obj.name}");
                continue;
            }

            var created = PrefabUtility.SaveAsPrefabAsset(go, savePath);
            if (created != null)
            {
                assetPaths.Add(savePath);
                Debug.Log($"[Temp Prefab] {savePath}");
            }
            else
            {
                Debug.LogWarning($"[Fail] 轉 Prefab 失敗：{obj.name}");
            }
        }

        if (assetPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Nothing to pack", "沒有可打包的資產路徑。", "OK");
            return;
        }

        // 2) 準備 AssetBundleBuild（單一 bundle，包含多個選取資產）
        var bundleName = BuildBundleNameFromSelection(selection);
        var build = new AssetBundleBuild
        {
            assetBundleName = bundleName,
            assetNames = assetPaths.Distinct().ToArray()
        };

        // 3) 建置
        EnsureDir(OutputDir);
        var options = BuildAssetBundleOptions.ChunkBasedCompression // LZ4：載入快
                    | BuildAssetBundleOptions.DeterministicAssetBundle;
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
            EditorUtility.DisplayDialog("Build Failed", "BuildPipeline.BuildAssetBundles 回傳 null。", "OK");
            return;
        }

        var outPath = Path.Combine(OutputDir, bundleName).Replace("\\", "/");
        EditorUtility.DisplayDialog("Build Done",
            $"已輸出 AssetBundle：\n{outPath}\n\n平台：{target}\n壓縮：LZ4", "OK");
        Debug.Log($"[AB] Built: {outPath}");
    }

    private static string BuildBundleNameFromSelection(UnityEngine.Object[] selection)
    {
        // 以第一個選取物件名 + 時戳 當 bundle 名，避免覆蓋
        var baseName = selection.Length > 0 ? Sanitize(selection[0].name) : "bundle";
        var time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{baseName}_{time}.ab".ToLowerInvariant();
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(s.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "asset" : cleaned;
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }
}
