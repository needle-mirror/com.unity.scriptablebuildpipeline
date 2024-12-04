using System;
using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEditor.Build.Player;
using UnityEngine;
using UnityEngine.Build.Pipeline;

namespace UnityEditor.Build.Pipeline.Interfaces
{
    /// <summary>
    /// Container for Asset data calculated by the build
    /// </summary>
    public struct AssetResultData
    {
        /// <summary>
        /// Asset GUID
        /// </summary>
        public GUID Guid;

        /// <summary>
        /// Asest hash
        /// </summary>
        public Hash128 Hash;

        /// <summary>
        /// Objects included in the asset
        /// </summary>
        public List<ObjectIdentifier> IncludedObjects;

        /// <summary>
        /// External objects referenced by the asset
        /// </summary>
        public List<ObjectIdentifier> ReferencedObjects;

        /// <summary>
        /// Object ID to type map
        /// </summary>
        public Dictionary<ObjectIdentifier, Type[]> ObjectTypes;
    }

    /// <summary>
    /// Base interface for the build results container
    /// </summary>
    public interface IBuildResults : IContextObject
    {
        /// <summary>
        /// Results from the script compiling step.
        /// </summary>
        ScriptCompilationResult ScriptResults { get; set; }

        /// <summary>
        /// Map of serialized file name to results for built content.
        /// </summary>
        Dictionary<string, WriteResult> WriteResults { get; }

        /// <summary>
        /// Map of serialized file name to additional metadata associated with the write result.
        /// </summary>
        Dictionary<string, SerializedFileMetaData> WriteResultsMetaData { get; }

        /// <summary>
        /// Map of Asset data included in this build
        /// </summary>
        Dictionary<GUID, AssetResultData> AssetResults { get; }
    }

    /// <summary>
    /// Extended interface for Asset Bundle build results container.
    /// <see cref="IBuildResults"/>
    /// </summary>
    public interface IBundleBuildResults : IBuildResults
    {
        /// <summary>
        /// Map of Asset Bundle name to details about the built bundle.
        /// </summary>
        Dictionary<string, BundleDetails> BundleInfos { get; }
    }
}
