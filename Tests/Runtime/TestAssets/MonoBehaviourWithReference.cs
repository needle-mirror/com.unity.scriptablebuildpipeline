using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ScriptableBuildPipelineTests.Runtime.Tests
{
    /// <summary>
    /// A monobehavior with a reference to a Unity Object
    /// </summary>
    public class MonoBehaviourWithReference : MonoBehaviour
    {
        /// <summary>
        /// The Object we're referencing
        /// </summary>
        public Object Reference;
    }
}
