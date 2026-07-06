using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Utilities;

[assembly: InternalsVisibleTo("Unity.Addressables.Editor.Tests")]
namespace UnityEditor.Build.Pipeline.Interfaces
{
    /// <summary>
    /// Describes the level of a log entry
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// The entry is reporting an error.
        /// </summary>
        Error,
        /// <summary>
        /// The entry is reporting an warning.
        /// </summary>
        Warning,
        /// <summary>
        /// The entry is reporting general information.
        /// </summary>
        Info,
        /// <summary>
        /// The entry is reporting verbose information.
        /// </summary>
        Verbose
    }

    /// <summary>
    /// Interface for monitoring the build process. Several tasks will log details of their progress through this interface.
    /// See the [Build Logging](https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@latest/index.html?subfolder=/manual/BuildLogger.html) documentation for more details.
    /// </summary>
    public interface IBuildLogger : IContextObject
    {
        /// <summary>
        /// Adds details to the active build step
        /// </summary>
        /// <param name="level">The log level of this entry.</param>
        /// <param name="msg">The message to add.</param>
        void AddEntry(LogLevel level, string msg);

        /// <summary>
        /// Should be called when beginning a build step.
        /// </summary>
        /// <param name="level">The log level of this step.</param>
        /// <param name="stepName">A name associated with the step. It is recommended that this name does not include specific context about the step; dynamic context should be added under the step as an entry.</param>
        /// <param name="subStepsCanBeThreaded">True if within this build step the IBuildLogger will be used on multiple threads.</param>
        void BeginBuildStep(LogLevel level, string stepName, bool subStepsCanBeThreaded);

        /// <summary>
        /// Ends the build step.
        /// </summary>
        void EndBuildStep();
    }
    internal enum DeferredEventType
    {
        Begin,
        End,
        Info
    }

    internal struct DeferredEvent
    {
        public LogLevel Level;
        public DeferredEventType Type;
        public double Time;
        public string Name;
        public string Context;
    }

    internal interface IDeferredBuildLogger
    {
        void HandleDeferredEventStream(IEnumerable<DeferredEvent> events);
    }

    /// <summary>
    /// Interface for loggers that can produce Trace Event Profiler (TEP) output for viewing in Perfetto.
    /// </summary>
    public interface ILogTEP
    {
        /// <summary>
        /// Adds a named key/value metadata entry to the log, emitted as TEP metadata.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        void AddMetaData(string key, string value);

        /// <summary>Adds a named key/value arg to the current build step for Perfetto event annotation.</summary>
        /// <param name="key">The arg key.</param>
        /// <param name="value">The arg value.</param>
        void AddArg(string key, string value);

        /// <summary>
        /// Formats the log as a Trace Event Profiler (TEP) JSON document.
        /// </summary>
        /// <returns>The log contents in Trace Event Profiler format.</returns>
        string FormatForTraceEventProfiler();

        /// <summary>
        /// Imports events from an external Trace Event Profiler file into the current build step,
        /// so they appear alongside the host events in the formatted output.
        /// </summary>
        /// <param name="tepFilePath">The path of the Trace Event Profiler file to import.</param>
        void ImportExternalTEP(string tepFilePath);
    }


    /// <summary>
    /// Helper class to define a scope with a using statement
    /// </summary>
    public struct ScopedBuildStep : IDisposable
    {
        IBuildLogger m_Logger;
        internal ScopedBuildStep(LogLevel level, string stepName, IBuildLogger logger, bool multiThreaded, string context)
        {
            m_Logger = logger;
            m_Logger?.BeginBuildStep(level, stepName, multiThreaded);
            if (!string.IsNullOrEmpty(context))
                m_Logger?.AddEntrySafe(level, context);
        }

        /// <inheritdoc/>
        void IDisposable.Dispose()
        {
            m_Logger?.EndBuildStep();
        }
    }

    internal struct ProfileCaptureScope : IDisposable
    {
        IBuildLogger m_Logger;
        public ProfileCaptureScope(IBuildLogger logger, ProfileCaptureOptions options)
        {
            m_Logger = ScriptableBuildPipeline.useDetailedBuildLog ? logger : null;
            ContentBuildInterface.StartProfileCapture(options);
        }

        public void Dispose()
        {
            ContentBuildProfileEvent[] events = ContentBuildInterface.StopProfileCapture();

            if (m_Logger == null)
                return;

            IDeferredBuildLogger dLog = (IDeferredBuildLogger)m_Logger;
            IEnumerable<DeferredEvent> dEvents = events.Select(i =>
            {
                var e = new DeferredEvent();
                e.Level = LogLevel.Verbose;
                BuildLoggerExternsions.ConvertNativeEventName(i.Name, out e.Name, out e.Context);
                e.Time = (double)i.TimeMicroseconds / (double)1000;
                e.Type = BuildLoggerExternsions.ConvertToDeferredType(i.Type);
                return e;
            });
            dLog.HandleDeferredEventStream(dEvents);
        }
    }

    /// <summary>
    /// Contains extension methods for the IBuildLogger interface
    /// </summary>
    public static class BuildLoggerExternsions
    {
        /// <summary>
        /// Adds details to the active build step
        /// </summary>
        /// <param name="log">The build log.</param>
        /// <param name="level">The log level of this entry.</param>
        /// <param name="msg">The message to add.</param>
        public static void AddEntrySafe(this IBuildLogger log, LogLevel level, string msg)
        {
            if (log != null)
            {
                log.AddEntry(level, msg);
            }
        }

        /// <summary>
        /// Begins a new build step and returns an ScopedBuildStep which will end the build step when disposed. It is recommended to use this in conjunction with the using statement.
        /// </summary>
        /// <param name="log">The build log.</param>
        /// <param name="level">The log level of this step.</param>
        /// <param name="stepName">A name associated with the step.</param>
        /// <param name="multiThreaded">True if within this build step the IBuildLogger will be used on multiple threads.</param>
        /// <returns>Returns a ScopedBuildStep that will end the build step when it is disposed.</returns>
        public static ScopedBuildStep ScopedStep(this IBuildLogger log, LogLevel level, string stepName, bool multiThreaded = false)
        {
            return new ScopedBuildStep(level, stepName, log, multiThreaded, null);
        }

        /// <summary>
        /// Begins a new build step and returns an ScopedBuildStep which will end the build step when disposed. It is recommended to use this in conjunction with the using statement.
        /// </summary>
        /// <param name="log">The build log.</param>
        /// <param name="level">The log level of this step.</param>
        /// <param name="stepName">A name associated with the step.</param>
        /// <param name="context">Adds an entry message the build step. This allows attaching specific context data without changing the stepName.</param>
        /// <returns>Returns a ScopedBuildStep that will end the build step when it is disposed.</returns>
        public static ScopedBuildStep ScopedStep(this IBuildLogger log, LogLevel level, string stepName, string context)
        {
            return new ScopedBuildStep(level, stepName, log, false, context);
        }

        /// <summary>
        /// Adds a named key/value arg to the active build step. No-op if log is null or does not support named args.
        /// </summary>
        /// <param name="log">The build log.</param>
        /// <param name="key">The arg key.</param>
        /// <param name="value">The arg value.</param>
        public static void AddArgSafe(this IBuildLogger log, string key, string value)
        {
            if (log is ILogTEP logTep)
                logTep.AddArg(key, value);
        }

        /// <summary>
        /// Begins a new build step with named args attached, and returns a ScopedBuildStep.
        /// </summary>
        /// <param name="log">The build log.</param>
        /// <param name="level">The log level of this step.</param>
        /// <param name="stepName">A name associated with the step.</param>
        /// <param name="args">Named key/value pairs to attach to the step as Perfetto event args.</param>
        /// <returns>Returns a ScopedBuildStep that will end the build step when it is disposed.</returns>
        public static ScopedBuildStep ScopedStep(this IBuildLogger log, LogLevel level, string stepName, params (string key, string value)[] args)
        {
            var step = new ScopedBuildStep(level, stepName, log, false, null);
            if (log is ILogTEP logTep && args != null)
                foreach (var (key, value) in args)
                    logTep.AddArg(key, value);
            return step;
        }

        /// <summary>
        /// Begins a new build step with named args and threading support, and returns a ScopedBuildStep.
        /// </summary>
        /// <param name="log">The build log.</param>
        /// <param name="level">The log level of this step.</param>
        /// <param name="stepName">A name associated with the step.</param>
        /// <param name="multiThreaded">True if within this build step the IBuildLogger will be used on multiple threads.</param>
        /// <param name="args">Named key/value pairs to attach to the step as Perfetto event args.</param>
        /// <returns>Returns a ScopedBuildStep that will end the build step when it is disposed.</returns>
        public static ScopedBuildStep ScopedStep(this IBuildLogger log, LogLevel level, string stepName, bool multiThreaded, params (string key, string value)[] args)
        {
            var step = new ScopedBuildStep(level, stepName, log, multiThreaded, null);
            if (log is ILogTEP logTep && args != null)
                foreach (var (key, value) in args)
                    logTep.AddArg(key, value);
            return step;
        }

        internal static DeferredEventType ConvertToDeferredType(ProfileEventType type)
        {
            if (type == ProfileEventType.Begin) return DeferredEventType.Begin;
            if (type == ProfileEventType.End) return DeferredEventType.End;
            if (type == ProfileEventType.Info) return DeferredEventType.Info;
            throw new Exception("Unknown type");
        }

        const string k_WriteFile = "Write file:";
        const string k_WriteObject = "Write object - ";

        internal static void ConvertNativeEventName(string nativeName, out string eventName, out string eventContext)
        {
            eventName = nativeName;
            eventContext = "";
            if (nativeName.StartsWith(k_WriteFile, StringComparison.Ordinal))
            {
                eventName = "Write File";
                eventContext = nativeName.Substring(k_WriteFile.Length);
            }
            else if (nativeName.StartsWith(k_WriteObject, StringComparison.Ordinal))
            {
                eventName = "Write Object";
                eventContext = nativeName.Substring(k_WriteObject.Length);
            }

            if (eventContext.Any(c => c == '"'))
                eventContext = eventContext.Replace("\"", "\\\"");
        }
    }
}
