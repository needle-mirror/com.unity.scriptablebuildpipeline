using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities.USerialize;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Build.Pipeline;
using System.Collections.Concurrent;

namespace UnityEditor.Build.Pipeline.Utilities
{
    /// <summary>
    /// Default implementation of the Build Cache
    /// </summary>
    public class BuildCache : IBuildCache, IDisposable
    {
        // Custom serialization handler for 'BuildUsageTagSet' type as it cannot be correctly serialized purely with reflection
        internal class USerializeCustom_BuildUsageTagSet : ICustomSerializer
        {
            // Return the type that this custom serializer deals with
            Type ICustomSerializer.GetType()
            {
                return typeof(BuildUsageTagSet);
            }

            void ICustomSerializer.USerializer(Serializer serializer, object value)
            {
                BuildUsageTagSet buildUsageTagSet = (BuildUsageTagSet)value;
                if (serializer.WriteNullFlag(buildUsageTagSet))
                    serializer.WriteBytes(buildUsageTagSet.SerializeToBinary());
            }

            object ICustomSerializer.UDeSerializer(DeSerializer deserializer)
            {
                BuildUsageTagSet tagSet = null;
                if (deserializer.ReadNullFlag())
                {
                    tagSet = new BuildUsageTagSet();
                    byte[] bytes = deserializer.ReadBytes();
                    tagSet.DeserializeFromBinary(bytes);
                }
                return tagSet;
            }
        }

        // Object factories used to create instances of types involved in build cache serialization more quickly than the generic Activator.CreateInstance()
        internal static (Type, DeSerializer.ObjectFactory)[] ObjectFactories = new (Type, DeSerializer.ObjectFactory)[]
        {
            (typeof(AssetLoadInfo), () => { return new AssetLoadInfo(); }),
            (typeof(BuildUsageTagGlobal), () => { return new BuildUsageTagGlobal(); }),
            (typeof(BundleDetails), () => { return new BundleDetails(); }),
            (typeof(CachedInfo), () => { return new CachedInfo(); }),
            (typeof(CacheEntry), () => { return new CacheEntry(); }),
            (typeof(ExtendedAssetData), () => { return new ExtendedAssetData(); }),
            (typeof(ObjectIdentifier), () => { return new ObjectIdentifier(); }),
            (typeof(ObjectSerializedInfo), () => { return new ObjectSerializedInfo(); }),
            (typeof(ResourceFile), () => { return new ResourceFile(); }),
            (typeof(SceneDependencyInfo), () => { return new SceneDependencyInfo(); }),
            (typeof(SerializedFileMetaData), () => { return new SerializedFileMetaData(); }),
            (typeof(SerializedLocation), () => { return new SerializedLocation(); }),
            (typeof(SpriteImporterData), () => { return new SpriteImporterData(); }),
            (typeof(WriteResult), () => { return new WriteResult(); }),
            (typeof(KeyValuePair<ObjectIdentifier, Type[]>), () => { return new KeyValuePair<ObjectIdentifier, Type[]>(); }),
            (typeof(List<KeyValuePair<ObjectIdentifier, Type[]>>), () => { return new List<KeyValuePair<ObjectIdentifier, Type[]>>(); }),
            (typeof(Hash128), () => { return new Hash128(); }),
        };

        // Custom serializers we use for build cache serialization of types that cannot be correctly serialized using the built in reflection based serialization code
        internal static ICustomSerializer[] CustomSerializers = new ICustomSerializer[]
        {
            new USerializeCustom_BuildUsageTagSet()
        };
        const string k_CachePath = "Library/BuildCache";
        const int k_Version = 5;
        internal const int k_CacheServerVersion = 2;
        internal const long k_BytesToGigaBytes = 1073741824L;

        [NonSerialized]
        IBuildLogger m_Logger;

        [NonSerialized]
        Hash128 m_GlobalHash;

        /// <summary>
        /// Creates a new build cache object.
        /// </summary>
        public BuildCache()
        {
            m_GlobalHash = CalculateGlobalArtifactVersionHash();
        }

