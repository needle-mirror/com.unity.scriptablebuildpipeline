# Load files by name

By default, AssetBundles built with the Scriptable Build Pipeline (SBP) only support loading assets by full path. This is to avoid loading collisions that occur when two assets in the same AssetBundle have different full paths but the same file name.

To change this behavior, use the [AssetBundleBuild.addressableNames](https://docs.unity3d.com/ScriptReference/AssetBundleBuild-addressableNames.html) field, or use the `ContentPipeline.BuildAssetBundles` API to change the loading path with [`IBundleBuildContent.Addresses`](xref:UnityEditor.Build.Pipeline.Interfaces.IBundleBuildContent).

The following example shows how to use the [`CompatibilityBuildPipeline`](xref:UnityEditor.Build.Pipeline.CompatibilityBuildPipeline) methods to load a file by file name instead of by full file path. The [`ContentBuildInterface.GenerateAssetBundleBuilds`](xref:UnityEditor.Build.Content.ContentBuildInterface.GenerateAssetBundleBuilds) method gets the set of bundles and assets to build, then modifies the [`addressableNames`](xref:UnityEditor.AssetBundleBuild.addressableNames) field to set the loading path of the file name instead of the full path.

```c#
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Content;
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

        // Get the set of bundles to build
        var bundles = ContentBuildInterface.GenerateAssetBundleBuilds();
        // Update the addressableNames to load by the file name without extension
        for (var i = 0; i < bundles.Length; i++)
            bundles[i].addressableNames = bundles[i].assetNames.Select(Path.GetFileNameWithoutExtension).ToArray();

        var manifest = CompatibilityBuildPipeline.BuildAssetBundles(outputPath, bundles, options, buildTarget);
        return manifest != null;
    }
}
```

## Additional resources

* [Build AssetBundles](build-assetbundles.md)
* [Convert existing AssetBundle code](UpgradeGuide.md)
* [Introduction to loading Addressable assets](https://docs.unity3d.com/Packages/com.unity.addressables@latest?subfolder=/manual/load-addressable-assets.html)
