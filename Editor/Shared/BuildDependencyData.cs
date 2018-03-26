﻿using System;
using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.Build.Pipeline
{
    /// <summary>
    /// Basic implementation of IDependencyData. Stores the dependency and usage data calculated during a build.
    /// <seealso cref="IDependencyData"/>
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

        /// <summary>
        /// Default constructor, initializes properties to defaults
        /// </summary>
        public BuildDependencyData()
        {
            AssetInfo = new Dictionary<GUID, AssetLoadInfo>();
            AssetUsage = new Dictionary<GUID, BuildUsageTagSet>();
            SceneInfo = new Dictionary<GUID, SceneDependencyInfo>();
            SceneUsage = new Dictionary<GUID, BuildUsageTagSet>();
        }
    }
}
