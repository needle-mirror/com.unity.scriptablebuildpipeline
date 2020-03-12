using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;

internal static class ReflectionExtentions
{
    public static void SetFileName(this ref ResourceFile file, string filename)
    {
        var fieldInfo = typeof(ResourceFile).GetField("m_FileName", System.Reflection.BindingFlags.NonPublic| System.Reflection.BindingFlags.Instance);
        object boxed = file;
        fieldInfo.SetValue(boxed, filename);
        file = (ResourceFile)boxed;
    }

    public static void SetFileAlias(this ref ResourceFile file, string fileAlias)
    {
        var fieldInfo = typeof(ResourceFile).GetField("m_FileAlias", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object boxed = file;
        fieldInfo.SetValue(boxed, fileAlias);
        file = (ResourceFile)boxed;
    }

    public static void SetSerializedFile(this ref ResourceFile file, bool serializedFile)
    {
        var fieldInfo = typeof(ResourceFile).GetField("m_SerializedFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object boxed = file;
        fieldInfo.SetValue(boxed, serializedFile);
        file = (ResourceFile)boxed;
    }

    public static void SetResourceFiles(this ref WriteResult result, ResourceFile[] resourceFiles)
    {
        var fieldInfo = typeof(WriteResult).GetField("m_ResourceFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object boxed = result;
        fieldInfo.SetValue(boxed, resourceFiles);
        result = (WriteResult)boxed;
    }

    public static void SetSerializedObjects(this ref WriteResult result, ObjectSerializedInfo[] osis)
    {
        var fieldInfo = typeof(WriteResult).GetField("m_SerializedObjects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object boxed = result;
        fieldInfo.SetValue(boxed, osis);
        result = (WriteResult)boxed;
    }

    public static void SetHeader(this ref ObjectSerializedInfo osi, SerializedLocation serializedLocation)
    {
        var fieldInfo = typeof(ObjectSerializedInfo).GetField("m_Header", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object boxed = osi;
        fieldInfo.SetValue(boxed, serializedLocation);
        osi = (ObjectSerializedInfo)boxed;
    }

    public static void SetFileName(this ref SerializedLocation serializedLocation, string filename)
    {
        var fieldInfo = typeof(SerializedLocation).GetField("m_FileName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object boxed = serializedLocation;
        fieldInfo.SetValue(boxed, filename);
        serializedLocation = (SerializedLocation)boxed;
    }

    public static void SetOffset(this ref SerializedLocation serializedLocation, ulong offset)
    {
        var fieldInfo = typeof(SerializedLocation).GetField("m_Offset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object boxed = serializedLocation;
        fieldInfo.SetValue(boxed, offset);
        serializedLocation = (SerializedLocation)boxed;
    }
}


public class ArchiveAndCompressTests
{
    static ReturnCode RunTask<T>(params IContextObject[] args) where T : IBuildTask
    {
        IBuildContext context = new BuildContext(args);
        IBuildTask instance = Activator.CreateInstance<T>();
        ContextInjector.Inject(context, instance);
        var result = instance.Run();
        ContextInjector.Extract(context, instance);
        return result;
    }

    string GetTemporaryDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    public static void WriteRandomData(string filename, long size, int seed)
    {
        System.Random r = new System.Random(seed);
        using (var s = File.Open(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite))
        {
            long written = 0;
            byte[] bytes = new byte[1 * 1024 * 1024];
            while (written < size)
            {
                r.NextBytes(bytes);
                int writeSize = (int)Math.Min(size - written, bytes.Length);
                s.Write(bytes, 0, writeSize);
                written += bytes.Length;
            }
        }
    }

    private string CreateFileOfSize(string path, long size)
    {
        System.Random r = new System.Random(m_Seed);
        m_Seed++;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        WriteRandomData(path, size, 0);
        return path;
    }

    int m_Seed;
    string m_TempDir;

    [SetUp]
    public void Setup()
    {
        m_Seed = 0;
        BuildCache.PurgeCache(false); // TOOD: If the build cache didn't use global directories, this wouldn't be necessary
        m_TempDir = GetTemporaryDirectory();
    }

    [TearDown]
    public void Teardown()
    {
        Directory.Delete(m_TempDir, true);
    }

    public string[] RunWebExtract(string filePath)
    {
        var baseDir = Path.GetDirectoryName(EditorApplication.applicationPath);
        var webExtractFiles = Directory.GetFiles(baseDir, "WebExtract*", SearchOption.AllDirectories);
        string webExtractPath = webExtractFiles[0];

        Assert.IsTrue(File.Exists(filePath), "Param filePath does not point to an existing file.");

        var process = new Process
        {
            StartInfo =
                {
                    FileName = webExtractPath,
                    Arguments = string.Format(@"""{0}""", filePath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
        };
        process.Start();

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var exitCode = process.ExitCode;
        process.Close();

        Assert.AreEqual(0, exitCode);
        //UnityEngine.Debug.Log(output);
        return Directory.GetFiles(filePath + "_data");
    }

    ArchiveAndCompressBundles.TaskInput GetDefaultInput()
    {
        ArchiveAndCompressBundles.TaskInput input = new ArchiveAndCompressBundles.TaskInput();

        input.InternalFilenameToWriteResults = new Dictionary<string, WriteResult>();
        input.InternalFilenameToBundleName = new Dictionary<string, string>();
        input.AssetToFilesDependencies = new Dictionary<UnityEditor.GUID, List<string>>();
        input.BuildCache = null;
        input.Threaded = false;
        input.ProgressTracker = null;
        input.OutCachedBundles = new List<string>();
        input.GetCompressionForIdentifier = (x) => UnityEngine.BuildCompression.LZ4;
        input.GetOutputFilePathForIdentifier = (x) => Path.Combine(m_TempDir, "bundleoutdir", x);
        input.TempOutputFolder = Path.Combine(m_TempDir, "temptestdir");

        return input;
    }

    private WriteResult AddSimpleBundle(ArchiveAndCompressBundles.TaskInput input, string bundleName, string internalName, string filePath)
    {
        WriteResult writeResult = new WriteResult();
        ResourceFile file = new ResourceFile();
        file.SetFileName(filePath);
        file.SetFileAlias(internalName);
        file.SetSerializedFile(false);
        writeResult.SetResourceFiles(new ResourceFile[] { file });
        input.InternalFilenameToWriteResults.Add(internalName, writeResult);
        input.InternalFilenameToBundleName.Add(internalName, bundleName);
        return writeResult;
    }

    private static string GetUniqueFilename(string desiredFilename)
    {
        string dir = Path.GetDirectoryName(desiredFilename);
        string noExtention = Path.GetFileNameWithoutExtension(desiredFilename);
        string ext = Path.GetExtension(desiredFilename);
        for (int i = 0; true; i++)
        {
            string testName = Path.Combine(dir, Path.Combine($"{noExtention}{i}{ext}"));
            if (!File.Exists(testName))
            {
                return testName;
            }
        }
    }

    private WriteResult AddSimpleBundle(ArchiveAndCompressBundles.TaskInput input, string bundleName, string internalName)
    {
        string tempFilename = CreateFileOfSize(GetUniqueFilename(Path.Combine(m_TempDir, "src", "testfile.bin")), 1024);
        return AddSimpleBundle(input, bundleName, internalName, tempFilename);
    }

    [Test]
    public void WhenAssetInBundleHasDependencies_DependenciesAreInDetails()
    {
        ArchiveAndCompressBundles.TaskInput input = GetDefaultInput();
        AddSimpleBundle(input, "mybundle", "internalName");
        AddSimpleBundle(input, "mybundle2", "internalName2");
        AddSimpleBundle(input, "mybundle3", "internalName3");

        input.AssetToFilesDependencies.Add(new GUID(), new List<string>() { "internalName", "internalName2" });
        input.AssetToFilesDependencies.Add(GUID.Generate(), new List<string>() { "internalName", "internalName3" });

        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output);

        Assert.AreEqual(2, output.BundleDetails["mybundle"].Dependencies.Length);
        Assert.AreEqual("mybundle2", output.BundleDetails["mybundle"].Dependencies[0]);
        Assert.AreEqual("mybundle3", output.BundleDetails["mybundle"].Dependencies[1]);
    }

    [Test]
    public void WhenBundleDoesNotHaveDependencies_DependenciesAreNotInDetails()
    {
        ArchiveAndCompressBundles.TaskInput input = GetDefaultInput();
        AddSimpleBundle(input, "mybundle", "internalName");
        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output);
        Assert.AreEqual(0, output.BundleDetails["mybundle"].Dependencies.Length);
    }

    private static void AssertDirectoriesEqual(string expectedDirectory, string directory, int expectedCount=-1)
    {
        string[] expectedFiles = Directory.GetFiles(expectedDirectory);
        Array.Sort(expectedFiles);
        string[] files = Directory.GetFiles(directory);
        Array.Sort(files);
        if (expectedCount != -1)
            Assert.AreEqual(expectedCount, expectedFiles.Length);
        Assert.AreEqual(expectedFiles.Length, files.Length);

        for (int i = 0; i < expectedFiles.Length; i++)
            FileAssert.AreEqual(expectedFiles[i], files[i]);
    }

    [Test]
    public void WhenArchiveIsAlreadyBuilt_CachedVersionIsUsed()
    {
        string bundleOutDir1 = Path.Combine(m_TempDir, "bundleoutdir1");
        string bundleOutDir2 = Path.Combine(m_TempDir, "bundleoutdir2");
        Directory.CreateDirectory(bundleOutDir1);
        Directory.CreateDirectory(bundleOutDir2);
        ArchiveAndCompressBundles.TaskInput input = GetDefaultInput();
        BuildCache cache = new BuildCache();
        input.BuildCache = cache;
        AddSimpleBundle(input, "mybundle", "internalName");
        input.GetOutputFilePathForIdentifier = (x) => Path.Combine(bundleOutDir1, x);
        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output);
        Assert.AreEqual(0, input.OutCachedBundles.Count);
        cache.SyncPendingSaves();

        input.GetOutputFilePathForIdentifier = (x) => Path.Combine(bundleOutDir2, x);
        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output2);
        Assert.AreEqual(1, input.OutCachedBundles.Count);
        Assert.AreEqual("mybundle", input.OutCachedBundles[0]);
        AssertDirectoriesEqual(bundleOutDir1, bundleOutDir2, 1);
    }

    [Test]
    public void WhenSerializedFileChanges_CachedVersionIsNotUsed()
    {
        ArchiveAndCompressBundles.TaskInput input = GetDefaultInput();
        BuildCache cache = new BuildCache();
        input.BuildCache = cache;
        AddSimpleBundle(input, "mybundle", "internalName");
        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output);
        string srcFile = input.InternalFilenameToWriteResults["internalName"].resourceFiles[0].fileName;
        CreateFileOfSize(srcFile, 2048);
        cache.SyncPendingSaves();
        cache.ClearCacheEntryMaps();
        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output2);
        Assert.AreEqual(0, input.OutCachedBundles.Count);
    }

    [Test]
    public void WhenCalculatingBundleHash_HashingBeginsAtFirstObject()
    {
        ArchiveAndCompressBundles.TaskInput input = GetDefaultInput();
        WriteResult result = AddSimpleBundle(input, "mybundle", "internalName");
        
        // Add a serialized. Say that the first object begins 100 bytes into the file
        var osi = new ObjectSerializedInfo();
        SerializedLocation header = new SerializedLocation();
        header.SetFileName(result.resourceFiles[0].fileAlias);
        header.SetOffset(100);
        osi.SetHeader(header);
        result.SetSerializedObjects(new ObjectSerializedInfo[] { osi });
        ResourceFile rf = result.resourceFiles[0];
        rf.SetSerializedFile(true);
        result.SetResourceFiles(new ResourceFile[] { rf });
        input.InternalFilenameToWriteResults["internalName"] = result;
        string srcFile = input.InternalFilenameToWriteResults["internalName"].resourceFiles[0].fileName;

        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output);
        // Change the first 100 bytes. This is before the serialized object.
        WriteRandomData(srcFile, 100, 1);
        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output2);
        
        // Change the first 104 bytes. This should affect the hash
        WriteRandomData(srcFile, 104, 2);
        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output3);

        Assert.AreEqual(output.BundleDetails["mybundle"].Hash, output2.BundleDetails["mybundle"].Hash);
        Assert.AreNotEqual(output.BundleDetails["mybundle"].Hash, output3.BundleDetails["mybundle"].Hash);
    }

#if UNITY_2019_3_OR_NEWER
    [Test]
    public void WhenBuildingManyArchives_ThreadedAndNonThreadedResultsAreIdentical()
    {
        const int kBundleCount = 100;
        ArchiveAndCompressBundles.TaskInput input = GetDefaultInput();

        for (int i = 0; i < kBundleCount; i++)
            AddSimpleBundle(input, $"mybundle{i}", $"internalName{i}");

        input.Threaded = false;
        input.GetOutputFilePathForIdentifier = (x) => Path.Combine(m_TempDir, "bundleoutdir_nothreading", x);
        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output1);

        input.Threaded = true;
        input.GetOutputFilePathForIdentifier = (x) => Path.Combine(m_TempDir, "bundleoutdir_threading", x);
        ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output2);

        AssertDirectoriesEqual(Path.Combine(m_TempDir, "bundleoutdir_nothreading"), Path.Combine(m_TempDir, "bundleoutdir_threading"), kBundleCount);
    }
#endif

    // Start is called before the first frame update
    [Test]
    public void ResourceFilesAreAddedToBundles()
    {
        ArchiveAndCompressBundles.TaskInput input = GetDefaultInput();
        string bundleOutDir = Path.Combine(m_TempDir, "bundleoutdir");
        
        AddSimpleBundle(input, "mybundle", "internalName");

        string srcFile = input.InternalFilenameToWriteResults["internalName"].resourceFiles[0].fileName;

        ReturnCode code = ArchiveAndCompressBundles.Run(input, out ArchiveAndCompressBundles.TaskOutput output);
        Assert.AreEqual(ReturnCode.Success, code);

        string[] files = RunWebExtract(Path.Combine(bundleOutDir, "mybundle"));
        Assert.AreEqual(1, files.Length);
        FileAssert.AreEqual(files[0], srcFile);
    }
}
