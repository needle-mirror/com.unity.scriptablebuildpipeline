using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;

namespace UnityEditor.Build.Pipeline.WriteTypes
{
    /// <summary>
    /// Explicit implementation for writing a scene serialized file that can be used with the Asset Bundle systems.
    /// </summary>
    [Serializable]
    public class SceneBundleWriteOperation : IWriteOperation
    {
        /// <inheritdoc />
        public WriteCommand Command { get; set; }
        /// <inheritdoc />
        public BuildUsageTagSet UsageSet { get; set; }
        /// <inheritdoc />
        public BuildReferenceMap ReferenceMap { get; set; }

        /// <summary>
        /// Source scene asset path
        /// </summary>
        public string Scene { get; set; }

        /// <summary>
        /// Processed scene path returned by the ProcessScene API.
        /// <seealso cref="ContentBuildInterface.PrepareScene"/>
        /// </summary>
#if UNITY_2019_3_OR_NEWER
        [Obsolete("ProcessedScene has been deprecated.")]
#endif
        public string ProcessedScene { get; set; }

        /// <summary>
        /// Information needed for scene preloadeding.
        /// <seealso cref="PreloadInfo"/>
        /// </summary>
        public PreloadInfo PreloadInfo { get; set; }

        /// <summary>
        /// Information needed for generating the Asset Bundle object to be included in the serialized file.
        /// <see cref="SceneBundleInfo"/>
        /// </summary>
        public SceneBundleInfo Info { get; set; }

        /// <inheritdoc />
        public WriteResult Write(string outputFolder, BuildSettings settings, BuildUsageTagGlobal globalUsage)
        {
#if UNITY_2019_3_OR_NEWER
            return ContentBuildInterface.WriteSceneSerializedFile(outputFolder, new WriteSceneParameters
            {
                scenePath = Scene,
                writeCommand = Command,
                settings = settings,
                globalUsage = globalUsage,
                usageSet = UsageSet,
                referenceMap = ReferenceMap,
                preloadInfo = PreloadInfo,
                sceneBundleInfo = Info
            });
#else
            return ContentBuildInterface.WriteSceneSerializedFile(outputFolder, Scene, ProcessedScene, Command, settings, globalUsage, UsageSet, ReferenceMap, PreloadInfo, Info);
#endif
        }

        /// <inheritdoc />
        public Hash128 GetHash128()
        {
            var prefabHashes = AssetDatabase.GetDependencies(Scene).Where(path => path.EndsWith(".prefab")).Select(AssetDatabase.GetAssetDependencyHash);
#if UNITY_2019_3_OR_NEWER
            CacheEntry entry = BuildCacheUtility.GetCacheEntry(Scene);
#else
            CacheEntry entry = BuildCacheUtility.GetCacheEntry(ProcessedScene);
#endif
            HashSet<CacheEntry> hashObjects = new HashSet<CacheEntry>();

            if (Command.serializeObjects != null)
                foreach (var serializeObject in Command.serializeObjects)
                    hashObjects.Add(BuildCacheUtility.GetCacheEntry(serializeObject.serializationObject));

            return HashingMethods.Calculate(Command.GetHash128(), UsageSet.GetHash128(), ReferenceMap.GetHash128(), Scene, PreloadInfo.GetHash128(), entry, Info, prefabHashes, hashObjects).ToHash128();
        }
    }
}
