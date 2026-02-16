using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.Build.Pipeline.Tasks
{
    /// <summary>
    /// Processes all callbacks after the writing task.
    /// </summary>
    public class CombineExtractedTypeTreeData : IBuildTask
    {
        /// <inheritdoc />
        public int Version { get { return 1; } }
        /// <summary>
        /// The output path for the combined TypeTree data file.
        /// </summary>
        public string OutputPath { get; set; }

#pragma warning disable 649
        [InjectContext]
        IBuildParameters m_Parameters;

        [InjectContext]
        IBuildResults m_Results;
#pragma warning restore 649

        /// <inheritdoc />
        public ReturnCode Run()
        {
#if UNITY_6000_5_OR_NEWER
            if (m_Parameters.ContentBuildFlags.HasFlag(ContentBuildFlags.ExtractTypeTree))
            {
                var dir = Path.GetDirectoryName(OutputPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                if (File.Exists(OutputPath))
                    File.Delete(OutputPath);
                var paths = m_Results.WriteResults.Select(r => r.Value.extractedTypeTreeDataPath).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                Array.Sort(paths);
                if (!ContentBuildInterface.CombineExtractedTypeTreeDataFiles(paths, OutputPath))
                    throw new Exception($"{nameof(CombineExtractedTypeTreeData)} - failed to create TypeTree data file at path {OutputPath}.");
            }
#endif
            return ReturnCode.Success;
        }
    }
}
