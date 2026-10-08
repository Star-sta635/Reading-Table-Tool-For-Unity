using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DExportExcels.Editor
{
    /// <summary>
    /// 读表工具编辑器菜单: 校验路径列 / 热重载配置表 / 打开数据目录
    /// </summary>
    public static class TableEditorMenu
    {
        private const string DataDirKey = "DExportExcels.DataDir";

        // ==================================================================
        // 热重载配置表: 运行中/编辑模式均可, 不重启游戏刷新表数据
        // ==================================================================
        [MenuItem("读表工具/热重载配置表 %#R", false, 0)]
        private static void HotReload()
        {
            string dir = GetDataDir();
            if (!Directory.Exists(dir))
            {
                Debug.LogError("[Table] 数据目录不存在: " + dir + " (读表工具/设置数据目录)");
                return;
            }
            var datas = new Dictionary<string, byte[]>();
            foreach (string file in Directory.GetFiles(dir, "*.bytes"))
            {
                datas[Path.GetFileNameWithoutExtension(file)] = File.ReadAllBytes(file);
            }
            TableManager.LoadAll(datas);
            Debug.Log("[Table] 热重载完成: " + datas.Count + " 张表");
        }

        // ==================================================================
        // 校验路径列: 扫描全部表数据的 string 字段, 凡以 "Assets/" 开头的值
        // 检查对应资源是否存在, 输出失效路径清单(防策划删资源忘改表)
        // ==================================================================
        [MenuItem("读表工具/校验路径列(资源存在性)", false, 1)]
        private static void ValidatePaths()
        {
            string dir = GetDataDir();
            if (!Directory.Exists(dir))
            {
                Debug.LogError("[Table] 数据目录不存在: " + dir);
                return;
            }

            var datas = new Dictionary<string, byte[]>();
            foreach (string file in Directory.GetFiles(dir, "*.bytes"))
            {
                datas[Path.GetFileNameWithoutExtension(file)] = File.ReadAllBytes(file);
            }
            TableManager.LoadAll(datas);

            var failures = new List<string>();
            int checkedCount = 0;
            object inst = TableManager.Instance;

            foreach (FieldInfo mgrField in typeof(TableManager).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object mgr = mgrField.GetValue(inst);
                if (mgr == null) continue;

                IEnumerable rows = GetRows(mgr);
                if (rows == null) continue;

                int rowIndex = 0;
                foreach (object row in rows)
                {
                    rowIndex++;
                    foreach (FieldInfo f in row.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (f.FieldType != typeof(string)) continue;
                        string v = f.GetValue(row) as string;
                        if (string.IsNullOrEmpty(v) || !v.StartsWith("Assets/")) continue;
                        checkedCount++;
                        if (AssetDatabase.LoadAssetAtPath(v, typeof(UnityEngine.Object)) == null)
                        {
                            failures.Add(string.Format("[{0}] 第{1}行 字段\"{2}\": 资源不存在 {3}",
                                mgrField.Name, rowIndex, f.Name, v));
                        }
                    }
                }
            }

            if (failures.Count == 0)
            {
                Debug.Log(string.Format("[Table] 路径校验通过: 共检查 {0} 条资源路径, 全部存在", checkedCount));
            }
            else
            {
                Debug.LogError(string.Format("[Table] 路径校验发现 {0} 条失效路径:", failures.Count));
                foreach (string f in failures)
                {
                    Debug.LogError("[Table] " + f);
                }
            }
        }

        // ==================================================================
        // 打开数据目录
        // ==================================================================
        [MenuItem("读表工具/打开数据目录", false, 2)]
        private static void OpenDataDir()
        {
            string dir = GetDataDir();
            if (Directory.Exists(dir))
            {
                EditorUtility.RevealInFinder(dir);
            }
            else
            {
                Debug.LogError("[Table] 数据目录不存在: " + dir);
            }
        }

        // ==================================================================
        // 数据目录管理
        // ==================================================================
        [MenuItem("读表工具/设置数据目录...", false, 20)]
        private static void SetDataDir()
        {
            string current = GetDataDir();
            string dir = EditorUtility.OpenFolderPanel(
                "选择数据目录(.bytes 所在)", current, "");
            if (string.IsNullOrEmpty(dir)) return;
            if (dir.StartsWith(Application.dataPath))
            {
                dir = "Assets" + dir.Substring(Application.dataPath.Length);
            }
            EditorPrefs.SetString(DataDirKey, dir);
            Debug.Log("[Table] 数据目录已设置: " + dir);
        }

        // ==================================================================
        // 内部工具
        // ==================================================================
        private static string GetDataDir()
        {
            return EditorPrefs.GetString(DataDirKey, "Assets/Game/Download/DataTable");
        }

        private static IEnumerable GetRows(object mgr)
        {
            PropertyInfo listProp = mgr.GetType().GetProperty("List");
            if (listProp != null) return listProp.GetValue(mgr, null) as IEnumerable;
            PropertyInfo dictProp = mgr.GetType().GetProperty("Dict");
            if (dictProp != null)
            {
                object dict = dictProp.GetValue(mgr, null);
                PropertyInfo valuesProp = dict.GetType().GetProperty("Values");
                return valuesProp.GetValue(dict, null) as IEnumerable;
            }
            return null;
        }
    }
}
