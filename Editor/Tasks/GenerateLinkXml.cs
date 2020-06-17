using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;

namespace UnityEditor.Build.Pipeline.Tasks
{
    public class GenerateLinkXml : IBuildTask
    {
        /// <inheritdoc />
        public int Version { get { return 1; } }

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IBuildParameters m_Parameters;

        [InjectContext(ContextUsage.In)]
        IBuildResults m_Results;
#pragma warning restore 649

        const string k_LinkXml = "link.xml";

        public ReturnCode Run()
        {
            if (!m_Parameters.WriteLinkXML)
                return ReturnCode.SuccessNotRun;

            var linker = LinkXmlGenerator.CreateDefault();
            foreach (var writeResult in m_Results.WriteResults)
                linker.AddTypes(writeResult.Value.includedTypes);

            var linkPath = m_Parameters.GetOutputFilePathForIdentifier(k_LinkXml);
            linker.Save(linkPath);

            return ReturnCode.Success;
        }
    }
}
