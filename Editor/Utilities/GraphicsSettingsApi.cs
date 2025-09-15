using UnityEditor.Build.Content;

namespace UnityEditor.Build.Pipeline.Utilities
{
    static class GraphicsSettingsApi
    {
        internal static BuildUsageTagGlobal GetGlobalUsage()
        {
            return ContentBuildInterface.GetGlobalUsageFromGraphicsSettings();
        }
    }
}
