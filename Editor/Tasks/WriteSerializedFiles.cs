using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Pipeline.WriteTypes;
using UnityEngine;
using static UnityEditor.Build.Pipeline.Utilities.TaskCachingUtility;

namespace UnityEditor.Build.Pipeline.Tasks
{
    internal class WriteResultReflection
    {
        public static void SetSerializedObjects(ref WriteResult result, ObjectSerializedInfo[] osis)
        {
            var fieldInfo = typeof(WriteResult).GetField("m_SerializedObjects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object boxed = result;
            fieldInfo.SetValue(boxed, osis);
            result = (WriteResult)boxed;
        }

        public static void SetResourceFiles(ref WriteResult result, ResourceFile[] files)
        {
            var fieldInfo = typeof(WriteResult).GetField("m_ResourceFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object boxed = result;
            fieldInfo.SetValue(boxed, files);
            result = (WriteResult)boxed;
        }
    }
    public class WriteSerializedFiles : IBuildTask, IRunCachedCallbacks<WriteSerializedFiles.Item>
    {
        /// <inheritdoc />
        public int Version { get { return 3; } }

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IBuildParameters m_Parameters;

        [InjectContext(ContextUsage.In)]
        IDependencyData m_DependencyData;

        [InjectContext(ContextUsage.In)]
        IWriteData m_WriteData;

        [InjectContext]
        IBuildResults m_Results;

        [InjectContext(ContextUsage.In, true)]
        IProgressTracker m_Tracker;

        [InjectContext(ContextUsage.In, true)]
        IBuildCache m_Cache;

        [InjectContext(ContextUsage.In, true)]
        IBuildLogger m_Log;
#pragma warning restore 649

        CacheEntry GetCacheEntry(IWriteOperation operation, BuildSettings settings, BuildUsageTagGlobal globalUsage, bool onlySaveFirstSerializedObject)
        {
            var entry = new CacheEntry();
            entry.Type = CacheEntry.EntryType.Data;
            entry.Guid = HashingMethods.Calculate("WriteSerializedFiles").ToGUID();
            entry.Hash = HashingMethods.Calculate(Version, operation.GetHash128(), settings.GetHash128(), globalUsage, onlySaveFirstSerializedObject).ToHash128();
            entry.Version = Version;
            return entry;
        }

        static void SlimifySerializedObjects(ref WriteResult result)
        {
            var fileOffsets = new List<ObjectSerializedInfo>();
            foreach (ResourceFile serializedFile in result.resourceFiles)
            {
                if (!serializedFile.serializedFile)
                    continue;
                fileOffsets.Add(result.serializedObjects.First(x => x.header.fileName == serializedFile.fileAlias));
            }

            WriteResultReflection.SetSerializedObjects(ref result, fileOffsets.ToArray());
        }

        CachedInfo GetCachedInfo(CacheEntry entry, WriteResult result, SerializedFileMetaData metaData)
        {
            var info = new CachedInfo();
            info.Asset = entry;
            info.Data = new object[] { result, metaData };
            info.Dependencies = new CacheEntry[0];
            return info;
        }

        class Item
        {
            public WriteResult Result;
            public SerializedFileMetaData MetaData;
        }

        BuildSettings m_BuildSettings;
        BuildUsageTagGlobal m_GlobalUsage;
        IBuildCache m_UseCache;

        internal void SetupTaskContext()
        {
            m_GlobalUsage = m_DependencyData.GlobalUsage;
            foreach (var sceneInfo in m_DependencyData.SceneInfo)
                m_GlobalUsage |= sceneInfo.Value.globalUsage;

            m_BuildSettings = m_Parameters.GetContentBuildSettings();
            m_UseCache = m_Parameters.UseCache ? m_Cache : null;
        }

        /// <inheritdoc />
        public ReturnCode Run()
        {
            SetupTaskContext();

            List<WorkItem<Item>> workItems = m_WriteData.WriteOperations.Select(
                i => new WorkItem<Item>(new Item(), i.Command.internalName)
                ).ToList();

            return TaskCachingUtility.RunCachedOperation<Item>(
                m_UseCache,
                m_Log,
                m_Tracker,
                workItems,
                this);
        }

        private SerializedFileMetaData CalculateFileMetadata(ref WriteResult result)
        {
            List<object> contentHashObjects = new List<object>();
            List<object> fullHashObjects = new List<object>();
            foreach (ResourceFile file in result.resourceFiles)
            {
                RawHash fileHash = HashingMethods.CalculateFile(file.fileName);
                RawHash contentHash = fileHash;
                fullHashObjects.Add(fileHash);
                if (file.serializedFile && result.serializedObjects.Count > 0)
                {
                    using (var stream = new FileStream(file.fileName, FileMode.Open, FileAccess.Read))
                    {
                        stream.Position = (long)result.serializedObjects[0].header.offset;
                        contentHash = HashingMethods.CalculateStream(stream);
                    }
                }
                contentHashObjects.Add(contentHash);
            }
            SerializedFileMetaData data = new SerializedFileMetaData();
            data.RawFileHash = HashingMethods.Calculate(fullHashObjects).ToHash128();
            data.ContentHash = HashingMethods.Calculate(contentHashObjects).ToHash128();
            return data;
        }

        CacheEntry IRunCachedCallbacks<Item>.CreateCacheEntry(WorkItem<Item> item)
        {
            return GetCacheEntry(m_WriteData.WriteOperations[item.Index], m_BuildSettings, m_GlobalUsage, ScriptableBuildPipeline.slimWriteResults);
        }

        void IRunCachedCallbacks<Item>.ProcessUncached(WorkItem<Item> item)
        {
            IWriteOperation op = m_WriteData.WriteOperations[item.Index];

            string targetDir = m_UseCache != null ? m_UseCache.GetCachedArtifactsDirectory(item.entry) : m_Parameters.TempOutputFolder;
            Directory.CreateDirectory(targetDir);

            using (m_Log.ScopedStep(LogLevel.Info, $"Writing {op.GetType().Name}", op.Command.fileName))
                item.Context.Result = op.Write(targetDir, m_BuildSettings, m_GlobalUsage);

            item.Context.MetaData = CalculateFileMetadata(ref item.Context.Result);

            if (ScriptableBuildPipeline.slimWriteResults)
                SlimifySerializedObjects(ref item.Context.Result);
        }

        void IRunCachedCallbacks<Item>.ProcessCached(WorkItem<Item> item, CachedInfo info)
        {
            item.Context.Result = (WriteResult)info.Data[0];
            item.Context.MetaData = (SerializedFileMetaData)info.Data[1];
        }

        void IRunCachedCallbacks<Item>.PostProcess(WorkItem<Item> item)
        {
            IWriteOperation op = m_WriteData.WriteOperations[item.Index];
            m_Results.WriteResults.Add(op.Command.internalName, item.Context.Result);
            m_Results.WriteResultsMetaData.Add(op.Command.internalName, item.Context.MetaData);
        }

        CachedInfo IRunCachedCallbacks<Item>.CreateCachedInfo(WorkItem<Item> item)
        {
            return GetCachedInfo(item.entry, item.Context.Result, item.Context.MetaData);
        }
    }
}
