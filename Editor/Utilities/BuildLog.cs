using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.Build.Pipeline.Utilities
{
    /// <summary>
    /// Basic implementation of IBuildLogger. Stores events in memory and can dump them to the trace event format.
    /// <see cref="IBuildLogger"/>
    /// </summary>
    [Serializable]
    public class BuildLog : IBuildLogger, ILogTEP, IDeferredBuildLogger
    {
        internal interface ILogStepChild { }

        [Serializable]
        internal class ExternalLog : ILogStepChild
        {
            public string Path { get; set; }
            // Wall-clock time (ms) when ImportExternalTEP was called — used as the END anchor
            // for remapping external absolute timestamps into our relative timeline.
            public double ImportTimeMs { get; set; }
        }

        [Serializable]
        internal struct LogEntry
        {
            public int ThreadId { get; set; }
            public double Time { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; }
        }

        [Serializable]
        internal class LogStep : ILogStepChild
        {
            List<ILogStepChild> m_Children;
            List<LogEntry> m_Entries;

            public string Name { get; set; }
            public LogLevel Level { get; set; }
            public List<ILogStepChild> Children { get { if (m_Children == null) m_Children = new List<ILogStepChild>(); return m_Children; } }
            public List<LogEntry> Entries { get { if (m_Entries == null) m_Entries = new List<LogEntry>(); return m_Entries; } }
            public double DurationMS { get; private set; }
            public int ThreadId { get; set; }
            public double StartTime { get; set; }

            public long StartManagedBytes;

            public long EndManagedBytes;

            public long StartNativeBytes;

            public long EndNativeBytes;

            public bool HasMemory { get; set; }

            internal bool isThreaded;

            List<KeyValuePair<string, string>> m_Args;
            public List<KeyValuePair<string, string>> Args { get { if (m_Args == null) m_Args = new List<KeyValuePair<string, string>>(); return m_Args; } }
            public bool HasArgs { get { return m_Args != null && m_Args.Count > 0; } }

            public bool HasChildren { get { return m_Children != null && m_Children.Count > 0; } }
            public bool HasEntries { get { return Entries != null && Entries.Count > 0; } }

            internal void Complete(double time)
            {
                DurationMS = time - StartTime;
            }
        }

        LogStep m_Root;
        [NonSerialized]
        Stack<LogStep> m_Stack;
        [NonSerialized]
        ThreadLocal<BuildLog> m_ThreadedLogs;
        [NonSerialized]
        Stopwatch m_WallTimer;

        bool m_ShouldOverrideWallTimer;
        double m_WallTimerOverride;

        const string k_TsSearchKey = "\"ts\":";
        static readonly string[] s_TsPrefixes = { "\"ts\": ", "\"ts\":" };
        static readonly int s_ProcessId = GetCurrentProcessId();
        static int s_MainThreadId = -1;

        static int GetCurrentProcessId()
        {
            using (var p = Process.GetCurrentProcess())
                return p.Id;
        }

        double GetWallTime()
        {
            return m_ShouldOverrideWallTimer ? m_WallTimerOverride : m_WallTimer.Elapsed.TotalMilliseconds;
        }

        // Cheapest available memory reads — no forced GC, no syscall.
        static void CaptureMemory(int threadId, out long managed, out long native)
        {
            managed = GC.GetTotalMemory(false);
            native = threadId == s_MainThreadId
                ? UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()
                : 0;
        }

        void Init(bool onThread)
        {
            m_WallTimer = Stopwatch.StartNew();
            m_Root = new LogStep();
            m_Stack = new Stack<LogStep>();
            m_Stack.Push(m_Root);

            AddMetaData("Date", DateTime.Now.ToString());

            if (!onThread)
            {
                AddMetaData("UnityVersion", UnityEngine.Application.unityVersion);
                PackageManager.PackageInfo info = PackageManager.PackageInfo.FindForAssembly(typeof(BuildLog).Assembly);
                if (info != null)
                    AddMetaData(info.name, info.version);
            }
        }

        /// <summary>
        /// Creates a new build log object.
        /// </summary>
        public BuildLog()
        {
            s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
            Init(false);
        }

        internal BuildLog(bool onThread)
        {
            Init(onThread);
        }

        private BuildLog GetThreadSafeLog()
        {
            if (m_ThreadedLogs != null)
            {
                if (!m_ThreadedLogs.IsValueCreated)
                    m_ThreadedLogs.Value = new BuildLog(true);
                return m_ThreadedLogs.Value;
            }
            return this;
        }

        /// <inheritdoc />
        public void BeginBuildStep(LogLevel level, string stepName, bool multiThreaded)
        {
            BuildLog log = GetThreadSafeLog();
            BeginBuildStepInternal(log, level, stepName, multiThreaded);
        }

        private static void BeginBuildStepInternal(BuildLog log, LogLevel level, string stepName, bool multiThreaded)
        {
            LogStep node = new LogStep();
            node.Level = level;
            node.Name = stepName;
            node.StartTime = log.GetWallTime();
            node.ThreadId = Thread.CurrentThread.ManagedThreadId;
            if (ScriptableBuildPipeline.useDetailedBuildLog)
            {
                CaptureMemory(node.ThreadId, out node.StartManagedBytes, out node.StartNativeBytes);
                node.HasMemory = true;
            }
            log.m_Stack.Peek().Children.Add(node);
            log.m_Stack.Push(node);
            if (multiThreaded)
            {
                Debug.Assert(log.m_ThreadedLogs == null);
                log.m_ThreadedLogs = new ThreadLocal<BuildLog>(true);
                log.m_ThreadedLogs.Value = log;
                node.isThreaded = true;
            }
        }

        /// <inheritdoc />
        public void EndBuildStep()
        {
            EndBuildStepInternal(GetThreadSafeLog());
        }

        private static void OffsetTimesR(LogStep step, double offset)
        {
            step.StartTime += offset;
            if (step.HasEntries)
            {
                for (int i = 0; i < step.Entries.Count; i++)
                {
                    LogEntry e = step.Entries[i];
                    e.Time = e.Time + offset;
                    step.Entries[i] = e;
                }
            }
            if (step.HasChildren)
                foreach (var child in step.Children)
                {
                    if (child is LogStep subStep)
                        OffsetTimesR(subStep, offset);
                    else if (child is ExternalLog extLog)
                        extLog.ImportTimeMs += offset;
                }
        }

        private static void EndBuildStepInternal(BuildLog log)
        {
            Debug.Assert(log.m_Stack.Count > 1);
            LogStep node = log.m_Stack.Pop();
            node.Complete(log.GetWallTime());
            if (node.HasMemory)
                CaptureMemory(node.ThreadId, out node.EndManagedBytes, out node.EndNativeBytes);

            if (node.isThreaded)
            {
                foreach (BuildLog subLog in log.m_ThreadedLogs.Values)
                {
                    if (subLog != log)
                    {
                        OffsetTimesR(subLog.Root, node.StartTime);
                        if (subLog.Root.HasChildren)
                            node.Children.AddRange(subLog.Root.Children);

                        if (subLog.Root.HasEntries)
                            node.Entries.AddRange(subLog.Root.Entries);
                    }
                }
                log.m_ThreadedLogs.Dispose();
                log.m_ThreadedLogs = null;
            }
        }

        internal LogStep Root { get { return m_Root; } }

        /// <inheritdoc />
        public void AddEntry(LogLevel level, string msg)
        {
            BuildLog log = GetThreadSafeLog();
            log.m_Stack.Peek().Entries.Add(new LogEntry() { Level = level, Message = msg, Time = log.GetWallTime(), ThreadId = Thread.CurrentThread.ManagedThreadId });
        }

        /// <inheritdoc />
        public void AddArg(string key, string value)
        {
            BuildLog log = GetThreadSafeLog();
            log.m_Stack.Peek().Args.Add(new KeyValuePair<string, string>(key, value ?? string.Empty));
        }

        /// <summary>
        /// Internal use only.
        /// <see cref="IBuildLogger"/>
        /// </summary>
        /// <param name="events">Event collection to handle</param>
        void IDeferredBuildLogger.HandleDeferredEventStream(IEnumerable<DeferredEvent> events)
        {
            HandleDeferredEventStreamInternal(events);
        }

        internal void HandleDeferredEventStreamInternal(IEnumerable<DeferredEvent> events)
        {
            // now make all those times relative to the active event
            LogStep startStep = m_Stack.Peek();

            m_ShouldOverrideWallTimer = true;
            foreach (DeferredEvent e in events)
            {
                m_WallTimerOverride = e.Time + startStep.StartTime;
                if (e.Type == DeferredEventType.Begin)
                {
                    BeginBuildStep(e.Level, e.Name, false);
                    if (!string.IsNullOrEmpty(e.Context))
                        AddEntry(e.Level, e.Context);
                }
                else if (e.Type == DeferredEventType.End)
                    EndBuildStep();
                else
                    AddEntry(e.Level, e.Name);
            }
            m_ShouldOverrideWallTimer = false;

            LogStep stopStep = m_Stack.Peek();
            if (stopStep != startStep)
                throw new Exception("Deferred events did not line up as expected");
        }

        static void AppendLineIndented(StringBuilder builder, int indentCount, string text)
        {
            for (int i = 0; i < indentCount; i++)
                builder.Append(" ");
            builder.AppendLine(text);
        }

        static void PrintNodeR(bool includeSelf, StringBuilder builder, int indentCount, BuildLog.LogStep node)
        {
            if (includeSelf)
                AppendLineIndented(builder, indentCount, $"[{node.Name}] {node.DurationMS * 1000}us");
            foreach (var msg in node.Entries)
            {
                string line = (msg.Level == LogLevel.Warning || msg.Level == LogLevel.Error) ? $"{msg.Level}: {msg.Message}" : msg.Message;
                AppendLineIndented(builder, indentCount + 1, line);
            }
            foreach (var child in node.Children)
                if (child is LogStep ls)
                    PrintNodeR(true, builder, indentCount + 1, ls);
        }

        internal string FormatAsText()
        {
            using (new CultureScope())
            {
                StringBuilder builder = new StringBuilder();
                PrintNodeR(false, builder, -1, Root);
                return builder.ToString();
            }
        }

        static string CleanJSONText(string message)
        {
            return message.Replace("\\", "\\\\");
        }

        static IEnumerable<string> IterateTEPLines(bool includeSelf, BuildLog.LogStep node)
        {
            ulong us = (ulong)(node.StartTime * 1000);

            string argText = string.Empty;
            if (node.HasArgs || node.Entries.Count > 0)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(", \"args\": {");
                bool firstArg = true;
                if (node.HasArgs)
                {
                    foreach (var kv in node.Args)
                    {
                        if (!firstArg) builder.Append(", ");
                        builder.Append($"\"{CleanJSONText(kv.Key)}\":\"{CleanJSONText(kv.Value)}\"");
                        firstArg = false;
                    }
                }
                for (int i = 0; i < node.Entries.Count; i++)
                {
                    if (!firstArg) builder.Append(", ");
                    string line = (node.Entries[i].Level == LogLevel.Warning || node.Entries[i].Level == LogLevel.Error) ? $"{node.Entries[i].Level}: {node.Entries[i].Message}" : node.Entries[i].Message;
                    builder.Append($"\"{i}\":\"{CleanJSONText(line)}\"");
                    firstArg = false;
                }
                builder.Append("}");
                argText = builder.ToString();
            }

            if (includeSelf)
                yield return "{" + $"\"name\": \"{CleanJSONText(node.Name)}\", \"ph\": \"X\", \"dur\": {node.DurationMS * 1000}, \"tid\": {node.ThreadId}, \"ts\": {us}, \"pid\": {s_ProcessId}" + argText + "}";

            foreach (var child in node.Children)
            {
                if (child is LogStep ls)
                    foreach (var r in IterateTEPLines(true, ls))
                        yield return r;
                else if (child is ExternalLog ext)
                    foreach (var line in ReadExternalTEPLines(ext.Path, ext.ImportTimeMs))
                        yield return line;
            }
        }

        struct ParsedTEP
        {
            public List<string> EventLines;
            public long MinTs;
            public long MaxTs;
        }

        static ParsedTEP ParseExternalTEPFile(string path)
        {
            var result = new ParsedTEP
            {
                EventLines = new List<string>(),
                MinTs = long.MaxValue,
                MaxTs = long.MinValue
            };
            using (var reader = new StreamReader(path))
            {
                string rawLine;
                bool inside = false;
                while ((rawLine = reader.ReadLine()) != null)
                {
                    string line = rawLine.Trim();
                    if (!inside)
                    {
                        if (line.StartsWith("\"traceEvents\"", StringComparison.Ordinal))
                            inside = true;
                        continue;
                    }
                    if (line == "]" || line == "],")
                        break;
                    if (line.StartsWith(",", StringComparison.Ordinal))
                        line = line.Substring(1).TrimStart();
                    if (line.EndsWith(",", StringComparison.Ordinal))
                        line = line.Substring(0, line.Length - 1).TrimEnd();
                    if (string.IsNullOrEmpty(line) || line == "[")
                        continue;
                    if (TryExtractLong(line, k_TsSearchKey, out long ts))
                    {
                        result.MinTs = Math.Min(result.MinTs, ts);
                        result.MaxTs = Math.Max(result.MaxTs, ts);
                    }
                    result.EventLines.Add(line);
                }
            }
            return result;
        }

        // Buffer all event lines so we can find min/max ts before yielding.
        // External files use absolute system timestamps. We anchor the END of the
        // external events at importTimeMs (the wall-clock moment ImportExternalTEP
        // was called, just after the external build finished), so the events land
        // in the correct position on our relative timeline.
        static IEnumerable<string> ReadExternalTEPLines(string path, double importTimeMs)
        {
            ParsedTEP parsed;
            try { parsed = ParseExternalTEPFile(path); }
            catch { yield break; }

            if (parsed.EventLines.Count == 0)
                yield break;

            long baseTs = parsed.MinTs == long.MaxValue ? 0 : parsed.MinTs;
            long durationUs = (parsed.MaxTs == long.MinValue || parsed.MaxTs <= parsed.MinTs) ? 0 : parsed.MaxTs - parsed.MinTs;
            // offsetUs = where the first event should land; end is at importTimeMs.
            long offsetUs = (long)(importTimeMs * 1000) - durationUs;
            foreach (string line in parsed.EventLines)
                yield return RemapTsField(line, baseTs, offsetUs);
        }

        static bool TryExtractLong(string line, string formattedKey, out long value)
        {
            value = 0;
            int idx = line.IndexOf(formattedKey, StringComparison.Ordinal);
            if (idx < 0) return false;
            idx += formattedKey.Length;
            while (idx < line.Length && line[idx] == ' ') idx++;
            int start = idx;
            while (idx < line.Length && (char.IsDigit(line[idx]) || line[idx] == '-')) idx++;
            return idx > start && long.TryParse(line.Substring(start, idx - start), out value);
        }

        static string RemapTsField(string line, long baseTs, long offsetUs)
        {
            if (!TryExtractLong(line, k_TsSearchKey, out long ts))
                return line;
            long newTs = (ts - baseTs) + offsetUs;
            foreach (string prefix in s_TsPrefixes)
            {
                string search = prefix + ts.ToString(CultureInfo.InvariantCulture);
                int idx = line.IndexOf(search, StringComparison.Ordinal);
                if (idx >= 0)
                    return line.Substring(0, idx + prefix.Length) +
                           newTs.ToString(CultureInfo.InvariantCulture) +
                           line.Substring(idx + search.Length);
            }
            return line;
        }

        class CultureScope : IDisposable
        {
            CultureInfo m_Prev;
            public CultureScope()
            {
                m_Prev = Thread.CurrentThread.CurrentCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            }

            public void Dispose()
            {
                Thread.CurrentThread.CurrentCulture = m_Prev;
            }
        }

        private List<Tuple<string, string>> m_MetaData = new List<Tuple<string, string>>();

        /// <summary>
        /// Adds a key value pair to the MetaData list. This can be used to store things like package version numbers.
        /// </summary>
        /// <param name="key">The key for the MetaData.</param>
        /// <param name="value">The value of the MetaData.</param>
        public void AddMetaData(string key, string value)
        {
            m_MetaData.Add(new Tuple<string, string>(key, value));
        }

        /// <inheritdoc />
        public void ImportExternalTEP(string tepFilePath)
        {
            BuildLog log = GetThreadSafeLog();
            log.m_Stack.Peek().Children.Add(new ExternalLog { Path = tepFilePath, ImportTimeMs = log.GetWallTime() });
        }

        /// <summary>
        /// Converts the captured build log events into the text Trace Event Profiler format
        /// </summary>
        /// <returns>Profile data.</returns>
        public string FormatForTraceEventProfiler()
        {
            using (new CultureScope())
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("{");

                foreach (Tuple<string, string> tuple in m_MetaData)
                    builder.AppendLine($"\"{tuple.Item1}\": \"{tuple.Item2}\",");

                builder.AppendLine("\"traceEvents\": [");
                int i = 0;
                foreach (string line in IterateTEPLines(false, Root))
                {
                    if (i != 0)
                        builder.Append(",");
                    builder.AppendLine(line);
                    i++;
                }
                builder.AppendLine("]");
                builder.AppendLine("}");
                return builder.ToString();
            }
        }
    }
}
