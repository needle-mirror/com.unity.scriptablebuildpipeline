using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;


namespace UnityEditor.Build.Pipeline.Utilities
{
    internal static class TaskCachingUtility
    {
        public class WorkItem<T>
        {
            public T Context;
            public int Index;
            public CacheEntry entry;
            public string StatusText;
            public WorkItem(T context, string statusText = "")
            {
                this.Context = context;
                this.StatusText = statusText;
            }
        }

        public interface IRunCachedCallbacks<T>
        {
            CacheEntry CreateCacheEntry(WorkItem<T> item);
            void ProcessUncached(WorkItem<T> item);
            void ProcessCached(WorkItem<T> item, CachedInfo info);
            void PostProcess(WorkItem<T> item);
            CachedInfo CreateCachedInfo(WorkItem<T> item);
        }

        public static ReturnCode RunCachedOperation<T>(IBuildCache cache, IBuildLogger log, IProgressTracker tracker, List<WorkItem<T>> workItems,
            IRunCachedCallbacks<T> cbs
        )
        {
            using (log.ScopedStep(LogLevel.Info, "RunCachedOperation"))
            {
                List<CacheEntry> cacheEntries = null;
                List<WorkItem<T>> nonCachedItems = workItems;
                var cachedItems = new List<WorkItem<T>>();

                for (int i = 0; i < workItems.Count; i++)
                {
                    workItems[i].Index = i;
                }

                IList<CachedInfo> cachedInfo = null;

                if (cache != null)
                {
                    using (log.ScopedStep(LogLevel.Info, "Creating Cache Entries"))
                        for (int i = 0; i < workItems.Count; i++)
                        {
                            workItems[i].entry = cbs.CreateCacheEntry(workItems[i]);
                        }

                    cacheEntries = workItems.Select(i => i.entry).ToList();

                    using (log.ScopedStep(LogLevel.Info, "Load Cached Data"))
                        cache.LoadCachedData(cacheEntries, out cachedInfo);

                    cachedItems = workItems.Where(x => cachedInfo[x.Index] != null).ToList();
                    nonCachedItems = workItems.Where(x => cachedInfo[x.Index] == null).ToList();
                }

                using (log.ScopedStep(LogLevel.Info, "Process Entries"))
                    foreach (WorkItem<T> item in nonCachedItems)
                    {
                        if (!tracker.UpdateInfoUnchecked(item.StatusText))
                            return ReturnCode.Canceled;
                        cbs.ProcessUncached(item);
                    }

                using (log.ScopedStep(LogLevel.Info, "Process Cached Entries"))
                    foreach (WorkItem<T> item in cachedItems)
                        cbs.ProcessCached(item, cachedInfo[item.Index]);

                foreach (WorkItem<T> item in workItems)
                    cbs.PostProcess(item);

                if (cache != null)
                {
                    List<CachedInfo> uncachedInfo;
                    using (log.ScopedStep(LogLevel.Info, "Saving to Cache"))
                    {
                        using (log.ScopedStep(LogLevel.Info, "Creating Cached Infos"))
                            uncachedInfo = nonCachedItems.Select((item) => cbs.CreateCachedInfo(item)).ToList();
                        cache.SaveCachedData(uncachedInfo);
                    }
                }

                log.AddEntrySafe(LogLevel.Info, $"Total Entries: {workItems.Count}, Processed: {nonCachedItems.Count}, Cached: {cachedItems.Count}");
                return ReturnCode.Success;
            }
        }
    }
}
