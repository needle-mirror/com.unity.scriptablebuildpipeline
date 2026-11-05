# Analyze build logs

The Scriptable Build Pipeline (SBP) has a profiling instrumentation system that enables build performance logging. By default, AssetBundles built with SBP create a `buildlogtep.json` log file in the Trace Event Format inside the target output directory. The file contains timing measurements of various build tasks.

**Note**: For Addressables, the default build log file is `Library/com.unity.addressables/AddressablesBuildTEP.json`. For more information about build logs in Addressables, refer to [Build profile logs](https://docs.unity3d.com/Packages/com.unity.addressables@latest/manual/BuildProfileLog.html).

## View log file

To view the log file, do the following:
1. Open any Chromium-based browser.
1. Enter `chrome://tracing` in the browser to view the log file with the [Trace Event Profiling Tool](https://www.chromium.org/developers/how-tos/trace-event-profiling-tool).
1. Click **Load** to open your log file.

## Create a custom logger

Create a custom logger to log performance data in a different format than the default logger, or to add build events to a custom performance report.

The [`BuildLog`](xref:UnityEditor.Build.Pipeline.Utilities.BuildLog) class represents the default logger and implements the [`IBuildLogger`](xref:UnityEditor.Build.Pipeline.Interfaces.IBuildLogger) interface. To override the default logger, pass an `IBuildLogger` object as a context object input.

**Note**: SBP doesn't produce a `buildlogtep.json` file if you supply your own logger.

## Add custom instrumentation

Consider adding instrumentation blocks to your new code when you create or modify build tasks that can affect build performance. Call the [`IBuildLogger`](xref:UnityEditor.Build.Pipeline.Interfaces.IBuildLogger) methods directly, or use the [`ScopedStep`](xref:UnityEditor.Build.Pipeline.Interfaces.BuildLoggerExternsions.ScopedStep) and [`AddEntrySafe`](xref:UnityEditor.Build.Pipeline.Interfaces.BuildLoggerExternsions.AddEntrySafe) extension methods.

## Additional resources
* [BuildLog Scripting API](xref:UnityEditor.Build.Pipeline.Utilities.BuildLog)
* [IBuildLogger Scripting API](xref:UnityEditor.Build.Pipeline.Interfaces.IBuildLogger)
* [BuildLogger Scripting API](xref:UnityEditor.Build.Pipeline.Utilities.BuildLogger)
* [BuildLoggerExternsions Scripting API](xref:UnityEditor.Build.Pipeline.Interfaces.BuildLoggerExternsions)