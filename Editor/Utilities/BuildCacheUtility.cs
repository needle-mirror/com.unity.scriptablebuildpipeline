using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Utilities;
using UnityEngine;

internal static class BuildCacheUtility
{
    static Dictionary<KeyValuePair<GUID, int>, CacheEntry> m_GuidToHash = new Dictionary<KeyValuePair<GUID, int>, CacheEntry>();
    static Dictionary<KeyValuePair<string, int>, CacheEntry> m_PathToHash = new Dictionary<KeyValuePair<string, int>, CacheEntry>();

    public static CacheEntry GetCacheEntry(GUID asset, int version = 1)
    {
        CacheEntry entry;
        KeyValuePair<GUID, int> key = new KeyValuePair<GUID, int>(asset, version);
        if (m_GuidToHash.TryGetValue(key, out entry))
            return entry;

        entry = new CacheEntry { Guid = asset, Version = version };
        string path = AssetDatabase.GUIDToAssetPath(asset.ToString());
        entry.Type = CacheEntry.EntryType.Asset;

        if (path.Equals(CommonStrings.UnityBuiltInExtraPath, StringComparison.OrdinalIgnoreCase) || path.Equals(CommonStrings.UnityDefaultResourcePath, StringComparison.OrdinalIgnoreCase))
            entry.Hash = HashingMethods.Calculate(Application.unityVersion, path).ToHash128();
        else
        {
            entry.Hash = AssetDatabase.GetAssetDependencyHash(path);
            if (!entry.Hash.isValid && File.Exists(path))
                entry.Hash = HashingMethods.CalculateFile(path).ToHash128();
        }

        if (entry.Hash.isValid)
            entry.Hash = HashingMethods.Calculate(entry.Hash, entry.Version).ToHash128();

        m_GuidToHash[key] = entry;
        return entry;
    }

    public static CacheEntry GetCacheEntry(string path, int version = 1)
    {
        CacheEntry entry;
        KeyValuePair<string, int> key = new KeyValuePair<string, int>(path, version);
        if (m_PathToHash.TryGetValue(key, out entry))
            return entry;

        var guid = AssetDatabase.AssetPathToGUID(path);
        if (!string.IsNullOrEmpty(guid))
            return GetCacheEntry(new GUID(guid), version);

        entry = new CacheEntry { File = path, Version = version };
        entry.Guid = HashingMethods.Calculate("FileHash", entry.File).ToGUID();
        if (File.Exists(entry.File))
            entry.Hash = HashingMethods.Calculate(HashingMethods.CalculateFile(entry.File), entry.Version).ToHash128();
        entry.Type = CacheEntry.EntryType.File;

        m_PathToHash[key] = entry;
        return entry;
    }

    internal static void ClearCacheHashes()
    {
        m_GuidToHash.Clear();
        m_PathToHash.Clear();
    }

    public static CacheEntry GetCacheEntry(ObjectIdentifier objectID, int version = 1)
    {
        if (objectID.guid.Empty())
            return GetCacheEntry(objectID.filePath, version);
        return GetCacheEntry(objectID.guid, version);
    }
}
