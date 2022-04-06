using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace UnitySweeper
{
    public class FindUnusedAssetsWindow : EditorWindow
    {
        AssetCollector collection = new AssetCollector();
        List<DeleteAsset> deleteAssets = new List<DeleteAsset>();
        Vector2 scroll;

        string searchTerm;

        [MenuItem("Window/Delete Unused Assets/Only Resource", false, 50)]
        static void InitWithoutCode()
        {
            var window = CreateInstance<FindUnusedAssetsWindow>();
            window.collection.useCodeStrip = false;
            window.collection.Collection(new[] {"Assets"});
            window.CopyDeleteFileList(window.collection.deleteFileList);

            window.Show();
        }

        [MenuItem("Window/Delete Unused Assets/Unused by Editor", false, 51)]
        static void InitWithout()
        {
            var window = CreateInstance<FindUnusedAssetsWindow>();
            window.collection.Collection(new[] {"Assets"});
            window.CopyDeleteFileList(window.collection.deleteFileList);

            window.Show();
        }

        [MenuItem("Window/Delete Unused Assets/Unused by Game", false, 52)]
        static void Init()
        {
            var window = CreateInstance<FindUnusedAssetsWindow>();
            window.collection.saveEditorExtensions = false;
            window.collection.Collection(new[] {"Assets"});
            window.CopyDeleteFileList(window.collection.deleteFileList);

            window.Show();
        }

        [MenuItem("Window/Delete Unused Assets/Clear cache")]
        static void ClearCache()
        {
            File.Delete(AssetCollector.EXPORT_XMP_PATH);
            File.Delete(ClassReferenceCollection.XMP_PATH);

            EditorUtility.DisplayDialog("Clear cache", "Clear cache", "OK");
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                EditorGUILayout.LabelField("delete unreference assets from buildsettings and resources");
            }

            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField("Space separated keywords for multiple filters. Use '-' prefix to exclude.");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            searchTerm = EditorGUILayout.TextField("Search: ", searchTerm);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("All", GUILayout.MaxWidth(100)))
            {
                foreach (var asset in deleteAssets)
                {
                    if (string.IsNullOrEmpty(asset.path))
                    {
                        continue;
                    }

                    if (!CheckSearch(asset.path, searchTerm))
                    {
                        continue;
                    }

                    asset.isDelete = true;
                }
            }

            if (GUILayout.Button("None", GUILayout.MaxWidth(100)))
            {
                foreach (var asset in deleteAssets)
                {
                    if (string.IsNullOrEmpty(asset.path))
                    {
                        continue;
                    }

                    if (!CheckSearch(asset.path, searchTerm))
                    {
                        continue;
                    }

                    asset.isDelete = false;
                }
            }

            EditorGUILayout.EndHorizontal();

            using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = scrollScope.scrollPosition;
                foreach (var asset in deleteAssets)
                {
                    if (string.IsNullOrEmpty(asset.path))
                    {
                        continue;
                    }

                    if (!CheckSearch(asset.path, searchTerm))
                    {
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        asset.isDelete = EditorGUILayout.Toggle(asset.isDelete, GUILayout.Width(20));
                        var icon = AssetDatabase.GetCachedIcon(asset.path);
                        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                        if (GUILayout.Button(asset.path, EditorStyles.largeLabel))
                        {
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(asset.path);
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope("box"))
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Exclude from Project", GUILayout.Width(160)) && deleteAssets.Count != 0)
                {
                    EditorApplication.delayCall += Exclude;
                }
            }
        }

        /// <summary>
        /// Checks if an asset should be shown based on search keyword
        /// </summary>
        /// <param name="path"></param>
        /// <param name="keyword"></param>
        /// <returns></returns>
        bool CheckSearch(string path, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm)) return true;

            path = path.ToLower();
            searchTerm = searchTerm.ToLower();

            string[] keywords = searchTerm.Split(' ');

            foreach (string keyword in keywords)
            {
                string curKeyword = keyword;

                if (curKeyword.Contains('-'))
                {
                    curKeyword = curKeyword.Replace("-", string.Empty);
                    if (path.Contains(curKeyword)) return false;
                }
                else if (!path.Contains(curKeyword)) return false;
            }

            return true;
        }

        void Exclude()
        {
            RemoveFiles();
            Close();
        }

        static void CleanDir()
        {
            RemoveEmptyDirectory("Assets");
            AssetDatabase.Refresh();
        }

        void CopyDeleteFileList(IEnumerable<string> deleteFileList)
        {
            foreach (var asset in deleteFileList)
            {
                var filePath = AssetDatabase.GUIDToAssetPath(asset);
                if (string.IsNullOrEmpty(filePath) == false)
                {
                    deleteAssets.Add(new DeleteAsset {path = filePath});
                }
            }
        }

        void RemoveFiles()
        {
            try
            {
                string exportDirectory = "BackupUnusedAssets";
                Directory.CreateDirectory(exportDirectory);
                var files = deleteAssets.Where(item => item.isDelete).Select(item => item.path).ToArray();
                string backupPackageName = exportDirectory + "/package" + DateTime.Now.ToString("yyyyMMddHHmmss") +
                                           ".unitypackage";
                EditorUtility.DisplayProgressBar("export package", backupPackageName, 0);

                AssetDatabase.ExportPackage(files, backupPackageName);

                int i = 0;
                int length = deleteAssets.Count;

                foreach (var assetPath in files)
                {
                    i++;
                    EditorUtility.DisplayProgressBar("delete unused assets", assetPath, (float) i / length);
                    AssetDatabase.DeleteAsset(assetPath);
                    if (File.Exists(assetPath))
                    {
                        File.Delete(assetPath);
                    }
                }

                EditorUtility.DisplayProgressBar("clean directory", "", 1);
                foreach (var dir in Directory.GetDirectories("Assets"))
                {
                    RemoveEmptyDirectory(dir);
                }

                Process.Start(exportDirectory);

                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void RemoveEmptyDirectory(string path)
        {
            var dirs = Directory.GetDirectories(path);
            foreach (var dir in dirs)
            {
                RemoveEmptyDirectory(dir);
            }


            var files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly)
                .Where(item => Path.GetExtension(item) != ".meta");
            if (!files.Any() && !Directory.GetDirectories(path).Any())
            {
                var metaFile = AssetDatabase.GetTextMetaFilePathFromAssetPath(path);
                FileUtil.DeleteFileOrDirectory(path);
                FileUtil.DeleteFileOrDirectory(metaFile);
            }
        }

        class DeleteAsset
        {
            public bool isDelete = true;
            public string path;
        }
    }
}