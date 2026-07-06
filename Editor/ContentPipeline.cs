using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Utilities;
using UnityEditor.Modules;

namespace UnityEditor.Build.Pipeline
{
    /// <summary>
    /// Static class containing the main content building entry points into the Scriptable Build Pipeline.
    /// </summary>
    public static class ContentPipeline
    {
        /// <summary>
        /// Default temporary path used for building content data.
        /// </summary>
        public const string kTempBuildPath = "Temp/ContentBuildData";

        /// <summary>
        /// Default temporary path used for building script data.
        /// </summary>
        public const string kScriptBuildPath = "Library/PlayerScriptAssemblies";

        /// <summary>
        /// Default callback implementation.
        /// </summary>
        public static BuildCallbacks BuildCallbacks = new BuildCallbacks();

        /// <summary>
        /// <para>Default implementation of generating Asset Bundles using the Scriptable Build Pipeline.</para>
        /// <para>The target platform must be installed. Otherwise AssetBundles will be built based on the editor version of the Assemblies and may have incorrect content.</para>
        /// </summary>
        /// <param name="parameters">Set of parameters used for building asset bundles.</param>
        /// <param name="content">Set of content and explicit asset bundle layout to build.</param>
        /// <param name="result">Results from building the content and explicit asset bundle layout.</param>
        /// <returns>Return code with status information about success or failure causes.</returns>
        public static ReturnCode BuildAssetBundles(IBundleBuildParameters parameters, IBundleBuildContent content, out IBundleBuildResults result)
        {
            BuildContext buildContext = new BuildContext();
            return BuildAssetBundles(buildContext, parameters, content, out result);
        }

        /// <summary>
        /// <para>Default implementation of generating Asset Bundles using the Scriptable Build Pipeline.</para>
        /// <para>The target platform must be installed. Otherwise AssetBundles will be built based on the editor version of the Assemblies and may have incorrect content.</para>
        /// </summary>
        /// <param name="buildContext">The build context to use for this build.</param>
        /// <param name="parameters">Set of parameters used for building asset bundles.</param>
        /// <param name="content">Set of content and explicit asset bundle layout to build.</param>
        /// <param name="result">Results from building the content and explicit asset bundle layout.</param>
        /// <returns>Return code with status information about success or failure causes.</returns>
        public static ReturnCode BuildAssetBundles(BuildContext buildContext, IBundleBuildParameters parameters, IBundleBuildContent content, out IBundleBuildResults result)
        {
            var taskList = DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleCompatible);
            return BuildAssetBundles(buildContext, parameters, content, out result, taskList);
        }

        /// <summary>
        /// <para>Default implementation of generating Asset Bundles using the Scriptable Build Pipeline.</para>
        /// <para>The target platform must be installed. Otherwise AssetBundles will be built based on the editor version of the Assemblies and may have incorrect content.</para>
        /// </summary>
        /// <param name="parameters">Set of parameters used for building asset bundles.</param>
        /// <param name="content">Set of content and explicit asset bundle layout to build.</param>
        /// <param name="result">Results from building the content and explicit asset bundle layout.</param>
        /// <param name="taskList">Custom task list for building asset bundles.</param>
        /// <param name="contextObjects">Additional context objects to make available to the build.</param>
        /// <returns>Return code with status information about success or failure causes.</returns>
        public static ReturnCode BuildAssetBundles(IBundleBuildParameters parameters, IBundleBuildContent content, out IBundleBuildResults result, IList<IBuildTask> taskList,
            params IContextObject[] contextObjects)
        {
            BuildContext buildContext = new BuildContext(contextObjects);
            return BuildAssetBundles(buildContext, parameters, content, out result, taskList, contextObjects);
        }

