using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DExportExcels.Editor
{
    /// <summary>
    /// 资源路径批量复制器: Project 窗口右键批量复制资源路径为 Excel 可粘贴格式
    /// 支持多选文件/文件夹(递归展开), 三种路径格式, 后缀过滤
    /// </summary>
    public static class AssetPathCopier
    {
        private const string MenuRoot = "Assets/读表工具/";
        private const string FormatPref = "DExportExcels.PathFormat";      // 0=完整路径 1=无Assets前缀 2=无后缀 3=仅文件名
        private const string ExtFilterPref = "DExportExcels.ExtFilter";   // 空=全部, 例: ".prefab,.png"

        // ---- 复制资源路径(按当前格式) ----
        [MenuItem(MenuRoot + "复制资源路径", false, 1)]
        private static void CopyPaths()
        {
            var paths = CollectSelectedPaths();
            if (paths.Count == 0) { Debug.LogWarning("[读表工具] 未选中任何资源"); return; }
            int format = EditorPrefs.GetInt(FormatPref, 0);
            var sb = new StringBuilder();
            for (int i = 0; i < paths.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(ApplyFormat(paths[i], format));
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            string formatName = format == 0 ? "完整路径" : format == 1 ? "无Assets前缀" : format == 2 ? "无后缀" : "仅文件名";
            Debug.Log(string.Format("[读表工具] 已复制 {0} 条资源路径(格式: {1})", paths.Count, formatName));
        }

        // ---- 复制资源路径+文件名(两列, Tab 分隔, Excel 直接粘贴) ----
        [MenuItem(MenuRoot + "复制资源路径(含文件名列)", false, 2)]
        private static void CopyPathsWithNames()
        {
            var paths = CollectSelectedPaths();
            if (paths.Count == 0) { Debug.LogWarning("[读表工具] 未选中任何资源"); return; }
            var sb = new StringBuilder();
            for (int i = 0; i < paths.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(Path.GetFileNameWithoutExtension(paths[i]));
                sb.Append('\t');
                sb.Append(paths[i]);
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log(string.Format("[读表工具] 已复制 {0} 条(文件名⇥路径 两列, 可直接粘贴进 Excel)", paths.Count));
        }

        // ---- 路径格式切换 ----
        [MenuItem(MenuRoot + "路径格式/完整路径 (Assets/...)", false, 21)]
        private static void SetFormatFull() { EditorPrefs.SetInt(FormatPref, 0); }

        [MenuItem(MenuRoot + "路径格式/无 Assets 前缀", false, 22)]
        private static void SetFormatRelative() { EditorPrefs.SetInt(FormatPref, 1); }

        [MenuItem(MenuRoot + "路径格式/去掉后缀", false, 23)]
        private static void SetFormatNoExt() { EditorPrefs.SetInt(FormatPref, 2); }

        [MenuItem(MenuRoot + "路径格式/仅文件名", false, 24)]
        private static void SetFormatName() { EditorPrefs.SetInt(FormatPref, 3); }

        [MenuItem(MenuRoot + "路径格式/完整路径 (Assets/...)", true)]
        private static bool ValidateFull() { return EditorPrefs.GetInt(FormatPref, 0) == 0; }

        [MenuItem(MenuRoot + "路径格式/无 Assets 前缀", true)]
        private static bool ValidateRelative() { return EditorPrefs.GetInt(FormatPref, 1) == 1; }

        [MenuItem(MenuRoot + "路径格式/去掉后缀", true)]
        private static bool ValidateNoExt() { return EditorPrefs.GetInt(FormatPref, 2) == 2; }

        [MenuItem(MenuRoot + "路径格式/仅文件名", true)]
        private static bool ValidateName() { return EditorPrefs.GetInt(FormatPref, 3) == 3; }

        // ---- 后缀过滤 ----
        [MenuItem(MenuRoot + "后缀过滤/仅 Prefab", false, 41)]
        private static void FilterPrefab() { EditorPrefs.SetString(ExtFilterPref, ".prefab"); Debug.Log("[读表工具] 后缀过滤: .prefab"); }

        [MenuItem(MenuRoot + "后缀过滤/仅图片", false, 42)]
        private static void FilterImage() { EditorPrefs.SetString(ExtFilterPref, ".png,.jpg,.tga,.psd"); Debug.Log("[读表工具] 后缀过滤: 图片"); }

        [MenuItem(MenuRoot + "后缀过滤/仅音频", false, 43)]
        private static void FilterAudio() { EditorPrefs.SetString(ExtFilterPref, ".mp3,.ogg,.wav"); Debug.Log("[读表工具] 后缀过滤: 音频"); }

        [MenuItem(MenuRoot + "后缀过滤/清除过滤", false, 44)]
        private static void FilterClear() { EditorPrefs.SetString(ExtFilterPref, ""); Debug.Log("[读表工具] 后缀过滤已清除"); }

        [MenuItem(MenuRoot + "后缀过滤/仅 Prefab", true)]
        private static bool ValidateFilterPrefab() { return EditorPrefs.GetString(ExtFilterPref, "") != ".prefab"; }

        // ==================================================================

        private static List<string> CollectSelectedPaths()
        {
            var paths = new List<string>();
            var seen = new HashSet<string>();
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (Directory.Exists(path))
                {
                    CollectFolder(path, paths, seen);
                }
                else
                {
                    AddAsset(paths, path, seen);
                }
            }
            return paths;
        }

        private static void CollectFolder(string folder, List<string> paths, HashSet<string> seen)
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || Directory.Exists(path)) continue;
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                AddAsset(paths, path, seen);
            }
        }

        private static void AddAsset(List<string> paths, string path, HashSet<string> seen)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();

            // 后缀过滤(仅对文件夹递归展开的资源生效, 直接选中的单文件不过滤)
            string filter = EditorPrefs.GetString(ExtFilterPref, "");
            if (!string.IsNullOrEmpty(filter) && paths.Count > 0)
            {
                // 有过滤时只保留匹配后缀的资源
                bool match = false;
                foreach (var f in filter.Split(','))
                {
                    if (ext == f.Trim().ToLowerInvariant()) { match = true; break; }
                }
                if (!match) return;
            }

            if (seen.Add(path)) paths.Add(path);
        }

        private static string ApplyFormat(string path, int format)
        {
            switch (format)
            {
                case 1:  // 无 Assets/ 前缀
                    return path.StartsWith("Assets/") ? path.Substring(7) : path;
                case 2:  // 去掉后缀
                    return Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path)).Replace('\\', '/');
                case 3:  // 仅文件名
                    return Path.GetFileNameWithoutExtension(path);
                default: // 完整路径
                    return path;
            }
        }
    }
}
