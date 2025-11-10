#if UNITY_2022_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Pipeline.WriteTypes;
using UnityEditor.Build.Utilities;
using UnityEngine;

namespace UnityEditor.Build.Pipeline.Tasks
{
    public interface IClusterOutput : IContextObject
    {
        Dictionary<ObjectIdentifier, Hash128> ObjectToCluster { get; }
        Dictionary<ObjectIdentifier, long> ObjectToLocalID { get; }
    }

    public class ClusterOutput : IClusterOutput
    {
        private Dictionary<ObjectIdentifier, Hash128> m_ObjectToCluster = new Dictionary<ObjectIdentifier, Hash128>();
        private Dictionary<ObjectIdentifier, long> m_ObjectToLocalID = new Dictionary<ObjectIdentifier, long>();
        public Dictionary<ObjectIdentifier, Hash128> ObjectToCluster { get { return m_ObjectToCluster; } }
        public Dictionary<ObjectIdentifier, long> ObjectToLocalID { get { return m_ObjectToLocalID; } }
    }

    /// <summary>
    /// Build task for creating content archives based asset co-location.
    /// </summary>
    public class ClusterBuildLayout : IBuildTask
    {
        private static void GetOrAdd<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key, out TValue value) where TValue : new()
        {
            if (dictionary.TryGetValue(key, out value))
                return;

            value = new TValue();
            dictionary.Add(key, value);
        }

        public ClusterBuildLayout()
        {
            m_useContentIdsForClusterName = false;
        }

        public ClusterBuildLayout(bool useContentIdsForClusterName)
        {
            m_useContentIdsForClusterName = useContentIdsForClusterName;
        }

        bool m_useContentIdsForClusterName;

        /// <inheritdoc />
        public int Version { get { return 2; } }

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IBundleBuildParameters m_Parameters;

        [InjectContext(ContextUsage.In)]
        IDependencyData m_DependencyData;

        [InjectContext]
        IBundleWriteData m_WriteData;

        [InjectContext(ContextUsage.In)]
        IDeterministicIdentifiers m_PackingMethod;

        [InjectContext]
        IBuildResults m_Results;

        [InjectContext]
        IClusterOutput m_ClusterResult;
#pragma warning restore 649

        /// <inheritdoc />
        public ReturnCode Run()
        {
            return Run(m_Parameters, m_DependencyData, m_WriteData, m_PackingMethod, m_ClusterResult, m_useContentIdsForClusterName);
        }

