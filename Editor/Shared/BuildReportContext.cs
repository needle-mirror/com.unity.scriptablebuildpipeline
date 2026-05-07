#if ENABLE_CONTENT_DIRECTORIES
using System;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// An interface for build report context objects that need to store a BuildReport.
    /// </summary>
    public interface IBuildReportContext : IContextObject
    {
        /// <summary>
        /// The build report being generated.
        /// </summary>
        BuildReport Report { get; }
    }

    /// <summary>
    /// Context object used to store the BuildReport being generated.
    /// </summary>
    [Serializable]
    public class BuildReportContext : IBuildReportContext
    {
        BuildReport m_Report;

        /// <summary>
        /// The build report being generated.
        /// </summary>
        public BuildReport Report { get { return m_Report; } }

        /// <summary>
        /// Constructor for BuildReportContext
        /// </summary>
        public BuildReportContext()
        {
            m_Report = null;
        }

        /// <summary>
        /// Constructor for BuildReportContext
        /// </summary>
        /// <param name="report">The build report being generated.</param>
        public BuildReportContext(BuildReport report)
        {
            m_Report = report;
        }
    }
}
#endif
