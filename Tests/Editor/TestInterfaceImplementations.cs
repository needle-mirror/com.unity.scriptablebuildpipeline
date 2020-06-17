using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Player;
using UnityEngine;

namespace UnityEditor.Build.Pipeline.Tests
{
    internal class TestBuildParametersBase : IBuildParameters
    {
        public virtual BuildTarget Target { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public virtual BuildTargetGroup Group { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public virtual ContentBuildFlags ContentBuildFlags { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public virtual TypeDB ScriptInfo { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public virtual ScriptCompilationOptions ScriptOptions { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public virtual string TempOutputFolder { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public virtual bool UseCache { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public virtual string CacheServerHost { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public virtual int CacheServerPort { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public virtual bool WriteLinkXML { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public virtual UnityEngine.BuildCompression GetCompressionForIdentifier(string identifier)
        {
            throw new System.NotImplementedException();
        }

        public virtual BuildSettings GetContentBuildSettings()
        {
            throw new System.NotImplementedException();
        }

        public virtual string GetOutputFilePathForIdentifier(string identifier)
        {
            throw new System.NotImplementedException();
        }

        public virtual ScriptCompilationSettings GetScriptCompilationSettings()
        {
            throw new System.NotImplementedException();
        }
    }

    internal class TestDependencyDataBase : IDependencyData
    {
        public virtual Dictionary<GUID, AssetLoadInfo> AssetInfo => throw new System.NotImplementedException();

        public virtual Dictionary<GUID, BuildUsageTagSet> AssetUsage => throw new System.NotImplementedException();

        public virtual Dictionary<GUID, SceneDependencyInfo> SceneInfo => throw new System.NotImplementedException();

        public virtual Dictionary<GUID, BuildUsageTagSet> SceneUsage => throw new System.NotImplementedException();

        public virtual BuildUsageCache DependencyUsageCache => throw new System.NotImplementedException();

        public virtual BuildUsageTagGlobal GlobalUsage => throw new System.NotImplementedException();
    }

    internal class TestWriteDataBase : IWriteData
    {
        public virtual Dictionary<GUID, List<string>> AssetToFiles => throw new System.NotImplementedException();

        public virtual Dictionary<string, List<ObjectIdentifier>> FileToObjects => throw new System.NotImplementedException();

        public virtual List<IWriteOperation> WriteOperations => throw new System.NotImplementedException();
    }

    internal class TestBuildResultsBase : IBuildResults
    {
        public virtual ScriptCompilationResult ScriptResults { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public virtual Dictionary<string, WriteResult> WriteResults => throw new System.NotImplementedException();

        public virtual Dictionary<string, SerializedFileMetaData> WriteResultsMetaData => throw new System.NotImplementedException();
    }
}
