# Get started with Scriptable Build Pipeline

The [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest) package uses the Scriptable Build Pipeline (SBP) package to build AssetBundles. If you want to create builds for new projects, it's recommended to use the Addressables package directly with AssetBundles or content directories (requires Unity 6.6 or later), rather than using the SBP package.

## Benefits of the Scriptable Build Pipeline package

You can use the SBP package directly in projects with custom AssetBundle build code. The SBP gives improved deterministic outputs if you build AssetBundles with the [`CompatibilityBuildPipeline`](xref:UnityEditor.Build.Pipeline.CompatibilityBuildPipeline) class. If your project currently uses [`BuildPipeline.BuildAssetBundles`](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildAssetBundles.html) to build AssetBundles, then you can update your build scripts to use the `CompatibilityBuildPipeline.BuildAssetBundles` method instead. For more information about converting BuildPipeline methods to their SBP equivalent, refer to [Convert existing AssetBundle code](UpgradeGuide.md).

The SBP makes it possible to customize the build process with your own build flows, derived classes, and other code you write to run during the build itself. However, this type of customization is intended for advanced use cases. For example, you can use the SBP to create custom build tooling with custom task lists. You can also access `DefaultBuildTasks` presets and use `ContentPipeline.BuildCallbacks` to receive callbacks at key phases of the build, such as after dependency calculation, packing, script compilation, or content writing.

To better understand how to use the SBP package, refer to the following use cases:
* [Build Asset Bundles with the Scriptable Build Pipeline](build-assetbundles.md)
* [Load files by name instead of by file path](load-files-by-name.md)
* [Investigate build performance with log data](BuildLogger.md)

## Drawbacks of the Scriptable Build Pipeline package

Upgrading to newer versions of the SBP can present challenges. For example, changes to the underlying package and `DefaultBuildTasks` might not be compatible with your customizations. To avoid this problem, try to make custom build scripts perform actions before and after the AssetBundle build.

## Additional resources

* [Scriptable Build Pipeline Scripting API](https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@latest/api/index.html)
* [Convert AssetBundles to Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest?subfolder=/manual/convert-assetbundles.html)
* [Create a custom build script](https://docs.unity3d.com/Packages/com.unity.addressables@latest?subfolder=/manual/build-scripting-custom.html)
* [AssetBundles](xref:um-asset-bundles-intro)
