# Build Logging

Scriptable Build Pipeline has a profiling instrumentation system enabling build performance logging. By default, building AssetBundles will create a .json log file in the Trace Event Profiler Format within the target output directory. The file contains timing measurements of various build tasks and can be viewed using the [Trace Event Profiling Tool](https://www.chromium.org/developers/how-tos/trace-event-profiling-tool).

The default logger can be overriden by passing in an [IBuildLogger](../api/UnityEditor.Build.Pipeline.Interfaces.IBuildLogger.html) object as a context object input. This could be useful if you want to log performance data in a different format or want the build events to be added to a custom performance repot. The [BuildLog](../api/UnityEditor.Build.Pipeline.Utilities.BuildLog.html) class implements [IBuildLogger](../api/UnityEditor.Build.Pipeline.Interfaces.IBuildLogger.html) and is used as the default logger.


# Adding Custom Instrumentation

If you are creating or modifying build tasks that could affect build performance, you should consider adding instrumentation blocks to your new code. You can do this by calling the [IBuildLogger](../api/UnityEditor.Build.Pipeline.Interfaces.IBuildLogger.html) methods directly or using the [ScopedStep](../api/UnityEditor.Build.Pipeline.Interfaces.BuildLoggerExternsions.html) and [AddEntrySafe](../api/UnityEditor.Build.Pipeline.Interfaces.BuildLoggerExternsions.html) extension methods.
