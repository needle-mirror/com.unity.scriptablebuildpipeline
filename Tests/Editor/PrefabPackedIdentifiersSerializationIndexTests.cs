using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Experimental;
using UnityEngine;

namespace UnityEditor.Build.Pipeline.Tests
{
    /// <summary>
    /// Mirrors the IN-104030 repro idea: one texture worth of "letter" subsprites (here A–F) and one
    /// texture worth of "digit" subsprites (0–9), modeled as <see cref="ObjectIdentifier"/>s that share
    /// guid+path per atlas. Contiguous bundles require every object from the same source asset to share
    /// the same asset-derived prefix of the serialization index (length depends on Prefab Packed Header Size; UUM-131143).
    /// </summary>
    /// <remarks>
    /// This file includes both <c>UnityEditor</c> and <c>UnityEngine</c> so the <c>GUID</c> type resolves across Unity
    /// versions (it may live in either assembly). Do not drop one of those usings as redundant cleanup.
    /// </remarks>
    [TestFixture]
    public class PrefabPackedIdentifiersSerializationIndexTests
    {
        // Same layout spirit as IN-104030: two atlases, different guids.
        static readonly GUID kLetterAtlasGuid = new GUID("10385bcdad8d47fda22f5921a3185d58");
        const string kLetterAtlasPath = "Assets/Repro/letters.png";

        static readonly GUID kDigitAtlasGuid = new GUID("20385bcdad8d47fda22f5921a3185d59");
        const string kDigitAtlasPath = "Assets/Repro/numbers.png";

        static readonly GUID kExtraArtifactGuid = new GUID("30385bcdad8d47fda22f5921a3185d5a");

        const BindingFlags kObjectIdentifierFieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly string[] kObjectIdentifierReflectFieldNames =
        {
            "m_GUID", "m_LocalIdentifierInFile", "m_FileType", "m_FilePath",
        };

        static PrefabPackedIdentifiersSerializationIndexTests()
        {
            AssertObjectIdentifierReflectionLayout();
        }

        static void AssertObjectIdentifierReflectionLayout()
        {
            var type = typeof(ObjectIdentifier);
            var missing = new List<string>();
            foreach (string name in kObjectIdentifierReflectFieldNames)
            {
                if (type.GetField(name, kObjectIdentifierFieldFlags) == null)
                    missing.Add(name);
            }

            if (missing.Count != 0)
            {
                Assert.Fail(
                    $"UnityEngine.ObjectIdentifier private fields missing or renamed: {string.Join(", ", missing)}. " +
                    $"Update {nameof(MakeObjectId)} for this Unity version.");
            }
        }

        /// <summary>Sprite names A–F; local ids are arbitrary but stable and unique per subsprite.</summary>
        static readonly (char Label, long LocalId)[] kLetterSprites =
        {
            ('A', 1001), ('B', 1002), ('C', 1003), ('D', 1004), ('E', 1005), ('F', 1006),
        };

        /// <summary>Sprite names 0–9; local ids arbitrary stable per subsprite.</summary>
        static readonly (char Label, long LocalId)[] kDigitSprites =
        {
            ('0', 2000), ('1', 2001), ('2', 2002), ('3', 2003), ('4', 2004),
            ('5', 2005), ('6', 2006), ('7', 2007), ('8', 2008), ('9', 2009),
        };

        static ObjectIdentifier MakeObjectId(GUID guid, long localIdentifierInFile, FileType fileType, string filePath)
        {
            var objectId = new ObjectIdentifier();
            var boxed = (object)objectId;
            var type = typeof(ObjectIdentifier);
            type.GetField("m_GUID", kObjectIdentifierFieldFlags).SetValue(boxed, guid);
            type.GetField("m_LocalIdentifierInFile", kObjectIdentifierFieldFlags).SetValue(boxed, localIdentifierInFile);
            type.GetField("m_FileType", kObjectIdentifierFieldFlags).SetValue(boxed, fileType);
            type.GetField("m_FilePath", kObjectIdentifierFieldFlags).SetValue(boxed, filePath);
            return (ObjectIdentifier)boxed;
        }

        static ObjectIdentifier LetterObject(long localId) =>
            MakeObjectId(kLetterAtlasGuid, localId, FileType.SerializedAssetType, kLetterAtlasPath);

        static ObjectIdentifier DigitObject(long localId) =>
            MakeObjectId(kDigitAtlasGuid, localId, FileType.SerializedAssetType, kDigitAtlasPath);