        internal static ReturnCode Run(IBundleBuildParameters buildParams, IDependencyData dependencyData, IBundleWriteData writeData, IDeterministicIdentifiers packingMethod, IClusterOutput clusterResult, bool useContentIdsForClusterName)
        {
            // Create mapping of objects to all assets that depend on them
            var objectToAssets = new Dictionary<ObjectIdentifier, HashSet<GUID>>();
            foreach (var pair in dependencyData.AssetInfo)
            {
                ExtractAssets(objectToAssets, pair.Key, pair.Value.includedObjects);
                ExtractAssets(objectToAssets, pair.Key, pair.Value.referencedObjects);
            }
            foreach (var pair in dependencyData.SceneInfo)
            {
                ExtractAssets(objectToAssets, pair.Key, pair.Value.referencedObjects);
            }

            //create clusters of object ids based on having the same assets referencing them
            //the cluster ids here are created from the asset ids
            var tempClusterToObjects = new Dictionary<Hash128, HashSet<ObjectIdentifier>>();
            foreach (var pair in objectToAssets)
            {
                HashSet<GUID> assets = pair.Value;
                Hash128 cluster = HashingMethods.Calculate(assets.OrderBy(x => x)).ToHash128();
                GetOrAdd(tempClusterToObjects, cluster, out var objectIds);
                objectIds.Add(pair.Key);
            }

            //create the final clusters with names based on the ids of the objects contained
            var finalClusterToObjects = new Dictionary<Hash128, List<ObjectIdentifier>>();
            foreach (var pair in tempClusterToObjects)
            {
                var objectsInCluster = pair.Value.ToList();
#if UNITY_6000_0_OR_NEWER
                objectsInCluster.Sort();
#else
                objectsInCluster.Sort((a, b) => a.GetHashCode().CompareTo(b.GetHashCode()));
#endif
                var clusterId = useContentIdsForClusterName ? ComputeClusterId(objectsInCluster) : pair.Key;
                finalClusterToObjects.Add(clusterId, objectsInCluster);
                foreach (var o in objectsInCluster)
                    clusterResult.ObjectToCluster.TryAdd(o, clusterId);
            }

            // From clusters, create the final write data
            BuildUsageTagSet usageSet = new BuildUsageTagSet();
            foreach (var pair in dependencyData.AssetUsage)
                usageSet.UnionWith(pair.Value);
            foreach (var pair in dependencyData.SceneUsage)
                usageSet.UnionWith(pair.Value);

            var builtInResourcesGUID = new GUID("0000000000000000e000000000000000");


            // Generates Content Archive Files from Clusters
            foreach (var pair in finalClusterToObjects)
            {
                var objectsInCluster = pair.Value;
                var clusterName = pair.Key.ToString();
                writeData.FileToObjects.Add(clusterName, objectsInCluster);
#pragma warning disable CS0618 // Type or member is obsolete
                var op = new RawWriteOperation();
#pragma warning restore CS0618 // Type or member is obsolete
                writeData.WriteOperations.Add(op);
                op.ReferenceMap = new BuildReferenceMap();
                op.Command = new WriteCommand();
                op.Command.fileName = clusterName;
                op.Command.internalName = clusterName;
                op.Command.serializeObjects = new List<SerializationInfo>();
                foreach (var objectId in objectsInCluster)
                {
                    var lfid = packingMethod.SerializationIndexFromObjectIdentifier(objectId);
                    op.Command.serializeObjects.Add(new SerializationInfo { serializationObject = objectId, serializationIndex = lfid });
                    op.ReferenceMap.AddMapping(clusterName, lfid, objectId);
                    clusterResult.ObjectToLocalID.Add(objectId, lfid);
                }
                var deps = ContentBuildInterface.GetPlayerDependenciesForObjects(objectsInCluster.ToArray(), buildParams.Target, buildParams.ScriptInfo, DependencyType.ValidReferences);
                foreach (var d in deps)
                {
                    if (d.m_GUID != builtInResourcesGUID)
                    {
                        var depCluster = clusterResult.ObjectToCluster[d].ToString();
                        op.ReferenceMap.AddMapping(depCluster, packingMethod.SerializationIndexFromObjectIdentifier(d), d);
                    }
                }

                op.UsageSet = usageSet;

                writeData.FileToBundle.Add(clusterName, clusterName);
            }

            // Generates Content Scene Archive Files from Scene Input
            foreach (var pair in dependencyData.SceneInfo)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                var op = new SceneRawWriteOperation();
#pragma warning restore CS0618 // Type or member is obsolete
                writeData.WriteOperations.Add(op);
                op.ReferenceMap = new BuildReferenceMap();
                op.Command = new WriteCommand();
                op.Command.fileName = pair.Key.ToString();
                op.Command.internalName = pair.Key.ToString();
                op.Command.serializeObjects = new List<SerializationInfo>();


                foreach (var d in pair.Value.m_ReferencedObjects)
                {
                    if (d.m_GUID != builtInResourcesGUID)
                    {
                        var depCluster = clusterResult.ObjectToCluster[d].ToString();
                        op.ReferenceMap.AddMapping(depCluster, packingMethod.SerializationIndexFromObjectIdentifier(d), d);
                    }
                }

                op.UsageSet = usageSet;
                op.Scene = pair.Value.scene;

                writeData.FileToBundle.Add(pair.Key.ToString(), pair.Key.ToString());
            }
            return ReturnCode.Success;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ObjectIdBufferData
        {
            public GUID guid;
            public long lfid;
            public FileType fileType;
            public Hash128 pathHash;
        }

        internal static Hash128 ComputeClusterId(List<ObjectIdentifier> objIds)
        {
            //since ObjectIdentifer is not blittable, copy to a struct that is and compute the hash for the entire buffer
            var seqObjectIdBuffer = new ObjectIdBufferData[objIds.Count];
            for (int i = 0; i < seqObjectIdBuffer.Length; i++)
            {
                var pathHash = !string.IsNullOrEmpty(objIds[i].filePath) ? Hash128.Compute(objIds[i].filePath) : default;
                seqObjectIdBuffer[i] = new ObjectIdBufferData { guid = objIds[i].guid, fileType = objIds[i].fileType, lfid = objIds[i].localIdentifierInFile, pathHash = pathHash };
            }
            return Hash128.Compute(seqObjectIdBuffer);
        }

        private static void ExtractAssets(Dictionary<ObjectIdentifier, HashSet<GUID>> objectToAssets, GUID asset, IEnumerable<ObjectIdentifier> objectIds)
        {
            foreach (var objectId in objectIds)
            {
                if (objectId.filePath.Equals(CommonStrings.UnityDefaultResourcePath, StringComparison.OrdinalIgnoreCase))
                    continue;
                GetOrAdd(objectToAssets, objectId, out var assets);
                assets.Add(asset);
            }
        }
    }
}
#endif
