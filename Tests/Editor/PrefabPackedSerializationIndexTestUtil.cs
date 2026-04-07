namespace UnityEditor.Build.Pipeline.Tests
{
    /// <summary>
    /// Asset-derived prefix of a Prefab Packed serialization index, aligned with
    /// <see cref="PrefabPackedIdentifiers"/> (UUM-131143).
    /// Casting <c>(ulong)serializationIndex</c> preserves the bit pattern when the index is negative as <c>long</c>,
    /// matching UInt64 sort order used for contiguous bundles.
    /// </summary>
    internal static class PrefabPackedSerializationIndexTestUtil
    {
        internal static int AssetBitCount(int headerSize) => headerSize >= 4 ? 32 : headerSize * 8;

        internal static ulong AssetOnlyPrefix(long serializationIndex, int headerSize) =>
            ((ulong)serializationIndex) >> (64 - AssetBitCount(headerSize));

        /// <summary>Int key for cluster bucketing in batching stress tests (see UUM-131143).</summary>
        internal static int ToClusterDictionaryKey(long serializationIndex, int headerSize) =>
            unchecked((int)(uint)(AssetOnlyPrefix(serializationIndex, headerSize) & 0xFFFFFFFF));
    }
}
