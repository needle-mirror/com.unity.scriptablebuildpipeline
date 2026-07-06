using System.Security.Cryptography;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace UnityEditor.Build.Pipeline.Utilities
{
    /// <summary>
    /// SpookyHash
    /// </summary>
    public unsafe sealed class SpookyHash : HashAlgorithm
    {
        Hash128 m_Hash;

        SpookyHash()
        {
            Initialize();
        }

        /// <summary>
        /// Create a new instance of SpookyHash
        /// </summary>
        /// <returns>The SpookyHash instance</returns>
        public new static SpookyHash Create()
        {
            return new SpookyHash();
        }

        /// <summary>
        /// Initialize the hash state.
        /// </summary>
        public override void Initialize() {}

        protected override void HashCore(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            if (inputBuffer == null || inputOffset < 0 || inputCount <= 0 || (inputCount > inputBuffer.Length) || (inputBuffer.Length - inputCount) < inputOffset)
                return;

            m_Hash.Append(inputBuffer, inputOffset, inputCount);
        }

        protected override byte[] HashFinal()
        {
            byte[] results = new byte[UnsafeUtility.SizeOf<Hash128>()];
            byte* hashPtr = (byte*)UnsafeUtility.AddressOf(ref m_Hash);
            fixed(byte* d = results)
            UnsafeUtility.MemCpy(d, hashPtr, results.Length);
            return results;
        }
    }
}
