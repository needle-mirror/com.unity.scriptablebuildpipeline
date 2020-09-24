using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Pipeline.WriteTypes;
using UnityEditor.Build.Player;
using UnityEngine;
using static UnityEditor.Build.Pipeline.Utilities.BuildLog;

namespace UnityEditor.Build.Pipeline.Tests
{
    public class WriteSerializedFileTests
    {
        class TestBuildParameters : TestBuildParametersBase
        {
            public override bool UseCache { get; set; }
            public override string TempOutputFolder { get; set; }

            internal BuildSettings TestBuildSettings;
            public override BuildSettings GetContentBuildSettings()
            {
                return TestBuildSettings;
            }
        }

        class TestDependencyData : TestDependencyDataBase
        {
            public Dictionary<GUID, SceneDependencyInfo> TestSceneInfo = new Dictionary<GUID, SceneDependencyInfo>();
            public override Dictionary<GUID, SceneDependencyInfo> SceneInfo => TestSceneInfo;
            public override BuildUsageTagGlobal GlobalUsage => default(BuildUsageTagGlobal);
        }

        class TestWriteData : TestWriteDataBase
        {
            internal List<IWriteOperation> TestOps = new List<IWriteOperation>();
            public override List<IWriteOperation> WriteOperations => TestOps;
        }

        class TestBuildResults : TestBuildResultsBase
        {
            Dictionary<string, WriteResult> m_Results = new Dictionary<string, WriteResult>();
            Dictionary<string, SerializedFileMetaData> m_MetaData = new Dictionary<string, SerializedFileMetaData>();

            public override Dictionary<string, WriteResult> WriteResults => m_Results;

            public override Dictionary<string, SerializedFileMetaData> WriteResultsMetaData => m_MetaData;
        }

