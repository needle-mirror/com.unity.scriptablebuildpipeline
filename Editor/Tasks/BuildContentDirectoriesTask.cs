#if ENABLE_CONTENT_DIRECTORIES
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.Build.Pipeline.Tasks
{
    /// <summary>
    /// The build task used to build content directories.
    /// </summary>
    public class BuildContentDirectoriesTask : IBuildTask
    {
        [InjectContext(ContextUsage.In)]
        IBuildParameters m_Parameters;

        [InjectContext(ContextUsage.Out)]
        IBuildReportContext m_BuildReportContext;

        /// <inheritdoc />
        public int Version => 1;

        /// <inheritdoc />
        public ReturnCode Run()
        {
            var parameters = m_Parameters.GetContentDirectoryParameters();
            var report = BuildPipeline.BuildContentDirectory(parameters);
            m_BuildReportContext = new BuildReportContext(report);
            return report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? ReturnCode.Success : ReturnCode.Error;
        }
    }
}
#endif
