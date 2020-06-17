using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEngine;

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

        public static Hash128 GetHash128(this BuildSettings settings)
        {
            if (settings.typeDB == null)
                return HashingMethods.Calculate(settings.target, settings.group, settings.buildFlags).ToHash128();
            return HashingMethods.Calculate(settings.target, settings.group, settings.buildFlags, settings.typeDB.GetHash128()).ToHash128();
        }
    }
}