        /// <summary>
        /// <para>Default implementation of generating Asset Bundles using the Scriptable Build Pipeline.</para>
        /// <para>The target platform must be installed. Otherwise AssetBundles will be built based on the editor version of the Assemblies and may have incorrect content.</para>
        /// </summary>
        /// <param name="buildContext">The build context to use for this build.</param>
        /// <param name="parameters">Set of parameters used for building asset bundles.</param>
        /// <param name="content">Set of content and explicit asset bundle layout to build.</param>
        /// <param name="result">Results from building the content and explicit asset bundle layout.</param>
        /// <param name="taskList">Custom task list for building asset bundles.</param>
        /// <param name="contextObjects">Additional context objects to make available to the build.</param>
        /// <returns>Return code with status information about success or failure causes.</returns>
        public static ReturnCode BuildAssetBundles(BuildContext buildContext, IBundleBuildParameters parameters, IBundleBuildContent content, out IBundleBuildResults result,
            IList<IBuildTask> taskList, params IContextObject[] contextObjects)
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                result = null;
                BuildLogger.LogException(new InvalidOperationException("Cannot build asset bundles while a build is in progress"));
                return ReturnCode.Exception;
            }

            // Avoid throwing exceptions in here as we don't want them bubbling up to calling user code
            if (parameters == null)
            {
                result = null;
                BuildLogger.LogException(new ArgumentNullException("parameters"));
                return ReturnCode.Exception;
            }

            // Avoid throwing exceptions in here as we don't want them bubbling up to calling user code
            if (taskList.IsNullOrEmpty())
            {
                result = null;
                BuildLogger.LogException(new ArgumentException("Argument cannot be null or empty.", "taskList"));
                return ReturnCode.Exception;
            }

            var uniqueAddresses = new HashSet<string>();
            //internal ids need to be unique but only per bundle.
            foreach (var b in content.BundleLayout)
            {
                uniqueAddresses.Clear();
                foreach (var guid in b.Value)
                {
                    if (!content.Addresses.TryGetValue(guid, out var address))
                    {
                        result = null;
                        BuildLogger.LogException(new InvalidOperationException($"Unable to find internal id for guid {guid} in bundle {b.Key}."));
                        return ReturnCode.Exception;
                    }

                    if (!uniqueAddresses.Add(address))
                    {
                        result = null;
                        BuildLogger.LogException(
                            new InvalidOperationException($"Duplicate internal id '{address}' for guid {guid} found in bundle {b.Key}. Each internal id within a bundle must be unique."));
                        return ReturnCode.Exception;
                    }
                }
            }

            var contentBuildSettings = parameters.GetContentBuildSettings();
            if (!CanBuildPlayer(contentBuildSettings.target, contentBuildSettings.group))
            {
                result = null;
                BuildLogger.LogException(new InvalidOperationException("Unable to build with the current configuration, please check the Build Settings."));
                return ReturnCode.Exception;
            }

            // Don't run if there are unsaved changes
            if (ValidationMethods.HasDirtyScenes())
            {
                result = null;
                return ReturnCode.UnsavedChanges;
            }

            ThreadingManager.WaitForOutstandingTasks();
            BuildLog buildLog = null;

            IBuildLogger logger;
            if (!buildContext.TryGetContextObject<IBuildLogger>(out logger))
            {
                logger = buildLog = new BuildLog();
                buildContext.SetContextObject(buildLog);
            }

            using (logger.ScopedStep(LogLevel.Info, "AssetDatabase.SaveAssets"))
                AssetDatabase.SaveAssets();

            ReturnCode exitCode;
            result = new BundleBuildResults();

#if !CI_TESTRUNNER_PROJECT
            using (new SceneStateCleanup(logger))
            using (var progressTracker = new ProgressTracker())
#else
            using (var progressTracker = new ProgressLoggingTracker())
