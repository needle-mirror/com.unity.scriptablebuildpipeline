# Build AssetBundles

To build an AssetBundle with the Scriptable Build Pipeline (SBP), use the [`ContentPipeline.BuildAssetBundles`](xref:UnityEditor.Build.Pipeline.ContentPipeline.BuildAssetBundles(UnityEditor.Build.Pipeline.Interfaces.IBundleBuildParameters,UnityEditor.Build.Pipeline.Interfaces.IBundleBuildContent,UnityEditor.Build.Pipeline.Interfaces.IBundleBuildResults@)) method. You need to supply at least the following parameters:

* The build parameters: An object that implements the [`IBuildParameters`](xref:UnityEditor.Build.Pipeline.Interfaces.IBundleBuildParameters) interface. The object specifies the `BuildTarget`, the `BuildTargetGroup`, the output path, and additional optional properties.

* The content to build: An object that implements the [`IBundleBuildContent`](xref:UnityEditor.Build.Pipeline.Interfaces.IBundleBuildContent) interface. The object specifies what assets to build and what assets go in which bundle.

* A results object: An object that implements the [`IBundleBuildResults`](xref:UnityEditor.Build.Pipeline.Interfaces.IBundleBuildResults) interface. The object receives the details of the built AssetBundles. This is an `out` parameter, so calls to the `BuildAssetBundles` method doesn’t construct it.

**Note**: The `UnityEditor.Build.Pipeline` namespace contains default implementations for all the SBP required interfaces. Implementation names are the same as the interface names without the leading `I`. For example, [`BuildParameters`](xref:UnityEditor.Build.Pipeline.BuildParameters) is the implementation of the `IBuildParameters` interface.

For existing projects, replace calls to the `BuildPipeline.BuildAssetBundles` method with calls to `CompatibilityBuildPipeline.BuildAssetBundles` to build AssetBundles with SBP. For more information about equivalent methods, refer to [Convert existing AssetBundle code](UpgradeGuide.md).

## Build AssetBundles with Scriptable Build Pipeline

This example assumes that you're already familiar with the basic usage of `BuildPipeline.BuildAssetBundles` and want to switch to the Scriptable Build Pipeline.

The following code example shows how AssetBundles are currently built:

```c#
using System.IO;
using UnityEditor;

public static class BuildAssetBundlesExample
{
    public static bool BuildAssetBundles(string outputPath, bool forceRebuild, bool useChunkBasedCompression, BuildTarget buildTarget)
    {
        var options = BuildAssetBundleOptions.None;
        if (useChunkBasedCompression)
            options |= BuildAssetBundleOptions.ChunkBasedCompression;

        if (forceRebuild)
            options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;

        Directory.CreateDirectory(outputPath);
        var manifest = BuildPipeline.BuildAssetBundles(outputPath, options, buildTarget);
        return manifest != null;
    }
}
```

To convert the previous code example to use SBP, add a new `using UnityEditor.Build.Pipeline` and replace the call to `BuildPipeline.BuildAssetBundles` with `CompatibilityBuildPipeline.BuildAssetBundles` as follows:

```c#
using System.IO;
using UnityEditor;
// Added new using
using UnityEditor.Build.Pipeline;

public static class BuildAssetBundlesExample
{
    public static bool BuildAssetBundles(string outputPath, bool forceRebuild, bool useChunkBasedCompression, BuildTarget buildTarget)
    {
        var options = BuildAssetBundleOptions.None;
        if (useChunkBasedCompression)
            options |= BuildAssetBundleOptions.ChunkBasedCompression;

        if (forceRebuild)
            options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;

        Directory.CreateDirectory(outputPath);
        // Replaced BuildPipeline.BuildAssetBundles with CompatibilityBuildPipeline.BuildAssetBundles here
        var manifest = CompatibilityBuildPipeline.BuildAssetBundles(outputPath, options, buildTarget);
        return manifest != null;
    }
}
```

**Note**: Some changes in the SBP building and loading process don't match the BuildPipeline behavior. For more information on these changes, refer to [Convert existing AssetBundle code](UpgradeGuide.md).

## Compress AssetBundles in a build

The following example shows how to build your AssetBundles using different compression levels for each AssetBundle. This is useful if you're planning on shipping part of your bundles as LZ4 or Uncompressed with the Player and want to download other bundles as LZMA later.

The simplest implementation is to create a custom build parameters class that inherits from [`BundleBuildParameters`](xref:UnityEditor.Build.Pipeline.BundleBuildParameters) and override the [`GetCompressionForIdentifier`](xref:UnityEditor.Build.Pipeline.Interfaces.IBuildParameters.GetCompressionForIdentifier) method. Then construct and pass this into the `ContentPipeline.BuildAssetBundles` method.

```c#
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

public static class BuildAssetBundlesExample
{
    // New parameters class inheriting from BundleBuildParameters
    class CustomBuildParameters : BundleBuildParameters
    {
        public Dictionary<string, BuildCompression> PerBundleCompression { get; set; }

        public CustomBuildParameters(BuildTarget target, BuildTargetGroup group, string outputFolder) : base(target, group, outputFolder)
        {
            PerBundleCompression = new Dictionary<string, BuildCompression>();
        }

        // Override the GetCompressionForIdentifier method with new logic
        public override BuildCompression GetCompressionForIdentifier(string identifier)
        {
            BuildCompression value;
            if (PerBundleCompression.TryGetValue(identifier, out value))
                return value;
            return BundleCompression;
        }
    }

    public static bool BuildAssetBundles(string outputPath, bool useChunkBasedCompression, BuildTarget buildTarget, BuildTargetGroup buildGroup)
    {
        var buildContent = new BundleBuildContent(ContentBuildInterface.GenerateAssetBundleBuilds());
        // Construct the new parameters class
        var buildParams = new CustomBuildParameters(buildTarget, buildGroup, outputPath);

        // Set the default compression for bundles that don't have a specific compression set
        buildParams.BundleCompression = useChunkBasedCompression ? BuildCompression.LZ4 : BuildCompression.LZMA;

        // Populate the bundle specific compression data
        buildParams.PerBundleCompression.Add("Bundle1", BuildCompression.Uncompressed);
        buildParams.PerBundleCompression.Add("Bundle2", BuildCompression.LZMA);

        IBundleBuildResults results;
        ReturnCode exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results);
        return exitCode == ReturnCode.Success;
    }
}

```

## Additional resources

* [Class ContentPipeline](xref:UnityEditor.Build.Pipeline.ContentPipeline)
* [Class BuildParameters](xref:UnityEditor.Build.Pipeline.BuildParameters)
* [Build Compression](xref:UnityEngine.BuildCompression)