        static ObjectIdentifier ExtraArtifactObject(string forwardSlashPath, long localId) =>
            MakeObjectId(kExtraArtifactGuid, localId, FileType.SerializedAssetType, forwardSlashPath);

        static void PushSbpState(out bool prevV2Hasher, out int prevSeed, out int prevHeader, int headerSize, int hashSeed = 0)
        {
#if UNITY_2020_1_OR_NEWER
            prevV2Hasher = ScriptableBuildPipeline.useV2Hasher;
            ScriptableBuildPipeline.useV2Hasher = false;
#else
            prevV2Hasher = false;
#endif
            prevSeed = ScriptableBuildPipeline.fileIDHashSeed;
            ScriptableBuildPipeline.fileIDHashSeed = hashSeed;
            prevHeader = ScriptableBuildPipeline.prefabPackedHeaderSize;
            ScriptableBuildPipeline.prefabPackedHeaderSize = headerSize;
        }

        static void PopSbpState(bool prevV2Hasher, int prevSeed, int prevHeader)
        {
#if UNITY_2020_1_OR_NEWER
            ScriptableBuildPipeline.useV2Hasher = prevV2Hasher;
#endif
            ScriptableBuildPipeline.fileIDHashSeed = prevSeed;
            ScriptableBuildPipeline.prefabPackedHeaderSize = prevHeader;
        }

        /// <summary>
        /// Verifies that two subsprites (A and F) from the same letter atlas share the same asset-derived prefix
        /// in their serialization indices, as required for contiguous bundle packing.
        /// </summary>
        /// <param name="headerSize">The prefab packed header size to test (1-4 bytes).</param>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SerializationIndexFromObjectIdentifier_LetterAtlas_A_SharesUpperBitsWith_F(int headerSize)
        {
            PushSbpState(out bool prevV2, out int prevSeed, out int prevHeader, headerSize);
            try
            {
                var packing = new PrefabPackedIdentifiers();
                var idA = LetterObject(1001);
                var idF = LetterObject(1006);

                ulong prefixA = PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(idA), headerSize);
                ulong prefixF = PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(idF), headerSize);