        /// <summary>
        /// Creates a new remote build cache object.
        /// </summary>
        /// <param name="logger">The build logger to use for cache activity logging.</param>
        public BuildCache(IBuildLogger logger)
        {
            m_Logger = logger;
            using (logger.ScopedStep(LogLevel.Info, "Initializing BuildCache"))
            {
                m_GlobalHash = CalculateGlobalArtifactVersionHash();
            }
        }

        static Hash128 CalculateGlobalArtifactVersionHash()
        {
            return HashingMethods.Calculate(Application.unityVersion, k_Version).ToHash128();
        }

        internal void ClearCacheEntryMaps()
        {
            BuildCacheUtility.ClearCacheHashes();
        }

        /// <summary>
        /// Disposes the build cache instance.
        /// </summary>
        public void Dispose() { }

        /// <inheritdoc />
        public CacheEntry GetCacheEntry(GUID asset, int version = 1)
        {
            return BuildCacheUtility.GetCacheEntry(asset, version);
        }

        /// <inheritdoc />
        public CacheEntry GetCacheEntry(string path, int version = 1)
        {
            return BuildCacheUtility.GetCacheEntry(path, version);
        }

        /// <inheritdoc />
        public CacheEntry GetCacheEntry(ObjectIdentifier objectID, int version = 1)
        {
            if (objectID.guid.Empty())
                return GetCacheEntry(objectID.filePath, version);
            return GetCacheEntry(objectID.guid, version);
        }

        /// <inheritdoc />
        public CacheEntry GetCacheEntry(Type type, int version = 1)
        {
            return BuildCacheUtility.GetCacheEntry(type, version);
        }

        internal CacheEntry GetUpdatedCacheEntry(CacheEntry entry)
        {
            if (entry.Type == CacheEntry.EntryType.File)
                return GetCacheEntry(entry.File, entry.Version);
            if (entry.Type == CacheEntry.EntryType.Asset)
                return GetCacheEntry(entry.Guid, entry.Version);
            if (entry.Type == CacheEntry.EntryType.ScriptType)
                return GetCacheEntry(Type.GetType(entry.ScriptType), entry.Version);
            return entry;
        }

        internal bool LogCacheMiss(string msg)
        {
            if (!ScriptableBuildPipeline.logCacheMiss)
                return false;
            m_Logger.AddEntrySafe(LogLevel.Warning, msg);
            UnityEngine.Debug.LogWarning(msg);
            return true;
        }

