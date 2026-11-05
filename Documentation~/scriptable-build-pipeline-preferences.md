# Scriptable Build Pipeline Preferences window reference

Preferences are saved in a `ProjectSettings/ScriptableBuildPipeline.json` file and apply to the whole project.

To access the Preferences for the Scriptable Build Pipeline, go to **Edit** > **Preferences** > **Scriptable Build Pipeline** (macOS: **Unity** > **Settings**).

## General Settings

The **General Settings** section contains the following settings:

| Property | Description |
| :---- | :---- |
| **Threaded Archiving**  | Enables multithreading for the archiving and compressing stage of the build. Disable to run on a single thread. <br><br>**Threaded Archiving** is enabled by default but is only available if your Unity version supports multithreaded archiving. |
| **Log Cache Miss** | Logs a warning when the build cache misses, and points out the problematic asset and dependency. Use **Log Cache Miss** to understand why incremental builds rebuild content. |
| **Log Asset Warnings**  | Logs warnings for invalid asset references. <br><br>**Log Asset Warnings** is enabled by default. |
| **Slim Write Results**  | Caches the minimum `WriteResults` data to reduce cache size and improve cache performance. <br><br>**Slim Write Results** is enabled by default. Disable it only if a custom build task needs the full write results. |
| **Use Detailed Build Log**  | Writes detailed event information to the build log. |
| **Use V2 Hasher**  | Uses the same hasher as Asset Database V2, which improves cache performance but invalidates the existing build cache. Triggers a full rebuild for the next build. <br><br>**Use V2 Hasher** is enabled by default. |
| **FileID Generator Seed**  | Provides a seed to the hash, which prevents file identifier collisions during build. Avoid changing this value after your project is released. <br><br>The default seed is 0. Generate a random seed with **Random**. |
| **Prefab Packed Header Size**  | Specifies the size of the header for PrefabPacked Asset Bundles in bytes to avoid file identifier collisions during build. Objects from the same asset share the same leading bytes and belong to the same AssetBundle. Higher size values reduce collisions between assets and enables more precise grouping of objects by asset. However, this makes it harder to differentiate objects within an asset and increases the risk of file identifier collisions in assets that contain many objects. <br><br>Avoid changing this value after your project is released as it changes file identifiers for all objects in all bundles. |

## Build Cache

The **Build Cache** section contains the following settings:

| Property | Description |
| :---- | :---- |
| **Maximum Cache Size (GB)**  | Keeps the build cache size under this value when possible. The default value is 20 GB. |
| **Purge Cache**  | Deletes the whole build cache folder. |
| **Prune Cache**  | Deletes the entries that were the least recently accessed until the cache is under the maximum cache size.  |

## Additional resources

* [Analyze build logs](BuildLogger.md)
* [ScriptableBuildPipeline Scripting API](xref:UnityEditor.Build.Pipeline.Utilities.ScriptableBuildPipeline)
* [Addressables Preferences reference](https://docs.unity3d.com/Packages/com.unity.addressables@latest?subfolder=/manual/addressables-preferences.html)
* [Unity’s Object base class](xref:um-class-object)