                Assert.AreEqual(prefixA, prefixF,
                    $"Letter atlas: subsprites A and F must share the asset-only prefix (headerSize={headerSize}, UUM-131143).");
            }
            finally
            {
                PopSbpState(prevV2, prevSeed, prevHeader);
            }
        }

        /// <summary>
        /// Verifies that two subsprites (0 and 9) from the same digit atlas share the same asset-derived prefix
        /// in their serialization indices, as required for contiguous bundle packing.
        /// </summary>
        /// <param name="headerSize">The prefab packed header size to test (1-4 bytes).</param>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SerializationIndexFromObjectIdentifier_DigitAtlas_0_SharesUpperBitsWith_9(int headerSize)
        {
            PushSbpState(out bool prevV2, out int prevSeed, out int prevHeader, headerSize);
            try
            {
                var packing = new PrefabPackedIdentifiers();
                var id0 = DigitObject(2000);
                var id9 = DigitObject(2009);

                ulong prefix0 = PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(id0), headerSize);
                ulong prefix9 = PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(id9), headerSize);

                Assert.AreEqual(prefix0, prefix9,
                    $"Digit atlas: subsprites 0 and 9 must share the asset-only prefix (headerSize={headerSize}, UUM-131143).");
            }
            finally
            {
                PopSbpState(prevV2, prevSeed, prevHeader);
            }
        }

        /// <summary>
        /// Verifies that all letter subsprites (A through F) from the same atlas share one common asset-derived prefix,
        /// ensuring objects from the same source asset can be packed contiguously in bundles.
        /// </summary>
        // Previously: SerializationIndexFromObjectIdentifier_AllLetterSubsprites_ShareOneUpperUInt32
        [Test]
        public void SerializationIndexFromObjectIdentifier_AllLetterSubsprites_ShareOneAssetPrefix()
        {
            const int headerSize = 2;
            PushSbpState(out bool prevV2, out int prevSeed, out int prevHeader, headerSize);
            try
            {
                var packing = new PrefabPackedIdentifiers();
                var prefixes = new List<ulong>(kLetterSprites.Length);
                foreach (var (label, localId) in kLetterSprites)
                {
                    var id = LetterObject(localId);
                    prefixes.Add(PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(id), headerSize));
                }

                ulong first = prefixes[0];
                for (int i = 1; i < prefixes.Count; i++)
                {
                    Assert.AreEqual(first, prefixes[i],
                        $"Letter atlas: subsprite {kLetterSprites[i].Label} should match A's asset prefix (UUM-131143).");
                }
            }
            finally
            {
                PopSbpState(prevV2, prevSeed, prevHeader);
            }
        }

        /// <summary>
        /// Verifies that all digit subsprites (0 through 9) from the same atlas share one common asset-derived prefix,
        /// ensuring objects from the same source asset can be packed contiguously in bundles.
        /// </summary>
        // Previously: SerializationIndexFromObjectIdentifier_AllDigitSubsprites_ShareOneUpperUInt32
        [Test]
        public void SerializationIndexFromObjectIdentifier_AllDigitSubsprites_ShareOneAssetPrefix()
        {
            const int headerSize = 2;
            PushSbpState(out bool prevV2, out int prevSeed, out int prevHeader, headerSize);
            try
            {
                var packing = new PrefabPackedIdentifiers();
                var prefixes = new List<ulong>(kDigitSprites.Length);
                foreach (var (label, localId) in kDigitSprites)
                {
                    var id = DigitObject(localId);
                    prefixes.Add(PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(id), headerSize));
                }

                ulong first = prefixes[0];
                for (int i = 1; i < prefixes.Count; i++)
                {
                    Assert.AreEqual(first, prefixes[i],
                        $"Digit atlas: subsprite {kDigitSprites[i].Label} should match 0's asset prefix (UUM-131143).");
                }
            }
            finally
            {
                PopSbpState(prevV2, prevSeed, prevHeader);
            }
        }

        /// <summary>
        /// Verifies that subsprites from different atlases (letter vs digit) have different asset-derived prefixes,
        /// ensuring objects from different source assets are distinguishable in their serialization indices.
        /// </summary>
        // Previously: SerializationIndexFromObjectIdentifier_LetterAtlasAndDigitAtlas_DifferInUpperUInt32
        [Test]
        public void SerializationIndexFromObjectIdentifier_LetterAtlasAndDigitAtlas_DifferInAssetPrefix()
        {
            const int headerSize = 2;
            PushSbpState(out bool prevV2, out int prevSeed, out int prevHeader, headerSize);
            try
            {
                var packing = new PrefabPackedIdentifiers();
                long letterIndex = packing.SerializationIndexFromObjectIdentifier(LetterObject(1003));
                long digitIndex = packing.SerializationIndexFromObjectIdentifier(DigitObject(2005));

                Assert.AreNotEqual(
                    PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(letterIndex, headerSize),
                    PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(digitIndex, headerSize),
                    "Letter atlas vs digit atlas should not share the same asset-only prefix.");
                Assert.AreNotEqual(letterIndex, digitIndex, "Distinct subsprites should not collide on the full index.");
            }
            finally
            {
                PopSbpState(prevV2, prevSeed, prevHeader);
            }
        }

        /// <summary>
        /// Verifies that letter atlas subsprites share the same asset-derived prefix even when a non-zero
        /// file ID hash seed is configured, ensuring consistent behavior across different hash seed configurations.
        /// </summary>
        /// <param name="headerSize">The prefab packed header size to test (1-4 bytes).</param>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SerializationIndexFromObjectIdentifier_NonZeroFileIdHashSeed_LetterAtlas_SharesAssetPrefix(int headerSize)
        {
            const int kNonZeroSeed = 42;
            PushSbpState(out bool prevV2, out int prevSeed, out int prevHeader, headerSize, kNonZeroSeed);
            try
            {
                var packing = new PrefabPackedIdentifiers();
                var idA = LetterObject(1001);
                var idF = LetterObject(1006);

                ulong prefixA = PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(idA), headerSize);
                ulong prefixF = PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(idF), headerSize);

                Assert.AreEqual(prefixA, prefixF,
                    $"Letter atlas with fileIDHashSeed={kNonZeroSeed}: A and F must share asset-only prefix (headerSize={headerSize}).");
            }
            finally
            {
                PopSbpState(prevV2, prevSeed, prevHeader);
            }
        }

        /// <summary>
        /// Verifies that letter and digit atlases have different asset-derived prefixes when a non-zero
        /// file ID hash seed is configured, ensuring distinct assets remain distinguishable.
        /// </summary>
        [Test]
        public void SerializationIndexFromObjectIdentifier_NonZeroFileIdHashSeed_LetterAndDigitAtlases_DifferInAssetPrefix()
        {
            const int headerSize = 2;
            const int kNonZeroSeed = 42;
            PushSbpState(out bool prevV2, out int prevSeed, out int prevHeader, headerSize, kNonZeroSeed);
            try
            {
                var packing = new PrefabPackedIdentifiers();
                long letterIndex = packing.SerializationIndexFromObjectIdentifier(LetterObject(1003));
                long digitIndex = packing.SerializationIndexFromObjectIdentifier(DigitObject(2005));

                Assert.AreNotEqual(
                    PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(letterIndex, headerSize),
                    PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(digitIndex, headerSize),
                    "Letter vs digit atlas should not share the same asset-only prefix when fileIDHashSeed is non-zero.");
            }
            finally
            {
                PopSbpState(prevV2, prevSeed, prevHeader);
            }
        }

        /// <summary>
        /// Tests serialization index behavior for objects located in VirtualArtifacts/Extra paths,
        /// verifying that virtual artifacts follow the same asset prefix rules as regular assets.
        /// </summary>
        [TestFixture]
        public sealed class VirtualArtifactsExtraSerializationIndex
        {
            const string kTildeFixtureFileA = "~extra_a.uum131143_virt";
            const string kTildeFixtureFileB = "~extra_b.uum131143_virt";

            const string kStagingAssetPathA = "Packages/com.unity.scriptablebuildpipeline/Tests/Editor/Fixtures/sbp_uum131143_staging_a.uum131143_virt";
            const string kStagingAssetPathB = "Packages/com.unity.scriptablebuildpipeline/Tests/Editor/Fixtures/sbp_uum131143_staging_b.uum131143_virt";

            const string kFixturesVirtualRoot = "Packages/com.unity.scriptablebuildpipeline/Tests/Editor/Fixtures";

            static string s_virtualExtraPathA;
            static string s_virtualExtraPathB;

            static string FixturesPhysicalDirectory =>
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "com.unity.scriptablebuildpipeline", "Tests", "Editor", "Fixtures"));

            /// <summary>
            /// Produces VirtualArtifacts/Extra paths for testing by deploying staging fixtures and forcing artifact production.
            /// </summary>
            [OneTimeSetUp]
            public static void ProduceExtraArtifactPaths()
            {
                DeployStagingFixturesFromTildeSources();
                s_virtualExtraPathA = ProduceOneVirtualArtifactsExtraPath(kStagingAssetPathA);
                s_virtualExtraPathB = ProduceOneVirtualArtifactsExtraPath(kStagingAssetPathB);
                Assert.AreNotEqual(s_virtualExtraPathA, s_virtualExtraPathB,
                    "Two preview artifacts must resolve to distinct VirtualArtifacts/Extra paths.");
            }

            /// <summary>
            /// Removes staging fixtures created during test setup to clean up the asset database.
            /// </summary>
            [OneTimeTearDown]
            public static void RemoveStagingFixtures()
            {
                AssetDatabase.DeleteAsset(kStagingAssetPathA);
                AssetDatabase.DeleteAsset(kStagingAssetPathB);
            }

            static void DeployStagingFixturesFromTildeSources()
            {
                string dir = FixturesPhysicalDirectory;
                string srcA = Path.Combine(dir, kTildeFixtureFileA);
                string srcB = Path.Combine(dir, kTildeFixtureFileB);
                if (!File.Exists(srcA))
                    throw new IOException($"Missing tilde fixture: {srcA}");
                if (!File.Exists(srcB))
                    throw new IOException($"Missing tilde fixture: {srcB}");

                // File.Copy does not create .meta; under Packages/ Unity ignores assets without a sibling .meta,
                // so these scripted-importer fixtures never import and ForceProduceArtifact fails in CI.
                string srcAssetPathA = $"{kFixturesVirtualRoot}/{kTildeFixtureFileA}";
                string srcAssetPathB = $"{kFixturesVirtualRoot}/{kTildeFixtureFileB}";
                RemovePhysicalAssetIfMissingMeta(Path.Combine(dir, Path.GetFileName(kStagingAssetPathA)));
                RemovePhysicalAssetIfMissingMeta(Path.Combine(dir, Path.GetFileName(kStagingAssetPathB)));
                CopyStagingFixtureViaAssetDatabase(srcAssetPathA, kStagingAssetPathA);
                CopyStagingFixtureViaAssetDatabase(srcAssetPathB, kStagingAssetPathB);

                AssetDatabase.ImportAsset(kStagingAssetPathA, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(kStagingAssetPathB, ImportAssetOptions.ForceUpdate);
            }

            static void RemovePhysicalAssetIfMissingMeta(string physicalAssetPath)
            {
                if (!File.Exists(physicalAssetPath))
                    return;
                if (File.Exists(physicalAssetPath + ".meta"))
                    return;
                File.Delete(physicalAssetPath);
            }

            static void CopyStagingFixtureViaAssetDatabase(string srcUnityPath, string dstUnityPath)
            {
                if (AssetDatabase.LoadMainAssetAtPath(dstUnityPath) != null)
                    AssetDatabase.DeleteAsset(dstUnityPath);
                if (!AssetDatabase.CopyAsset(srcUnityPath, dstUnityPath))
                    throw new IOException($"AssetDatabase.CopyAsset failed: {srcUnityPath} -> {dstUnityPath}");
            }

            static string ProduceOneVirtualArtifactsExtraPath(string assetPath)
            {
                GUID guid = AssetDatabase.GUIDFromAssetPath(assetPath);
                Assert.False(guid.Empty(), $"Missing test asset (expected in this package): {assetPath}");

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                var key = new ArtifactKey(guid, typeof(VirtualArtifactSerializationIndexTestImporter));
                var artifactId = AssetDatabaseExperimental.ForceProduceArtifact(key);
                Assert.IsTrue(artifactId.isValid, "ForceProduceArtifact should yield a valid ArtifactID.");

                Assert.IsTrue(AssetDatabaseExperimental.GetArtifactPaths(artifactId, out string[] paths),
                    "GetArtifactPaths should succeed after production.");
                Assert.IsNotNull(paths);
                Assert.IsNotEmpty(paths, "ScriptedImporter artifact should list at least one produced path.");

                foreach (string p in paths)
                {
                    if (p.Replace('\\', '/').StartsWith("VirtualArtifacts/Extra/", StringComparison.Ordinal))
                        return p;
                }

                Assert.Fail($"Expected at least one path under VirtualArtifacts/Extra/ for {assetPath}. Got: {string.Join(", ", paths)}");
                return null;
            }

            /// <summary>
            /// Verifies that objects with different local IDs from the same VirtualArtifacts/Extra file
            /// share the same asset-derived prefix in their serialization indices.
            /// </summary>
            /// <param name="fileIdHashSeed">The file ID hash seed to use (0 or non-zero).</param>
            [TestCase(0)]
            [TestCase(42)]
            public void SerializationIndexFromObjectIdentifier_VirtualArtifactsExtra_SameFileDifferentLocalIds_ShareAssetPrefix(int fileIdHashSeed)
            {
                const int headerSize = 2;
                PushSbpState(out bool prevV2, out int prevSeed, out int prevHeader, headerSize, fileIdHashSeed);
                try
                {
                    var packing = new PrefabPackedIdentifiers();
                    var id1 = ExtraArtifactObject(s_virtualExtraPathA, 3001);
                    var id2 = ExtraArtifactObject(s_virtualExtraPathA, 3002);

                    ulong p1 = PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(id1), headerSize);
                    ulong p2 = PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(packing.SerializationIndexFromObjectIdentifier(id2), headerSize);

                    Assert.AreEqual(p1, p2,
                        $"VirtualArtifacts/Extra same file: local ids must share asset-only prefix (fileIDHashSeed={fileIdHashSeed}).");
                }
                finally
                {
                    PopSbpState(prevV2, prevSeed, prevHeader);
                }
            }

            /// <summary>
            /// Verifies that objects from different VirtualArtifacts/Extra files have different asset-derived prefixes,
            /// ensuring distinct virtual artifacts are distinguishable.
            /// </summary>
            /// <param name="fileIdHashSeed">The file ID hash seed to use (0 or non-zero).</param>
            [TestCase(0)]
            [TestCase(42)]
            public void SerializationIndexFromObjectIdentifier_VirtualArtifactsExtra_DifferentFiles_DifferInAssetPrefix(int fileIdHashSeed)
            {
                const int headerSize = 2;
                PushSbpState(out bool prevV2, out int prevSeed, out int prevHeader, headerSize, fileIdHashSeed);
                try
                {
                    var packing = new PrefabPackedIdentifiers();
                    long idxA = packing.SerializationIndexFromObjectIdentifier(ExtraArtifactObject(s_virtualExtraPathA, 3001));
                    long idxB = packing.SerializationIndexFromObjectIdentifier(ExtraArtifactObject(s_virtualExtraPathB, 3002));

                    Assert.AreNotEqual(
                        PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(idxA, headerSize),
                        PrefabPackedSerializationIndexTestUtil.AssetOnlyPrefix(idxB, headerSize),
                        $"Different VirtualArtifacts/Extra files should not share asset-only prefix (fileIDHashSeed={fileIdHashSeed}).");
                }
                finally
                {
                    PopSbpState(prevV2, prevSeed, prevHeader);
                }
            }
        }
    }
}
