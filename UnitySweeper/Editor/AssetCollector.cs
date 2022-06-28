using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using UnityEditor;

namespace UnitySweeper
{
    public class AssetCollector
    {
        public const string EXPORT_XMP_PATH = "referencemap.xml";

        public readonly List<string> deleteFileList = new List<string>();
        private List<CollectionData> referenceCollection = new List<CollectionData>();

        public bool useCodeStrip = true;
        public bool saveEditorExtensions = true;

        public void Collection(string[] collectionFolders)
        {
            try
            {
                var serialize = new XmlSerializer(typeof(List<CollectionData>));
                deleteFileList.Clear();
                referenceCollection.Clear();

                if (File.Exists(EXPORT_XMP_PATH))
                {
                    using (var fileStream = new StreamReader(EXPORT_XMP_PATH))
                    {
                        referenceCollection = (List<CollectionData>) serialize.Deserialize(fileStream);
                        fileStream.Close();
                    }
                }
                var collectionList = new List<IReferenceCollection>();

                if (useCodeStrip)
                {
                    collectionList.Add(new ClassReferenceCollection(saveEditorExtensions));
                }

                collectionList.AddRange(new IReferenceCollection[]
                {
                    new ShaderReferenceCollection(),
                    new AssetReferenceCollection()
                });

                foreach (var collection in collectionList)
                {
                    collection.Init(referenceCollection);
                    collection.CollectionFiles();
                }

                // Find assets
                var files = StripTargetPathsAll(useCodeStrip, collectionFolders);

                foreach (var path in files)
                {
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    deleteFileList.Add(guid);
                }

                EditorUtility.DisplayProgressBar("checking", "collection all files", 0.2f);
                UnregisterReferenceFromResources();

                EditorUtility.DisplayProgressBar("checking", "check reference from resources", 0.4f);
                UnregisterReferenceFromScenes();

                EditorUtility.DisplayProgressBar("checking", "check reference from scenes", 0.6f);
                if (saveEditorExtensions)
                {
                    UnregisterEditorCodes();
                }

                EditorUtility.DisplayProgressBar("checking", "check reference from ignorelist", 0.8f);
                UnregisterReferenceFromIgnoreList();
                UnregisterReferenceFromExtensionMethod();

                using (var fileStream = new StreamWriter(EXPORT_XMP_PATH))
                {
                    serialize.Serialize(fileStream, referenceCollection);
                    fileStream.Close();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private List<string> StripTargetPathsAll(bool isUseCodeStrip, string[] paths)
        {
            var files = paths.SelectMany(c => Directory.GetFiles(c, "*.*", SearchOption.AllDirectories))
                .Distinct()
                .Where(item => Path.GetExtension(item) != ".meta")
                .Where(item => Path.GetExtension(item) != ".js")
                .Where(item => Path.GetExtension(item) != ".dll")
                .Where(item => Regex.IsMatch(item, "[\\/\\\\]Gizmos[\\/\\\\]") == false)
                .Where(item => Regex.IsMatch(item, "[\\/\\\\]Plugins[\\/\\\\]Android[\\/\\\\]") == false)
                .Where(item => Regex.IsMatch(item, "[\\/\\\\]Plugins[\\/\\\\]iOS[\\/\\\\]") == false)
                .Where(item => Regex.IsMatch(item, "[\\/\\\\]Resources[\\/\\\\]") == false);

            if (isUseCodeStrip == false)
            {
                files = files.Where(item => Path.GetExtension(item) != ".cs");
            }

            return files.ToList();
        }

        private void UnregisterReferenceFromIgnoreList()
        {
            var codePaths = deleteFileList.Where(fileName => Path.GetExtension(fileName) == ".cs");

            foreach (var path in codePaths)
            {
                var code = ClassReferenceCollection.StripComment(File.ReadAllText(path));
                if (Regex.IsMatch(code, "static\\s*(partial)*\\s*class"))
                {
                    UnregisterFromDeleteList(AssetDatabase.AssetPathToGUID(path));
                }
            }
        }

        private void UnregisterReferenceFromExtensionMethod()
        {
            var resourcesFiles = deleteFileList
                .Where(item => Path.GetExtension(item) != ".meta")
                .ToArray();
            foreach (var path in AssetDatabase.GetDependencies(resourcesFiles))
            {
                UnregisterFromDeleteList(AssetDatabase.AssetPathToGUID(path));
            }
        }

        private void UnregisterReferenceFromResources()
        {
            var resourcesFiles = deleteFileList
                .Where(item => Regex.IsMatch(item, "[\\/\\\\]Resources[\\/\\\\]"))
                .Where(item => Path.GetExtension(item) != ".meta")
                .ToArray();
            foreach (var path in AssetDatabase.GetDependencies(resourcesFiles))
            {
                UnregisterFromDeleteList(AssetDatabase.AssetPathToGUID(path));
            }
        }

        private void UnregisterReferenceFromScenes()
        {
            // Exclude objects that reference from scenes.
            var scenes = EditorBuildSettings.scenes
                .Where(item => item.enabled)
                .Select(item => item.path)
                .ToArray();
            foreach (var path in AssetDatabase.GetDependencies(scenes))
            {
                UnregisterFromDeleteList(AssetDatabase.AssetPathToGUID(path));
            }
        }

        private void UnregisterEditorCodes()
        {
            // Exclude objects that reference from Editor API
            var editorCodes = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories)
                .Where(fileName => Path.GetExtension(fileName) == ".cs")
                .Where(item => Regex.IsMatch(item, "[\\/\\\\]Editor[\\/\\\\]"))
                .ToArray();

            EditorUtility.DisplayProgressBar("checking", "check reference from editor codes", 0.8f);

            foreach (var path in editorCodes)
            {
                var code = ClassReferenceCollection.StripComment(File.ReadAllText(path));
                if (Regex.IsMatch(code, "(\\[MenuItem|AssetPostprocessor|EditorWindow)"))
                {
                    UnregisterFromDeleteList(AssetDatabase.AssetPathToGUID(path));
                }
            }

            foreach (var path in editorCodes)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);

                if (referenceCollection.Exists(c => c.fileGuid == guid) == false)
                {
                    continue;
                }

                var referenceGuids = referenceCollection.First(c => c.fileGuid == guid).referenceGuids;


                if (referenceGuids.Any(c => deleteFileList.Contains(c)) == false)
                {
                    UnregisterFromDeleteList(AssetDatabase.AssetPathToGUID(path));
                }
            }
        }

        private void UnregisterFromDeleteList(string guid)
        {
            if (deleteFileList.Contains(guid) == false)
            {
                return;
            }

            deleteFileList.Remove(guid);

            if (referenceCollection.Exists(c => c.fileGuid == guid))
            {
                var refInfo = referenceCollection.First(c => c.fileGuid == guid);
                foreach (var referenceGuid in refInfo.referenceGuids)
                {
                    UnregisterFromDeleteList(referenceGuid);
                }
            }
        }
    }
}