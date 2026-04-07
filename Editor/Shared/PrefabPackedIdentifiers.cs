using System;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;

namespace UnityEditor.Build.Pipeline
{
    /// <summary>
    /// Generates a deterministic identifier using a MD5 hash algorithm and does not require object ordering to be deterministic.
    /// This algorithm ensures objects coming from the same asset are packed closer together and can improve loading performance under certain situations.
    /// </summary>
    public class PrefabPackedIdentifiers : IDeterministicIdentifiers
    {
        /// <inheritdoc />
        public virtual string GenerateInternalFileName(string name)
        {
            return "CAB-" + HashingMethods.Calculate(name);
        }

        /// <inheritdoc />
        public virtual long SerializationIndexFromObjectIdentifier(ObjectIdentifier objectID)
        {
            byte[] assetHash;
            byte[] objectHash;
            bool extraArtifact = HashingMethods.IsVirtualArtifactsExtraPath(objectID.filePath)
                || HashingMethods.IsUdsDataPath(objectID.filePath);
            int hashSeed = ScriptableBuildPipeline.fileIDHashSeed;
            if (extraArtifact && hashSeed != 0)
            {
                RawHash fileHash = HashingMethods.CalculateFile(objectID.filePath);
                assetHash = HashingMethods.Calculate(hashSeed, fileHash).ToBytes();
                objectHash = HashingMethods.Calculate(hashSeed, fileHash, objectID.localIdentifierInFile).ToBytes();
            }
            else if (extraArtifact)
            {
                RawHash fileHash = HashingMethods.CalculateFile(objectID.filePath);
                assetHash = fileHash.ToBytes();
                objectHash = HashingMethods.Calculate(fileHash, objectID.localIdentifierInFile).ToBytes();
            }
            else if (hashSeed != 0)
            {
                assetHash = HashingMethods.Calculate(hashSeed, objectID.guid, objectID.filePath).ToBytes();
                objectHash = HashingMethods.Calculate(hashSeed, objectID).ToBytes();
            }
            else
            {
                assetHash = HashingMethods.Calculate(objectID.guid, objectID.filePath).ToBytes();
                objectHash = HashingMethods.Calculate(objectID).ToBytes();
            }

            int headerSize = ScriptableBuildPipeline.prefabPackedHeaderSize;
            if (headerSize == 0)
                throw new ArgumentOutOfRangeException(nameof(headerSize), headerSize, "Prefab packed header size must be non-zero; zero breaks serialization index masking (64-bit shift / low mask).");

            ulong assetVal = BitConverter.ToUInt64(assetHash, 0);
            ulong objectVal = BitConverter.ToUInt64(objectHash, 0);
            ulong mask = 0xFFFFFFFFFFFFFFFF;
            mask = mask >> headerSize * 8;
            return (long)((~mask & assetVal) | (mask & (objectVal ^ assetVal)));
        }
    }
}