        class TestWriteOperation : IWriteOperation
        {
            internal int TestWriteCount;
            public bool OutputSerializedFile = false;
            public bool WriteBundle = false;
            public WriteCommand TestCommand;
            public WriteCommand Command { get => TestCommand; set => throw new System.NotImplementedException(); }
            public BuildUsageTagSet UsageSet { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
            public BuildReferenceMap ReferenceMap { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
            public Hash128 DependencyHash { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            Hash128 debugHash = new Hash128();
            public void SetDebugHash(uint hash)
            {
                debugHash = new Hash128(0, 0, 0, hash);
            }

            public Hash128 GetHash128(IBuildLogger log)
            {
                return HashingMethods.Calculate(debugHash, new Hash128(0, 0, 0, (uint)QualitySettingsApi.GetNumberOfLODsStripped())).ToHash128();
            }

            public Hash128 GetHash128()
            {
                return GetHash128(null);
            }

            internal static void WriteRandomData(Stream s, long size, int seed)
            {
                System.Random r = new System.Random(seed);

                long written = 0;
                byte[] bytes = new byte[Math.Min(1 * 1024 * 1024, size)];
                while (written < size)
                {
                    r.NextBytes(bytes);
                    int writeSize = (int)Math.Min(size - written, bytes.Length);
                    s.Write(bytes, 0, writeSize);
                    written += bytes.Length;
                }
            }

            internal static void WriteRandomData(string filename, long size, int seed)
            {
                using (var s = File.Open(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    WriteRandomData(s, size, seed);
                }
            }

            internal string CreateFileOfSize(string path, long size)
            {
                System.Random r = new System.Random(0);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                WriteRandomData(path, size, 0);
                return path;
            }

            public WriteResult Write(string outputFolder, BuildSettings settings, BuildUsageTagGlobal globalUsage)
            {
#if UNITY_2019_4_OR_NEWER
                if (WriteBundle)
                {
                    return ContentBuildInterface.WriteSerializedFile(outputFolder, new WriteParameters
                    {
                        writeCommand = new WriteCommand() { fileName = "bundle", internalName = "bundle" },
                        settings = settings,
                        globalUsage = globalUsage,
                        usageSet = new BuildUsageTagSet(),
                        referenceMap = new BuildReferenceMap(),
                        bundleInfo = new AssetBundleInfo() {  bundleName = "bundle" }
                    });
                }
#endif

                string filename = Path.Combine(outputFolder, "resourceFilename");
                CreateFileOfSize(filename, 1024);
                TestWriteCount++;
                WriteResult result = new WriteResult();
                ResourceFile file = new ResourceFile();
                file.SetFileName(filename);

                file.SetSerializedFile(OutputSerializedFile);
                result.SetResourceFiles(new ResourceFile[] { file });

                if (OutputSerializedFile)
                {
                    var obj1 = new ObjectSerializedInfo();
                    SerializedLocation header = new SerializedLocation();
                    header.SetFileName(result.resourceFiles[0].fileAlias);
                    header.SetOffset(100);
                    obj1.SetHeader(header);

                    var obj2 = new ObjectSerializedInfo();
                    SerializedLocation header2 = new SerializedLocation();
                    header2.SetFileName(result.resourceFiles[0].fileAlias);
                    header2.SetOffset(200);
                    obj2.SetHeader(header2);

                    result.SetSerializedObjects(new ObjectSerializedInfo[] { obj1, obj2 });
                }

                return result;
            }
        }

        TestBuildParameters m_BuildParameters;
        TestDependencyData m_DependencyData;
        TestWriteData m_WriteData;
        TestBuildResults m_BuildResults;
        WriteSerializedFiles m_Task;
        BuildCache m_Cache;
        BuildContext m_Context;
        BuildLog m_Log;
        string m_TestTempDir;
        bool m_PreviousSlimSettings;
        bool m_PreviousStripUnusedMeshComponents;
        bool m_PreviousBakeCollisionMeshes;
        int m_PreviousQualityLevel;
        List<int> m_PreviousMaximumLODLeve = new List<int>();
#if UNITY_2020_1_OR_NEWER
        bool m_PreviousMipStripping;
        List<int> m_PreviousMasterTextureLimits = new List<int>();
#endif

        [SetUp]
        public void Setup()
        {
            m_PreviousSlimSettings = ScriptableBuildPipeline.slimWriteResults;
            ScriptableBuildPipeline.s_Settings.slimWriteResults = false;
            m_PreviousStripUnusedMeshComponents = PlayerSettings.stripUnusedMeshComponents;
            PlayerSettings.stripUnusedMeshComponents = false;
            m_PreviousBakeCollisionMeshes = PlayerSettings.bakeCollisionMeshes;
            PlayerSettings.bakeCollisionMeshes = false;
            QualitySettings_GetPrevious();
#if UNITY_2020_1_OR_NEWER
            m_PreviousMipStripping = PlayerSettings.mipStripping;
            PlayerSettings.mipStripping = false;
#endif

            BuildCache.PurgeCache(false);

            m_TestTempDir = Path.Combine("Temp", "test");
            Directory.CreateDirectory(m_TestTempDir);

            m_BuildParameters = new TestBuildParameters();
            m_BuildParameters.UseCache = true;
            m_BuildParameters.TempOutputFolder = m_TestTempDir;
            m_BuildParameters.TestBuildSettings = new BuildSettings();
            m_DependencyData = new TestDependencyData();
            m_WriteData = new TestWriteData();
            m_WriteData.TestOps = new List<IWriteOperation>();
            m_BuildResults = new TestBuildResults();
            m_Task = new WriteSerializedFiles();
            m_Cache = new BuildCache();
            m_Log = new BuildLog();

            m_Context = new BuildContext(m_BuildParameters, m_DependencyData, m_WriteData, m_BuildResults, m_Cache, m_Log);
            ContextInjector.Inject(m_Context, m_Task);
        }

        [TearDown]
        public void Teardown()
        {
            Directory.Delete(m_TestTempDir, true);
            ScriptableBuildPipeline.s_Settings.slimWriteResults = m_PreviousSlimSettings;
            PlayerSettings.stripUnusedMeshComponents = m_PreviousStripUnusedMeshComponents;
            PlayerSettings.bakeCollisionMeshes = m_PreviousBakeCollisionMeshes;
            QualitySettings_RestorePrevious();
#if UNITY_2020_1_OR_NEWER
            PlayerSettings.mipStripping = m_PreviousMipStripping;
#endif
            m_Cache.Dispose();
        }

        void QualitySettings_GetPrevious()
        {
            m_PreviousQualityLevel = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(0);

#if UNITY_2020_1_OR_NEWER
            m_PreviousMasterTextureLimits.Clear();
#endif
            m_PreviousMaximumLODLeve.Clear();

            var count = QualitySettings.names.Length;
            for (int i = 0; i < count; i++)
            {
#if UNITY_2020_1_OR_NEWER
                m_PreviousMasterTextureLimits.Add(QualitySettings.masterTextureLimit);
                QualitySettings.masterTextureLimit = 0;
#endif
                m_PreviousMaximumLODLeve.Add(QualitySettings.maximumLODLevel);
                QualitySettings.maximumLODLevel = 0;
                QualitySettings.IncreaseLevel();
            }
        }

#if UNITY_2020_1_OR_NEWER
        void SetQualitySettings_MasterTextureLimits(int limit)
        {
            QualitySettings.SetQualityLevel(0);

            for (int i = 0; i < m_PreviousMasterTextureLimits.Count; i++)
            {
                QualitySettings.masterTextureLimit = limit;
                QualitySettings.IncreaseLevel();
            }
        }

#endif
        void SetQualitySettings_MaximumLODLevel(int level)
        {
            QualitySettings.SetQualityLevel(0);

            for (int i = 0; i < m_PreviousMaximumLODLeve.Count; i++)
            {
                QualitySettings.maximumLODLevel = level;
                QualitySettings.IncreaseLevel();
            }
        }

        void QualitySettings_RestorePrevious()
        {
            QualitySettings.SetQualityLevel(0);

            var count = QualitySettings.names.Length;
            for (int i = 0; i < count; i++)
            {
#if UNITY_2020_1_OR_NEWER
                QualitySettings.masterTextureLimit = m_PreviousMasterTextureLimits[i];
#endif
                QualitySettings.maximumLODLevel = m_PreviousMaximumLODLeve[i];
                QualitySettings.IncreaseLevel();
            }
            QualitySettings.SetQualityLevel(m_PreviousQualityLevel);
        }

        TestWriteOperation AddTestOperation(string name = "testInternalName")
        {
            TestWriteOperation op = new TestWriteOperation();
            op.TestCommand = new WriteCommand();
            op.TestCommand.internalName = name;
            m_WriteData.WriteOperations.Add(op);
            return op;
        }

        public static IEnumerable RebuildTestCases
        {
            get
            {
                yield return new TestCaseData(false, new Action<WriteSerializedFileTests>((_this) => {})).SetName("NoChanges");
                yield return new TestCaseData(true, new Action<WriteSerializedFileTests>((_this) => { _this.m_BuildParameters.TestBuildSettings.buildFlags |= ContentBuildFlags.DisableWriteTypeTree; })).SetName("BuildSettings");
                yield return new TestCaseData(true, new Action<WriteSerializedFileTests>((_this) => { ((TestWriteOperation)_this.m_WriteData.WriteOperations[0]).SetDebugHash(27); })).SetName("OperationHash");
                yield return new TestCaseData(true, new Action<WriteSerializedFileTests>((_this) => { ScriptableBuildPipeline.s_Settings.slimWriteResults = true; })).SetName("SlimWriteResults");
                yield return new TestCaseData(true, new Action<WriteSerializedFileTests>((_this) => { PlayerSettings.stripUnusedMeshComponents = true; })).SetName("StripUnusedMeshComponents");
                yield return new TestCaseData(true, new Action<WriteSerializedFileTests>((_this) => { PlayerSettings.bakeCollisionMeshes = true; })).SetName("BakeCollisionMeshes");
                yield return new TestCaseData(true, new Action<WriteSerializedFileTests>((_this) => { _this.SetQualitySettings_MaximumLODLevel(1); })).SetName("LODStripping");
#if UNITY_2020_1_OR_NEWER
                yield return new TestCaseData(true, new Action<WriteSerializedFileTests>((_this) => { _this.SetQualitySettings_MasterTextureLimits(1); PlayerSettings.mipStripping = true; })).SetName("MipStripping");
#endif
            }
        }

        [Test, TestCaseSource(typeof(WriteSerializedFileTests), "RebuildTestCases")]
        public void WhenInputsChanges_OnlyChangedDependenciesTriggersRebuild(bool shouldRebuild, Action<WriteSerializedFileTests> postFirstBuildAction)
        {
            TestWriteOperation op = AddTestOperation();
            m_Task.Run();
            Assert.AreEqual(1, op.TestWriteCount);
            m_Cache.SyncPendingSaves();
            postFirstBuildAction(this);
            m_BuildResults.WriteResults.Clear();
            m_BuildResults.WriteResultsMetaData.Clear();
            m_Task.Run();
            Assert.AreEqual(shouldRebuild ? 2 : 1, op.TestWriteCount);
        }

        [Test]
        public void WhenFileHasSerializedObjects_AndSlimMode_OnlyFirstObjectInWriteResults([Values] bool slimEnabled)
        {
            TestWriteOperation op = AddTestOperation();
            op.OutputSerializedFile = true;
            ScriptableBuildPipeline.s_Settings.slimWriteResults = slimEnabled;
            m_Task.Run();
            Assert.AreEqual(1, op.TestWriteCount);
            WriteResult r = m_BuildResults.WriteResults[op.TestCommand.internalName];
            if (slimEnabled)
            {
                Assert.AreEqual(1, r.serializedObjects.Count);
                Assert.AreEqual(100, r.serializedObjects[0].header.offset);
            }
            else
            {
                Assert.AreEqual(2, r.serializedObjects.Count);
            }
        }

        [Test]
        public void WhenResourceFileIsNotASerializedFile_ContentHashIsFullFileHash()
        {
            TestWriteOperation op = AddTestOperation();

            m_Task.Run();
            Assert.AreEqual(1, op.TestWriteCount);
            SerializedFileMetaData md = m_BuildResults.WriteResultsMetaData[op.TestCommand.internalName];
            WriteResult result = m_BuildResults.WriteResults[op.TestCommand.internalName];
            Assert.AreEqual(md.RawFileHash, md.ContentHash);
        }

        [Test]
        public void WhenResourceFileIsASerializedFile_ContentHashBeginsAtFirstObject()
        {
            TestWriteOperation op = AddTestOperation();
            op.OutputSerializedFile = true;
            m_Task.Run();
            Assert.AreEqual(1, op.TestWriteCount);
            SerializedFileMetaData md = m_BuildResults.WriteResultsMetaData[op.TestCommand.internalName];
            WriteResult result = m_BuildResults.WriteResults[op.TestCommand.internalName];

            Hash128 expectedContentHash;
            using (FileStream fs = File.OpenRead(result.resourceFiles[0].fileName))
            {
                fs.Position = (long)result.serializedObjects[0].header.offset;
                expectedContentHash = HashingMethods.Calculate(new List<object>() { HashingMethods.CalculateStream(fs) }).ToHash128();
            }
            var objs = new List<object>() { HashingMethods.CalculateFile(result.resourceFiles[0].fileName) };
            Hash128 fullFileHash = HashingMethods.Calculate(objs).ToHash128();

            Assert.AreEqual(fullFileHash, md.RawFileHash);
            Assert.AreEqual(expectedContentHash, md.ContentHash);
            Assert.AreNotEqual(md.RawFileHash, md.ContentHash);
        }

        [Test]
        public void Run_CallsWriteOnOperationAndOutputsWriteResult()
        {
            TestWriteOperation op = AddTestOperation();
            ReturnCode result = m_Task.Run();
            Assert.AreEqual(1, op.TestWriteCount);
            WriteResult reportedResult = m_BuildResults.WriteResults[op.Command.internalName];
            FileAssert.Exists(reportedResult.resourceFiles[0].fileName);
        }

        [Test]
        public void Run_WithoutCache_Succeeds()
        {
            m_BuildParameters.UseCache = false;
            AddTestOperation("testOp1");
            AddTestOperation("testOp2");

            ReturnCode result = m_Task.Run();
            Assert.AreEqual(ReturnCode.Success, result);

            m_BuildParameters.UseCache = true;
        }

#if UNITY_2020_2_OR_NEWER || ENABLE_DETAILED_PROFILE_CAPTURING
        [Test]
        public void WhenWritingSerializedFilesAndUsingDetailedBuildLog_ProfileCaptureScope_CreatesLogEventsWithinTaskThreshold()
        {
            TestWriteOperation op = AddTestOperation();
            op.WriteBundle = true;
            bool useDetailedBuildLog = ScriptableBuildPipeline.useDetailedBuildLog;
            ScriptableBuildPipeline.useDetailedBuildLog = true;

            m_Task.Run();
            LogStep runCachedOp = m_Log.Root.Children.Find(x => x.Name == "RunCachedOperation");
            LogStep processEntries = runCachedOp.Children.Find(x => x.Name == "Process Entries");
            LogStep writingOp = processEntries.Children.Find(x => x.Name == "Writing TestWriteOperation");
            
            Assert.IsTrue(writingOp.Children.Count > 0);

            double taskEndTime = runCachedOp.StartTime + runCachedOp.DurationMS;
            foreach(LogStep e in writingOp.Children)
            {
                Assert.LessOrEqual(e.StartTime + e.DurationMS, taskEndTime);
            }
            ScriptableBuildPipeline.useDetailedBuildLog = useDetailedBuildLog;
        }

#endif
    }
}
