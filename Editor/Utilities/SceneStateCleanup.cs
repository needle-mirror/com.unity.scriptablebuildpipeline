using System;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.SceneManagement;
using System.Linq;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.Build.Utilities
{
    /// <summary>
    /// Cleans up scenes in the scene manager.
    /// </summary>
    public class SceneStateCleanup : IDisposable
    {
        IBuildLogger m_Logger;

        SceneSetup[] m_Scenes;

        bool m_Disposed;

        /// <summary>
        /// Creates a new scene state cleanup object.
        /// </summary>
        /// <param name="logger">The build logger to report scene restore steps to, or null for no logging.</param>
        public SceneStateCleanup(IBuildLogger logger = null)
        {
            m_Logger = logger;
            using (m_Logger?.ScopedStep(LogLevel.Verbose, "SceneStateCleanup Init"))
            {
                m_Scenes = EditorSceneManager.GetSceneManagerSetup();
            }
        }

        /// <summary>
        /// Disposes of the scene state cleanup instance.
        /// </summary>
        public void Dispose()
        {
            using (m_Logger?.ScopedStep(LogLevel.Verbose, "SceneStateCleanup Dispose"))
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Disposes of the scene state cleanup instance.
        /// </summary>
        /// <param name="disposing">Set to true to reset the scenes list. Set to false to leave the scenes list as is.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            m_Disposed = true;

            if (disposing)
            {
                // Test runner injects scenes, so we strip those here
                var scenes = m_Scenes.Where(s => !string.IsNullOrEmpty(s.path)).ToArray();
                if (!scenes.IsNullOrEmpty())
                    EditorSceneManager.RestoreSceneManagerSetup(scenes);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }
    }
}
