using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;

namespace UnityEditor.Build.Pipeline.Tests
{
    [TestFixture]
    class AssetLoadInfoSortingTests
    {
        const string k_TestAsset = "Packages/com.unity.scriptablebuildpipeline/Tests/Editor/TestAssets/SpriteTexture32x32.png";

        static AssetLoadInfo GetTestAssetLoadInfo()
        {
            GUID asset = new GUID(AssetDatabase.AssetPathToGUID(k_TestAsset));
            ObjectIdentifier[] oId = ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(asset, EditorUserBuildSettings.activeBuildTarget);
            AssetLoadInfo loadInfo = new AssetLoadInfo()
            {
                asset = asset,
                address = k_TestAsset,
                includedObjects = oId.ToList(),
                referencedObjects = new List<ObjectIdentifier>()
            };
            return loadInfo;
        }

        static List<AssetLoadInfo> GetTestAssetLoadInfoList()
        {
            List<AssetLoadInfo> testList = new List<AssetLoadInfo>
            {
                GetTestAssetLoadInfo(),
                GetTestAssetLoadInfo()
            };
            return testList;
        }

        [Test]
        public void AssetLoadInfo_SortsByGuid()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();
            testList[1].asset = new GUID();

            var asset0 = testList[0].asset;
            var asset1 = testList[1].asset;

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.AreEqual(asset1, testList[0].asset);
            Assert.AreEqual(asset0, testList[1].asset);
        }

        [Test]
        public void AssetLoadInfo_SortsByGuid_PreSorted()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();
            testList[0].asset = new GUID();

            var asset0 = testList[0].asset;
            var asset1 = testList[1].asset;

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.AreEqual(asset0, testList[0].asset);
            Assert.AreEqual(asset1, testList[1].asset);
        }

        [Test]
        public void AssetLoadInfo_SortsByIncludedObjects()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();

            // Verify which one we should swap to ensure current ordering is different from expected ordering.
            if (testList[0].includedObjects[0].localIdentifierInFile < testList[0].includedObjects[1].localIdentifierInFile)
                testList[0].includedObjects.Swap(0, 1);
            else
                testList[1].includedObjects.Swap(0, 1);

            var object0 = testList[0].includedObjects[0];
            var object1 = testList[1].includedObjects[0];

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.AreEqual(object1, testList[0].includedObjects[0]);
            Assert.AreEqual(object0, testList[1].includedObjects[0]);
        }

        [Test]
        public void AssetLoadInfo_SortsByIncludedObjects_PreSorted()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();

            // Verify which one we should swap to ensure current ordering is different from expected ordering.
            if (testList[0].includedObjects[0].localIdentifierInFile < testList[0].includedObjects[1].localIdentifierInFile)
                testList[1].includedObjects.Swap(0, 1);
            else
                testList[0].includedObjects.Swap(0, 1);

            var object0 = testList[0].includedObjects[0];
            var object1 = testList[1].includedObjects[0];

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.AreEqual(object0, testList[0].includedObjects[0]);
            Assert.AreEqual(object1, testList[1].includedObjects[0]);
        }

        [Test]
        public void AssetLoadInfo_SortsByIncludedObjects_SortsNullIncludedObjects()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();
            testList[1].includedObjects = null;

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.IsNull(testList[0].includedObjects);
            Assert.IsNotNull(testList[1].includedObjects);
        }

        [Test]
        public void AssetLoadInfo_SortsByIncludedObjects_SortsNullIncludedObjects_PreSorted()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();
            testList[0].includedObjects = null;

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.IsNull(testList[0].includedObjects);
            Assert.IsNotNull(testList[1].includedObjects);
        }

        [Test]
        public void AssetLoadInfo_SortsByAddress()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();
            testList[1].address = "A/short/path";

            var address0 = testList[0].address;
            var address1 = testList[1].address;

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.AreEqual(address1, testList[0].address);
            Assert.AreEqual(address0, testList[1].address);
        }

        [Test]
        public void AssetLoadInfo_SortsByAddress_PreSorted()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();
            testList[0].address = "A/short/path";

            var address0 = testList[0].address;
            var address1 = testList[1].address;

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.AreEqual(address0, testList[0].address);
            Assert.AreEqual(address1, testList[1].address);
        }

        [Test]
        public void AssetLoadInfo_SortsByAddress_SortsNullAdress()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();
            testList[1].address = null;

            var address0 = testList[0].address;
            var address1 = testList[1].address;

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.AreEqual(address1, testList[0].address);
            Assert.AreEqual(address0, testList[1].address);
        }

        [Test]
        public void AssetLoadInfo_SortsByAddress_SortsNullAdress_PreSorted()
        {
            List<AssetLoadInfo> testList = GetTestAssetLoadInfoList();
            testList[0].address = null;

            var address0 = testList[0].address;
            var address1 = testList[1].address;

            testList.Sort(GenerateBundleCommands.AssetLoadInfoCompare);

            Assert.AreEqual(address0, testList[0].address);
            Assert.AreEqual(address1, testList[1].address);
        }
    }
}
