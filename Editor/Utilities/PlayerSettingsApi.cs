using UnityEngine;

namespace UnityEditor.Build.Pipeline.Utilities
{
    static class PlayerSettingsApi
    {
        static SerializedObject m_Target;
        static SerializedProperty m_NumberOfMipsStripped;

        static PlayerSettingsApi()
        {
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            m_Target = new SerializedObject(playerSettings);
            m_NumberOfMipsStripped = m_Target.FindProperty("numberOfMipsStripped");
        }

        internal static int GetNumberOfMipsStripped()
        {
            m_Target.Update();
            return m_NumberOfMipsStripped.intValue;
        }

    }
}
