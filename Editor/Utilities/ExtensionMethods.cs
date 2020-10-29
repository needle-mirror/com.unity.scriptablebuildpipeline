using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.Build.Pipeline.Utilities
{
    static class ExtensionMethods
    {
        public static bool IsNullOrEmpty<T>(this ICollection<T> collection)
        {
            return collection == null || collection.Count == 0;
        }

        public static void GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue value) where TValue : new()
        {
            if (dictionary.TryGetValue(key, out value))
                return;

            value = new TValue();
            dictionary.Add(key, value);
        }

        public static void Swap<T>(this IList<T> list, int first, int second)
        {
            T temp = list[second];
            list[second] = list[first];
            list[first] = temp;
        }

        public static void GatherSerializedObjectCacheEntries(this WriteCommand command, HashSet<CacheEntry> cacheEntries)
        {
            if (command.serializeObjects != null)
            {
                var objectIds = command.serializeObjects.Select(x => x.serializationObject);
                var types = BuildCacheUtility.GetTypeForObjects(objectIds);
                cacheEntries.UnionWith(types.Select(BuildCacheUtility.GetCacheEntry));
                cacheEntries.UnionWith(objectIds.Select(BuildCacheUtility.GetCacheEntry));
            }
        }

        public static void ExtractCommonCacheData(IBuildCache cache, IEnumerable<ObjectIdentifier> includedObjects, IEnumerable<ObjectIdentifier> referencedObjects, HashSet<Type> uniqueTypes, List<KeyValuePair<ObjectIdentifier, Type[]>> objectTypes, HashSet<CacheEntry> dependencies)
        {
            if (includedObjects != null)
            {
                foreach (var objectId in includedObjects)
                {
                    var types = BuildCacheUtility.GetTypeForObject(objectId);
                    objectTypes.Add(new KeyValuePair<ObjectIdentifier, Type[]>(objectId, types));
                    uniqueTypes.UnionWith(types);
                }
            }
            if (referencedObjects != null)
            {
                foreach (var objectId in referencedObjects)
                {
                    var types = BuildCacheUtility.GetTypeForObject(objectId);
                    objectTypes.Add(new KeyValuePair<ObjectIdentifier, Type[]>(objectId, types));
                    uniqueTypes.UnionWith(types);
                    dependencies.Add(cache.GetCacheEntry(objectId));
                }
            }
            dependencies.UnionWith(uniqueTypes.Select(cache.GetCacheEntry));
        }
    }
}
