using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;

#if !UNITY_2019_3_OR_NEWER
using System.IO;
#endif

namespace UnityEditor.Build.Pipeline.Tasks
{
    /// <summary>
    /// Calculates the dependency data for all scenes.
    /// </summary>
    public class CalculateSceneDependencyData : IBuildTask
    {
        /// <inheritdoc />
        public int Version { get { return 4; } }

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IBuildParameters m_Parameters;

        [InjectContext(ContextUsage.In)]
        IBuildContent m_Content;

        [InjectContext]
        IDependencyData m_DependencyData;

        [InjectContext(ContextUsage.In, true)]
        IProgressTracker m_Tracker;

        [InjectContext(ContextUsage.In, true)]
        IBuildCache m_Cache;
#pragma warning restore 649

        CachedInfo GetCachedInfo(GUID scene, IEnumerable<ObjectIdentifier> references, SceneDependencyInfo sceneInfo, BuildUsageTagSet usageTags, IEnumerable<CacheEntry> prefabEntries, Hash128 prefabDependency)
        {
            var info = new CachedInfo();
            info.Asset = m_Cache.GetCacheEntry(scene, Version);

#if ENABLE_TYPE_HASHING || UNITY_2020_1_OR_NEWER
            var uniqueTypes = new HashSet<System.Type>(sceneInfo.includedTypes);
#else
            var uniqueTypes = new HashSet<System.Type>();
#endif
            var objectTypes = new List<KeyValuePair<ObjectIdentifier, System.Type[]>>();
            var dependencies = new HashSet<CacheEntry>(prefabEntries);
            ExtensionMethods.ExtractCommonCacheData(m_Cache, null, references, uniqueTypes, objectTypes, dependencies);
            info.Dependencies = dependencies.ToArray();

            info.Data = new object[] { sceneInfo, usageTags, prefabDependency, objectTypes };

            return info;
        }

        /// <inheritdoc />
        public ReturnCode Run()
        {
            if (m_Content.Scenes.IsNullOrEmpty())
                return ReturnCode.SuccessNotRun;

            IList<CachedInfo> cachedInfo = null;
            IList<CachedInfo> uncachedInfo = null;
            if (m_Parameters.UseCache && m_Cache != null)
            {
                IList<CacheEntry> entries = m_Content.Scenes.Select(x => m_Cache.GetCacheEntry(x, Version)).ToList();
                m_Cache.LoadCachedData(entries, out cachedInfo);

                uncachedInfo = new List<CachedInfo>();
            }

            BuildSettings settings = m_Parameters.GetContentBuildSettings();
            for (int i = 0; i < m_Content.Scenes.Count; i++)
            {
                GUID scene = m_Content.Scenes[i];
                string scenePath = AssetDatabase.GUIDToAssetPath(scene.ToString());

                SceneDependencyInfo sceneInfo;
                BuildUsageTagSet usageTags;
                Hash128 prefabDependency = new Hash128();

                if (cachedInfo != null && cachedInfo[i] != null)
                {
                    if (!m_Tracker.UpdateInfoUnchecked(string.Format("{0} (Cached)", scenePath)))
                        return ReturnCode.Canceled;

                    sceneInfo = (SceneDependencyInfo)cachedInfo[i].Data[0];
                    usageTags = cachedInfo[i].Data[1] as BuildUsageTagSet;
                    prefabDependency = (Hash128)cachedInfo[i].Data[2];
                    var objectTypes = cachedInfo[i].Data[3] as List<KeyValuePair<ObjectIdentifier, System.Type[]>>;
                    if (objectTypes != null)
                        BuildCacheUtility.SetTypeForObjects(objectTypes);
                }
                else
                {
                    if (!m_Tracker.UpdateInfoUnchecked(scenePath))
                        return ReturnCode.Canceled;

                    usageTags = new BuildUsageTagSet();

#if UNITY_2019_3_OR_NEWER
                    sceneInfo = ContentBuildInterface.CalculatePlayerDependenciesForScene(scenePath, settings, usageTags, m_DependencyData.DependencyUsageCache);
#else
                    string outputFolder = m_Parameters.TempOutputFolder;
                    if (m_Parameters.UseCache && m_Cache != null)
                        outputFolder = m_Cache.GetCachedArtifactsDirectory(m_Cache.GetCacheEntry(scene, Version));
                    Directory.CreateDirectory(outputFolder);

                    sceneInfo = ContentBuildInterface.PrepareScene(scenePath, settings, usageTags, m_DependencyData.DependencyUsageCache, outputFolder);
#endif
                    if (uncachedInfo != null)
                    {
                        // We only need to gather prefab dependencies and calculate the hash if we are using caching, otherwise we can skip it
                        var prefabEntries = AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(scene.ToString())).Where(path => path.EndsWith(".prefab")).Select(m_Cache.GetCacheEntry);
                        prefabDependency = HashingMethods.Calculate(prefabEntries).ToHash128();
                        uncachedInfo.Add(GetCachedInfo(scene, sceneInfo.referencedObjects, sceneInfo, usageTags, prefabEntries, prefabDependency));
                    }
                }

                SetOutputInformation(scene, sceneInfo, usageTags, prefabDependency);
            }

            if (m_Parameters.UseCache && m_Cache != null)
                m_Cache.SaveCachedData(uncachedInfo);

            return ReturnCode.Success;
        }

        void SetOutputInformation(GUID asset, SceneDependencyInfo sceneInfo, BuildUsageTagSet usageTags, Hash128 prefabDependency)
        {
            // Add generated scene information to BuildDependencyData
            m_DependencyData.SceneInfo.Add(asset, sceneInfo);
            m_DependencyData.SceneUsage.Add(asset, usageTags);
            m_DependencyData.DependencyHash.Add(asset, prefabDependency);
        }
    }
}
