using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace UnityEditor.Build.Pipeline.Tests
{
    class MyContextObjectClass : IContextObject
    {
    }

    interface ITestInterfaceWithContextDerivation : IContextObject {}
    class TestITestInterfaceWithContextDerivationImplementation : ITestInterfaceWithContextDerivation
    {}

    /// <summary>
    /// BuildContextTests
    /// </summary>
    public class BuildContextTests
    {
        BuildContext m_Ctx;

        /// <summary>
        /// Setup
        /// </summary>
        [SetUp]
        public void Setup()
        {
            m_Ctx = new BuildContext();
        }

        /// <summary>
        /// SetContextObject_WhenTypeDoesNotExist_AddsContextObject
        /// </summary>
        [Test]
        public void SetContextObject_WhenTypeDoesNotExist_AddsContextObject()
        {
            m_Ctx.SetContextObject(new MyContextObjectClass());
            Assert.NotNull(m_Ctx.GetContextObject<MyContextObjectClass>());
        }

        /// <summary>
        /// SetContextObject_WhenTypeHasInterfaceAssignableToContextObject_InterfaceAndObjectTypeUsedAsKey
        /// </summary>
        [Test]
        public void SetContextObject_WhenTypeHasInterfaceAssignableToContextObject_InterfaceAndObjectTypeUsedAsKey()
        {
            m_Ctx.SetContextObject(new TestITestInterfaceWithContextDerivationImplementation());
            Assert.NotNull(m_Ctx.GetContextObject<ITestInterfaceWithContextDerivation>());
            Assert.NotNull(m_Ctx.GetContextObject<TestITestInterfaceWithContextDerivationImplementation>());
        }

        /// <summary>
        /// GetContextObject_WhenTypeDoesNotExist_Throws
        /// </summary>
        [Test]
        public void GetContextObject_WhenTypeDoesNotExist_Throws()
        {
            Assert.Throws(typeof(Exception), () => m_Ctx.GetContextObject<ITestInterfaceWithContextDerivation>());
        }
    }
}
