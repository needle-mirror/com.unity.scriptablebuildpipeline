using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

namespace UnityEditor.Build.Pipeline.Tests.ContentLoad
{

    /// <summary>
    /// Simple test catalog that maps addressable names to content files and local file identifiers, used by the content load tests.
    /// </summary>
    [Serializable()]
    public class Catalog
    {
        /// <summary>
        /// Describes a single content file and its dependencies.
        /// </summary>
        [Serializable()]
        public class ContentFileInfo
        {
            /// <summary>
            /// The filename of the content file.
            /// </summary>
            public string Filename;

            /// <summary>
            /// The filenames of the content files this file depends on.
            /// </summary>
            public List<string> Dependencies;
        }

        /// <summary>
        /// Describes where an addressable object can be loaded from.
        /// </summary>
        [Serializable()]
        public class AddressableLocation
        {
            /// <summary>
            /// The addressable name of the object.
            /// </summary>
            public string AddressableName;

            /// <summary>
            /// The filename of the content file containing the object.
            /// </summary>
            public string Filename;

            /// <summary>
            /// The local file identifier of the object within the content file.
            /// </summary>
            public ulong LFID;
        }

        /// <summary>
        /// The content files in this catalog.
        /// </summary>
        public List<ContentFileInfo> ContentFiles = new List<ContentFileInfo>();

        /// <summary>
        /// The addressable locations in this catalog.
        /// </summary>
        public List<AddressableLocation> Locations = new List<AddressableLocation>();

        private Dictionary<string, AddressableLocation> AddressToLocation =
            new Dictionary<string, AddressableLocation>();

        private Dictionary<string, ContentFileInfo> FileToInfo = new Dictionary<string, ContentFileInfo>();

        /// <summary>
        /// Creates an empty catalog.
        /// </summary>
        public Catalog()
        {
        }

        /// <summary>
        /// Reads the entire contents of a file as text using the AsyncReadManager, which supports virtual file system paths.
        /// </summary>
        /// <param name="path">The path of the file to read.</param>
        /// <returns>The contents of the file as a string.</returns>
        unsafe public static string ReadAllTextVFS(string path)
        {
            FileInfoResult infoResult;
            ReadHandle h = AsyncReadManager.GetFileInfo(path, &infoResult);
            h.JobHandle.Complete();
            var getInfoStatus = h.Status;
            h.Dispose();

            if (getInfoStatus != ReadStatus.Complete)
                throw new Exception($"Could not get file info for path {path}");

            FileHandle fH = AsyncReadManager.OpenFileAsync(path);
            ReadCommand cmd;
            cmd.Buffer = UnsafeUtility.Malloc(infoResult.FileSize, 0, Unity.Collections.Allocator.Temp);
            cmd.Offset = 0;
            cmd.Size = infoResult.FileSize;
            var readHandle = AsyncReadManager.Read(path, &cmd, 1);
            readHandle.JobHandle.Complete();
            AsyncReadManager.CloseCachedFileAsync(path).Complete();

            var readResult = readHandle.Status;
            readHandle.Dispose();

            if (readResult != ReadStatus.Complete)
            {
                UnsafeUtility.Free(cmd.Buffer, Unity.Collections.Allocator.Temp);
                throw new Exception($"Failed to read data from {path}");
            }

            // Convert to string
            string text = System.Text.Encoding.Default.GetString((byte*) cmd.Buffer, (int) cmd.Size);

            UnsafeUtility.Free(cmd.Buffer, Unity.Collections.Allocator.Temp);
            return text;
        }

        /// <summary>
        /// Loads a catalog from a json file.
        /// </summary>
        /// <param name="path">The path of the catalog json file.</param>
        /// <returns>The deserialized catalog.</returns>
        public static Catalog LoadFromFile(string path)
        {
            string jsonText = ReadAllTextVFS(path);
            Catalog catalog = JsonUtility.FromJson<Catalog>(jsonText);
            catalog.OnDeserialize();
            return catalog;
        }

        /// <summary>
        /// Gets the location for an addressable name.
        /// </summary>
        /// <param name="name">The addressable name to look up.</param>
        /// <returns>The location for the addressable name.</returns>
        public AddressableLocation GetLocation(string name)
        {
            return AddressToLocation[name];
        }

        /// <summary>
        /// Gets the info for a content file.
        /// </summary>
        /// <param name="filename">The filename of the content file to look up.</param>
        /// <returns>The info for the content file.</returns>
        public ContentFileInfo GetFileInfo(string filename)
        {
            return FileToInfo[filename];
        }

        void BuildMaps()
        {
            AddressToLocation = new Dictionary<string, AddressableLocation>();
            FileToInfo = new Dictionary<string, ContentFileInfo>();
            foreach (ContentFileInfo f in ContentFiles)
                FileToInfo[f.Filename] = f;
            foreach (AddressableLocation l in Locations)
            {
                AddressToLocation[l.AddressableName] = l;
            }
        }

        /// <summary>
        /// Rebuilds the internal lookup maps after deserialization.
        /// </summary>
        [OnDeserializing()]
        public void OnDeserialize()
        {
            BuildMaps();
        }
    }
}
