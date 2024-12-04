using System;
using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;

namespace UnityEditor.Build.Pipeline
{
    /// <summary>
    /// Basic implementation of IDependencyData. Stores the dependency and usage data calculated during a build.
    /// <see cref="IDependencyData"/>
    /// </summary>
    [Serializable]
    public class BuildDependencyData : IDependencyData
    {
        /// <inheritdoc />
        public Dictionary<GUID, AssetLoadInfo> AssetInfo { get; private set; }

        /// <inheritdoc />
        public Dictionary<GUID, BuildUsageTagSet> AssetUsage { get; private set; }

        /// <inheritdoc />
        public Dictionary<GUID, SceneDependencyInfo> SceneInfo { get; private set; }
        /// <inheritdoc />
        public Dictionary<GUID, BuildUsageTagSet> SceneUsage { get; private set; }
        /// <inheritdoc />
        public Dictionary<GUID, Hash128> DependencyHash { get; private set; }

        /// <summary>
        /// Stores how lighting information is being used during a build.
        /// </summary>
        public BuildUsageTagGlobal GlobalUsage { get; set; }

        [NonSerialized]
        BuildUsageCache m_BuildUsageCache;

        /// <summary>
        /// Stores the dependency caching object.
        /// </summary>
        public BuildUsageCache DependencyUsageCache
        {
            get
            {
                if (m_BuildUsageCache == null)
                    m_BuildUsageCache = new BuildUsageCache();
                return m_BuildUsageCache;
            }
        }

        /// <summary>
        /// Default constructor, initializes properties to defaults
        /// </summary>
        public BuildDependencyData()
        {
            AssetInfo = new Dictionary<GUID, AssetLoadInfo>();
            AssetUsage = new Dictionary<GUID, BuildUsageTagSet>();
            SceneInfo = new Dictionary<GUID, SceneDependencyInfo>();
            SceneUsage = new Dictionary<GUID, BuildUsageTagSet>();
            DependencyHash = new Dictionary<GUID, Hash128>();
            m_BuildUsageCache = new BuildUsageCache();
            GlobalUsage = GraphicsSettingsApi.GetGlobalUsage();
        }
    }

    /// <summary>
    /// Basic implementation of IObjectDependencyData. Stores the dependencies between objects calculated during a build.
    /// <see cref="IObjectDependencyData"/>
    /// </summary>
    [Serializable]
    internal class ObjectDependencyData : IObjectDependencyData
    {
        /// <inheritdoc />
        public Dictionary<ObjectIdentifier, List<ObjectIdentifier>> ObjectDependencyMap { get; }

        /// <summary>
        /// Default constructor, initializes properties to defaults
        /// </summary>
        public ObjectDependencyData()
        {
            ObjectDependencyMap = new Dictionary<ObjectIdentifier, List<ObjectIdentifier>>();
        }
    }
}