#endif
            {
#if ENABLE_BUILDUSAGE_WARNING_SCOPE
                using (new UnityEditor.Build.Content.BuildUsageWarningScope())
#endif
                using (new AutoBuildCacheUtility(logger))
                using (var interfacesWrapper = new BuildInterfacesWrapper(logger))
                using (var buildCache = new BuildCache(logger))
                {
                    using (logger.ScopedStep(LogLevel.Verbose, "Create temp folders"))
                    {
                        BuildCacheUtility.SetCurrentBuildContent(content);
                        Directory.CreateDirectory(parameters.TempOutputFolder);
                        Directory.CreateDirectory(parameters.ScriptOutputFolder);
                    }

                    try
                    {
                        using (logger.ScopedStep(LogLevel.Verbose, "Initialize Context"))
                        {
                            buildContext.SetContextObject(parameters);
                            buildContext.SetContextObjectIfNull(content);
                            buildContext.SetContextObjectIfNull(result);
                            buildContext.SetContextObjectIfNull(interfacesWrapper);
                            buildContext.SetContextObjectIfNull(progressTracker);
                            buildContext.SetContextObjectIfNull(buildCache);
                            // If IDeterministicIdentifiers was passed in with contextObjects, don't add the default
                            if (!buildContext.ContainsContextObject(typeof(IDeterministicIdentifiers)))
                                buildContext.SetContextObjectIfNull(parameters.ContiguousBundles ? new PrefabPackedIdentifiers() : (IDeterministicIdentifiers)new Unity5PackedIdentifiers());
                            buildContext.SetContextObjectIfNull(new BuildDependencyData());
                            buildContext.SetContextObjectIfNull(new ObjectDependencyData());
                            buildContext.SetContextObjectIfNull(new BundleWriteData());
                            buildContext.SetContextObjectIfNull(BuildCallbacks);
                            buildCache.SetBuildLogger(logger);
                        }
                    }
                    catch (Exception e)
                    {
                        // Avoid throwing exceptions in here as we don't want them bubbling up to calling user code
                        result = null;
                        BuildLogger.LogException(e);
                        return ReturnCode.Exception;
                    }

                    using (logger.ScopedStep(LogLevel.Verbose, "Validate tasks"))
                    {
                        exitCode = BuildTasksRunner.Validate(taskList, buildContext);
                    }

                    using (logger.ScopedStep(LogLevel.Verbose, "Run tasks"))
                    {
                        if (exitCode >= ReturnCode.Success)
#if SBP_PROFILER_ENABLE
                            exitCode = BuildTasksRunner.RunProfiled(taskList, buildContext);
#else
                            exitCode = BuildTasksRunner.Run(taskList, buildContext);
#endif
                        logger.AddArgSafe("ExitCode", exitCode.ToString());
                    }

                    if (Directory.Exists(parameters.TempOutputFolder))
                    {
                        using (logger.ScopedStep(LogLevel.Verbose, "Cleanup temp folder", parameters.TempOutputFolder))
                        {
                            Directory.Delete(parameters.TempOutputFolder, true);
                        }
                    }

                    if (buildLog != null)
                    {
                        using (logger.ScopedStep(LogLevel.Verbose, "Write buildlogtep.json"))
                        {
                            string buildLogPath = parameters.GetOutputFilePathForIdentifier("buildlogtep.json");
                            logger.AddArgSafe("BuildLogPath", buildLogPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(buildLogPath));
                            File.WriteAllText(parameters.GetOutputFilePathForIdentifier("buildlogtep.json"), buildLog.FormatForTraceEventProfiler());
                        }
                    }
                }
            }


            using (logger.ScopedStep(LogLevel.Verbose, "Prune build cache"))
            {
                long maximumCacheSize = ScriptableBuildPipeline.maximumCacheSize * BuildCache.k_BytesToGigaBytes;
                logger.AddArgSafe("MaxCacheSize", maximumCacheSize.ToString());
                BuildCache.PruneCache_Background(maximumCacheSize);
            }

            return exitCode;
        }

        internal static bool CanBuildPlayer(BuildTarget target, BuildTargetGroup targetGroup)
        {
            // The Editor APIs we need only exist in 2021.3 and later. For earlier versions, assume we can build.
            return CanBuildPlayer(target, targetGroup, GetBuildWindowExtension(target, targetGroup));
        }

        private static IBuildWindowExtension GetBuildWindowExtension(BuildTarget target, BuildTargetGroup targetGroup)
        {
            var module = ModuleManager.GetTargetStringFrom(target);
            return ModuleManager.GetBuildWindowExtension(module);
        }

        internal static bool CanBuildPlayer(BuildTarget target, BuildTargetGroup targetGroup, IBuildWindowExtension buildWindowExtension)
        {
            // we expect this to mainly happen within yamato when no build target modules are installed
            if (!BuildPipeline.IsBuildTargetSupported(targetGroup, target))
            {
                BuildLogger.LogWarning("The currently selected build target is not supported. If the build fails please check the Build Settings.");
                return true;
            }

            return buildWindowExtension != null ? buildWindowExtension.EnabledBuildButton() : false;
        }
    }
}
