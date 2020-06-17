using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Utilities;

namespace UnityEditor.Build.Pipeline.Tests
{
    class GenerateBundlePackingTests
    {
        IDependencyData GetDependencyData(List<ObjectIdentifier> objects, params GUID[] guids)
        {
            IDependencyData dep = new BuildDependencyData();
            for (int i = 0; i < guids.Length; i++)
            {
                AssetLoadInfo loadInfo = new AssetLoadInfo()
                {
                    asset = guids[i],
                    address = $"path{i}",
                    includedObjects = objects,
                    referencedObjects = objects
                };
                dep.AssetInfo.Add(guids[i], loadInfo);
            }
            return dep;
        }

        List<ObjectIdentifier> CreateObjectIdentifierList(string path, params GUID[] guids)
        {
            var objects = new List<ObjectIdentifier>();
            foreach (GUID guid in guids)
            {
                var obj = new ObjectIdentifier();
                obj.SetObjectIdentifier(guid, 0, FileType.SerializedAssetType, path);
                objects.Add(obj);
            }
            return objects;
        }

        [Test]
        public void WhenReferencesAreUnique_FilterReferencesForAsset_ReturnsReferences()
        {
            var assetInBundle = new GUID("00000000000000000000000000000000");
            List<ObjectIdentifier> objects = CreateObjectIdentifierList("path", assetInBundle, assetInBundle);
            IDependencyData dep = GetDependencyData(objects, assetInBundle);

            var references = new List<ObjectIdentifier>(objects);
            List<GUID> results = GenerateBundlePacking.FilterReferencesForAsset(dep, assetInBundle, references);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(assetInBundle, results[0]);
        }

        [Test]
        public void WhenReferencesContainsDefaultResources_FilterReferencesForAsset_PrunesDefaultResources()
        {
            var assetInBundle = new GUID("00000000000000000000000000000000");
            List<ObjectIdentifier> objects = CreateObjectIdentifierList(CommonStrings.UnityDefaultResourcePath, assetInBundle);
            IDependencyData dep = GetDependencyData(objects, assetInBundle);

            var references = new List<ObjectIdentifier>(objects);
            GenerateBundlePacking.FilterReferencesForAsset(dep, assetInBundle, references);
            Assert.AreEqual(0, references.Count);
        }

        [Test]
        public void WhenReferencesContainsAssetsInBundles_FilterReferencesForAsset_PrunesAssetsInBundles()
        {
            var assetInBundle = new GUID("00000000000000000000000000000000");
            List<ObjectIdentifier> objects = CreateObjectIdentifierList("path", assetInBundle);
            IDependencyData dep = GetDependencyData(objects, assetInBundle);

            var references = new List<ObjectIdentifier>(objects);
            GenerateBundlePacking.FilterReferencesForAsset(dep, assetInBundle, references);
            Assert.AreEqual(0, references.Count);
        }

        [Test]
        public void WhenReferencesDoesNotContainAssetsInBundles_FilterReferences_PrunesNothingAndReturnsNothing()
        {
            var assetInBundle = new GUID("00000000000000000000000000000000");
            List<ObjectIdentifier> objects = CreateObjectIdentifierList("path", assetInBundle);
            IDependencyData dep = new BuildDependencyData();

            var references = new List<ObjectIdentifier>(objects);
            List<GUID> results = GenerateBundlePacking.FilterReferencesForAsset(dep, assetInBundle, references);
            Assert.AreEqual(1, references.Count);
            Assert.AreEqual(assetInBundle, references[0].guid);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void WhenReferencesContainsRefsIncludedByNonCircularAssets_FilterReferencesForAsset_PrunesRefsIncludedByNonCircularAssets()
        {
            var assetNotInBundle = new GUID("00000000000000000000000000000000");
            var referenceInBundle = new GUID("00000000000000000000000000000001");
            var referenceNotInBundle = new GUID("00000000000000000000000000000002");
            List<ObjectIdentifier> objects = CreateObjectIdentifierList("path", referenceNotInBundle);
            IDependencyData dep = GetDependencyData(objects, referenceInBundle);

            List<ObjectIdentifier> references = CreateObjectIdentifierList("path", referenceInBundle, referenceNotInBundle);
            GenerateBundlePacking.FilterReferencesForAsset(dep, assetNotInBundle, references);
            Assert.AreEqual(0, references.Count);
        }

        [Test]
        public void WhenReferencesContainsRefsIncludedByCircularAssetsWithLowerGuid_FilterReferencesForAsset_PrunesRefsIncludedByCircularAssetsWithLowerGuid()
        {
            var assetNotInBundle = new GUID("00000000000000000000000000000001");
            var referenceInBundle = new GUID("00000000000000000000000000000000");
            List<ObjectIdentifier> objects = CreateObjectIdentifierList("path", assetNotInBundle); // circular reference to asset whose references we want to filter
            IDependencyData dep = GetDependencyData(objects, referenceInBundle);

            List<ObjectIdentifier> references = CreateObjectIdentifierList("path", referenceInBundle, assetNotInBundle);
            GenerateBundlePacking.FilterReferencesForAsset(dep, assetNotInBundle, references);
            Assert.AreEqual(0, references.Count);
        }

        [Test]
        public void WhenReferencesContainsRefsIncludedByCircularAssetsWithHigherGuid_FilterReferencesForAsset_DoesNotPruneRefsIncludedByCircularAssetsWithHigherGuid()
        {
            var assetNotInBundle = new GUID("00000000000000000000000000000000");
            var referenceInBundle = new GUID("00000000000000000000000000000001");
            List<ObjectIdentifier> objects = CreateObjectIdentifierList("path", assetNotInBundle); // circular reference to asset whose references we want to filter
            IDependencyData dep = GetDependencyData(objects, referenceInBundle);

            List<ObjectIdentifier> references = CreateObjectIdentifierList("path", referenceInBundle, assetNotInBundle);
            GenerateBundlePacking.FilterReferencesForAsset(dep, assetNotInBundle, references);
            Assert.AreEqual(1, references.Count);
            Assert.AreEqual(assetNotInBundle, references[0].guid);
        }

        [Test]
        public void WhenReferencesContainsPreviousSceneObjects_FilterReferencesForAsset_PrunesPreviousSceneObjects()
        {
            var assetInBundle = new GUID("00000000000000000000000000000001");
            var referenceNotInBundle = new GUID("00000000000000000000000000000000");
            List<ObjectIdentifier> objects = CreateObjectIdentifierList("path", referenceNotInBundle);
            IDependencyData dep = GetDependencyData(objects, assetInBundle);

            var references = new List<ObjectIdentifier>(objects);
            GenerateBundlePacking.FilterReferencesForAsset(dep, assetInBundle, references, new HashSet<ObjectIdentifier>() { objects[0] });
            Assert.AreEqual(0, references.Count);
        }
    }
}