        /// <inheritdoc />
        public bool HasAssetOrDependencyChanged(CachedInfo info)
        {
            if (info == null || !info.Asset.IsValid())
                return true;

            var result = false;
            var updatedEntry = GetUpdatedCacheEntry(info.Asset);
            if (info.Asset != updatedEntry)
            {
                if (!LogCacheMiss($"[Cache Miss]: Source asset changed. Old: {info.Asset} New: {updatedEntry}"))
                    return true;
                result = true;
            }

            foreach (var dependency in info.Dependencies)
            {
                if (!dependency.IsValid())
                {
                    if (!LogCacheMiss($"[Cache Miss]: Dependency is no longer valid. Asset: {info.Asset} Dependency: {dependency}"))
                        return true;
                    result = true;
                }

                updatedEntry = GetUpdatedCacheEntry(dependency);
#if VALIDATE_CACHED_DEPENDENCIES
                     if (!updatedEntry.IsValid())
                         return result;
#endif

                if (dependency != GetUpdatedCacheEntry(updatedEntry))
                {
                    if (!LogCacheMiss($"[Cache Miss]: Dependency changed. Asset: {info.Asset} Old: {dependency} New: {updatedEntry}"))
                        return true;
                    result = true;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public string GetCachedInfoFile(CacheEntry entry)
        {
            var guid = entry.Guid.ToString();
            string finalHash = HashingMethods.Calculate(m_GlobalHash, entry.Hash).ToString();
            return string.Format("{0}/{1}/{2}/{3}/{2}.info", k_CachePath, guid.Substring(0, 2), guid, finalHash);
        }

        /// <inheritdoc />
        public string GetCachedArtifactsDirectory(CacheEntry entry)
        {
            var guid = entry.Guid.ToString();
            string finalHash = HashingMethods.Calculate(m_GlobalHash, entry.Hash).ToString();
            return string.Format("{0}/{1}/{2}/{3}", k_CachePath, guid.Substring(0, 2), guid, finalHash);
        }

        /// <inheritdoc />
        public void LoadCachedData(IList<CacheEntry> entries, out IList<CachedInfo> cachedInfos)
        {
            if (entries == null)
            {
                cachedInfos = null;
                return;
            }

            if (entries.Count == 0)
            {
                cachedInfos = new List<CachedInfo>();
                return;
            }

            int cachedCount = 0;
            using (m_Logger.ScopedStep(LogLevel.Info, "LoadCachedData"))
            {
                m_Logger.AddEntrySafe(LogLevel.Info, $"{entries.Count} items");

                Stopwatch deserializeTimer = null;
                using (m_Logger.ScopedStep(LogLevel.Info, "Read and deserialize cache info"))
                {
                    CachedInfo[] cachedInfoArray = new CachedInfo[entries.Count];
                    deserializeTimer = Stopwatch.StartNew();
                    int workerThreadCount = Math.Min(Environment.ProcessorCount, 4);    // Testing of the USerialize code has shown increasing concurrency beyond four threads produces worse performance (the suspicion is due to GC contention but that's TBC)
                    ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = workerThreadCount };
                    ConcurrentStack<DeSerializer> deserializers = new ConcurrentStack<DeSerializer>();
                    for (int serializerNum = 0; serializerNum < workerThreadCount; serializerNum++)
                        deserializers.Push(new DeSerializer(CustomSerializers, ObjectFactories));
                    Parallel.For(0, entries.Count, parallelOptions, index =>
                    {
                        string file = GetCachedInfoFile(entries[index]);
                        if (!File.Exists(file))
                        {
                            LogCacheMiss($"[Cache Miss]: Missing cache entry. Entry: {entries[index]}");
                            return;
                        }
                        byte[] rented = null;
                        try
                        {
                            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                int length = (int)fs.Length;
                                rented = ArrayPool<byte>.Shared.Rent(length);
                                int bytesRead = 0;
                                while (bytesRead < length)
                                {
                                    int read = fs.Read(rented, bytesRead, length - bytesRead);
                                    if (read == 0) break;
                                    bytesRead += read;
                                }
                                if (length > 0)
                                {
                                    using (MemoryStream memoryStream = new MemoryStream(rented, 0, length, false))
                                    {
                                        deserializers.TryPop(out DeSerializer deserializer);
                                        cachedInfoArray[index] = deserializer.DeSerialize<CachedInfo>(memoryStream);
                                        deserializers.Push(deserializer);
                                        Interlocked.Increment(ref cachedCount);
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            LogCacheMiss($"[Cache Miss]: Invalid cache entry. Entry: {entries[index]}");
                        }
                        finally
                        {
                            if (rented != null)
                                ArrayPool<byte>.Shared.Return(rented);
                        }
                    });
                    deserializeTimer.Stop();
                    cachedInfos = cachedInfoArray;
                }

                m_Logger.AddEntrySafe(LogLevel.Info, $"Time spent deserializing: {deserializeTimer.ElapsedMilliseconds}ms");
                m_Logger.AddEntrySafe(LogLevel.Info, $"Local Cache hit count: {cachedCount}");
            }

            using (m_Logger.ScopedStep(LogLevel.Info, "Check for changed dependencies"))
            {
                int unchangedCount = 0;
                for (int i = 0; i < cachedInfos.Count; i++)
                {
                    if (HasAssetOrDependencyChanged(cachedInfos[i]))
                        cachedInfos[i] = null;
                    else
                        unchangedCount++;
                }
                m_Logger.AddEntrySafe(LogLevel.Info, $"Unchanged dependencies count: {unchangedCount}");
            }

            Assert.AreEqual(entries.Count, cachedInfos.Count);
        }

        class SaveCachedDataTaskData
        {
            public Semaphore m_ReadyLock = new Semaphore(0, 1);
            public Semaphore m_DoneLock = new Semaphore(0, 1);
        }

        static void SaveCachedDataTask(object data)
        {
            SaveCachedDataTaskData saveTaskData = (SaveCachedDataTaskData)data;

            // Tell the SaveCachedData() function that our task has started, ThreadingManager ensures this can only happen after any queued prune tasks have completed
            saveTaskData.m_ReadyLock.Release();

            // Now wait until SaveCachedData() has finished serialising and writing files.  The presence of this task in the Save task queue will prevent any new prune tasks being queued
            saveTaskData.m_DoneLock.WaitOne();
        }

        /// <inheritdoc />
        public void SaveCachedData(IList<CachedInfo> infos)
        {
            if (infos == null || infos.Count == 0)
                return;

            using (m_Logger.ScopedStep(LogLevel.Info, "SaveCachedData"))
            {
                m_Logger.AddEntrySafe(LogLevel.Info, $"Saving {infos.Count} infos");

                // Queue the Save task and wait until it actually starts executing.  This ensures any queued prune tasks finish before we start writing to the cache folder
                SaveCachedDataTaskData taskData = new SaveCachedDataTaskData();
                ThreadingManager.QueueTask(ThreadingManager.ThreadQueues.SaveQueue, SaveCachedDataTask, taskData);
                taskData.m_ReadyLock.WaitOne();

                using (m_Logger.ScopedStep(LogLevel.Info, "SerializingCacheInfos[" + infos.Count + "]"))
                {
                    int workerThreadCount = Math.Min(Environment.ProcessorCount, 4);    // Testing of the USerialize code has shown increasing concurrency beyond four threads produces worse performance (the suspicion is due to GC contention but that's TBC)
                    ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = workerThreadCount };
                    ConcurrentStack<(Serializer serializer, MemoryStream stream)> serializers = new ConcurrentStack<(Serializer, MemoryStream)>();
                    for (int serializerNum = 0; serializerNum < workerThreadCount; serializerNum++)
                        serializers.Push((new Serializer(CustomSerializers), new MemoryStream()));
                    Parallel.For(0, infos.Count, parallelOptions, index =>
                    {
                        serializers.TryPop(out var item);
                        try
                        {
                            item.stream.SetLength(0);
                            item.serializer.Serialize(item.stream, infos[index], 1);

                            if (item.stream.Length > 0)
                            {
                                string cachedInfoFilepath = GetCachedInfoFile(infos[index].Asset);
                                Directory.CreateDirectory(Path.GetDirectoryName(cachedInfoFilepath));

                                using (FileStream fileStream = new FileStream(cachedInfoFilepath, FileMode.Create))
                                {
                                    fileStream.Write(item.stream.GetBuffer(), 0, (int)item.stream.Length);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            BuildLogger.LogException(e);
                        }
                        finally
                        {
                            serializers.Push(item);
                        }
                    });
                }

                // Signal the Save task's SaveCachedDataTask() function that we are done so it can exit.  This will allow prune tasks to proceed once again
                taskData.m_DoneLock.Release();
            }
        }

        internal void SyncPendingSaves()
        {
            using (m_Logger.ScopedStep(LogLevel.Info, "SyncPendingSaves"))
                ThreadingManager.WaitForOutstandingTasks();
        }

        internal struct CacheFolder
        {
            public DirectoryInfo directory;
            public long Length { get; set; }
            public void Delete() => directory.Delete(true);
            public DateTime LastAccessTimeUtc
            {
                get => directory.LastAccessTimeUtc;
                internal set => directory.LastAccessTimeUtc = value;
            }
        }

        /// <summary>
        /// Deletes the build cache directory.
        /// </summary>
        /// <param name="prompt">The message to display in the popup window.</param>
        public static void PurgeCache(bool prompt)
        {
            ThreadingManager.WaitForOutstandingTasks();
            BuildCacheUtility.ClearCacheHashes();
            if (!Directory.Exists(k_CachePath))
            {
                if (prompt)
                    UnityEngine.Debug.Log("Current build cache is empty.");
                return;
            }

            if (prompt)
            {
                if (!EditorUtility.DisplayDialog("Purge Build Cache", "Do you really want to purge your entire build cache?", "Yes", "No"))
                    return;

                EditorUtility.DisplayProgressBar(ScriptableBuildPipeline.Properties.purgeCache.text, ScriptableBuildPipeline.Properties.pleaseWait.text, 0.0F);
                Directory.Delete(k_CachePath, true);
                EditorUtility.ClearProgressBar();
            }
            else
                Directory.Delete(k_CachePath, true);
        }

        /// <summary>
        /// Prunes the build cache so that its size is within the maximum cache size.
        /// </summary>
        public static void PruneCache()
        {
            ThreadingManager.WaitForOutstandingTasks();
            int maximumSize = ScriptableBuildPipeline.maximumCacheSize;
            long maximumCacheSize = maximumSize * k_BytesToGigaBytes;

            // Get sizes based on common directory root for a guid / hash
            ComputeCacheSizeAndFolders(out long currentCacheSize, out List<CacheFolder> cacheFolders);

            if (currentCacheSize < maximumCacheSize)
            {
                UnityEngine.Debug.LogFormat("Current build cache currentCacheSize {0}, prune threshold {1} GB. No prune performed. You can change this value in the \"Edit/Preferences...\" window.", EditorUtility.FormatBytes(currentCacheSize), maximumSize);
                return;
            }

            if (!EditorUtility.DisplayDialog("Prune Build Cache", string.Format("Current build cache currentCacheSize is {0}, which is over the prune threshold of {1}. Do you want to prune your build cache now?", EditorUtility.FormatBytes(currentCacheSize), EditorUtility.FormatBytes(maximumCacheSize)), "Yes", "No"))
                return;

            EditorUtility.DisplayProgressBar(ScriptableBuildPipeline.Properties.pruneCache.text, ScriptableBuildPipeline.Properties.pleaseWait.text, 0.0F);

            PruneCacheFolders(maximumCacheSize, currentCacheSize, cacheFolders);

            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Prunes the build cache without showing UI prompts.
        /// </summary>
        /// <param name="maximumCacheSize">The maximum cache size.</param>
        public static void PruneCache_Background(long maximumCacheSize)
        {
            ThreadingManager.QueueTask(ThreadingManager.ThreadQueues.PruneQueue, PruneCache_Background_Internal, maximumCacheSize);
        }

        internal static void PruneCache_Background_Internal(object maximumCacheSize)
        {
            long maxCacheSize = (long)maximumCacheSize;
            // Get sizes based on common directory root for a guid / hash
            ComputeCacheSizeAndFolders(out long currentCacheSize, out List<CacheFolder> cacheFolders);
            if (currentCacheSize < maxCacheSize)
                return;

            PruneCacheFolders(maxCacheSize, currentCacheSize, cacheFolders);
        }

        internal static void ComputeCacheSizeAndFolders(out long currentCacheSize, out List<CacheFolder> cacheFolders)
        {
            currentCacheSize = 0;
            cacheFolders = new List<CacheFolder>();

            var directory = new DirectoryInfo(k_CachePath);
            if (!directory.Exists)
                return;

            int length = directory.FullName.Count(x => x == Path.DirectorySeparatorChar) + 3;
            DirectoryInfo[] subDirectories = directory.GetDirectories("*", SearchOption.AllDirectories);
            foreach (var subDirectory in subDirectories)
            {
                if (subDirectory.FullName.Count(x => x == Path.DirectorySeparatorChar) != length)
                    continue;

                FileInfo[] files = subDirectory.GetFiles("*", SearchOption.AllDirectories);
                var cacheFolder = new CacheFolder { directory = subDirectory, Length = files.Sum(x => x.Length) };
                cacheFolders.Add(cacheFolder);

                currentCacheSize += cacheFolder.Length;
            }
        }

        internal static void PruneCacheFolders(long maximumCacheSize, long currentCacheSize, List<CacheFolder> cacheFolders)
        {
            cacheFolders.Sort((a, b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc));
            // Need to delete sets of files as the .info might reference a specific file artifact
            foreach (var cacheFolder in cacheFolders)
            {
                currentCacheSize -= cacheFolder.Length;
                cacheFolder.Delete();
                if (currentCacheSize < maximumCacheSize)
                    break;
            }
        }

        // TODO: Add to IBuildCache interface when IBuildLogger becomes public
        internal void SetBuildLogger(IBuildLogger profiler)
        {
            if(m_Logger == null)
                m_Logger = profiler;
        }
    }
}
