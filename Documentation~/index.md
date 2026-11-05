# Scriptable Build Pipeline package

The Scriptable Build Pipeline (SBP) is a low-level package used by the Addressables package to build AssetBundles. The SBP package runs a pre-defined build flow for AssetBundles, which reduces build time, improves incremental build processing, and uses C# instead of C++ to support orchestration, caching, and asset dependency calculation.

SBP can work as a standalone package to build AssetBundles and is useful if you want to customize your build process with build preferences or to manage the build cache. However, you don’t need to use the SBP package directly to build AssetBundles. Instead, it's recommended to use the Addressables package to create content builds with [AssetBundles](xref:um-asset-bundles-intro) or [content directories](xref:um-content-directories-introduction) (requires Unity 6.6 or later).

| Topic | Description |
| :---- | :---- |
| [Install Scriptable Build Pipeline](install-and-upgrade.md) | Install the Scriptable Build Pipeline package for your project. |
| [Get started with Scriptable Build Pipeline](GettingStarted.md) | Learn about how to use the Scriptable Build Pipeline package. |
| [Convert existing AssetBundle code](UpgradeGuide.md) | Understand which Build Pipeline features are compatible with the Scriptable Build Pipeline. |
| [Build AssetBundles](build-assetbundles.md) | Build AssetBundles with the Scriptable Build Pipeline. |
| [Load files by name](load-files-by-name.md) | Load files with their names instead of their paths. |
| [Analyze build logs](BuildLogger.md) | Examine AssetBundles build performance data. |
| [Scriptable Build Pipeline Preferences window reference](scriptable-build-pipeline-preferences.md) | Reference for the Scriptable Build Pipeline Preferences window. |

## Additional resources

* [Addressables package](https://docs.unity3d.com/Packages/com.unity.addressables@latest)
* [Building and publishing](xref:um-building-and-publishing)
* [Choose a content build system](https://docs.unity3d.com/Packages/com.unity.addressables@latest?subfolder=/manual/content-build-systems.html)
