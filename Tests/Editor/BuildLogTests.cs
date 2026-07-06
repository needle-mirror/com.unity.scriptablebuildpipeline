using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using static UnityEditor.Build.Pipeline.Utilities.BuildLog;

namespace UnityEditor.Build.Pipeline.Tests
{
    /// <summary>
    /// BuildLogTests
    /// </summary>
    public class BuildLogTests
    {
        /// <summary>
        /// WhenBeginAndEndScope_DurationIsCorrect
        /// </summary>
        [Test]
        public void WhenBeginAndEndScope_DurationIsCorrect()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "TestStep"))
                Thread.Sleep(5);
            LogStep step0 = (LogStep)log.Root.Children[0];
            Assert.AreEqual("TestStep", step0.Name);
            Assert.Greater(step0.DurationMS, 4);
        }

        /// <summary>
        /// WhenAddMessage_EntryIsCreated
        /// </summary>
        [Test]
        public void WhenAddMessage_EntryIsCreated()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "TestStep"))
                log.AddEntry(LogLevel.Info, "TestEntry");
            Assert.AreEqual("TestEntry", ((LogStep)log.Root.Children[0]).Entries[0].Message);
        }

        /// <summary>
        /// WhenMessageAddedWithScope_EntryIsCreated
        /// </summary>
        [Test]
        public void WhenMessageAddedWithScope_EntryIsCreated()
        {
            BuildLog log = new BuildLog();
            ((IDisposable)log.ScopedStep(LogLevel.Info, "TestStep", "TestEntry")).Dispose();
            Assert.AreEqual("TestEntry", ((LogStep)log.Root.Children[0]).Entries[0].Message);
        }

        /// <summary>
        /// WhenScopeIsThreaded_AndThreadAddsNode_NodeEnteredInThreadedScope
        /// </summary>
        [Test]
        public void WhenScopeIsThreaded_AndThreadAddsNode_NodeEnteredInThreadedScope()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "TestStep", true))
            {
                var t = new Thread(() =>
                {
                    log.AddEntry(LogLevel.Info, "ThreadedMsg1");
                    using (log.ScopedStep(LogLevel.Info, "ThreadedStep"))
                    {
                        log.AddEntry(LogLevel.Info, "ThreadedMsg2");
                    }
                });
                t.Start();
                t.Join();
            }
            LogStep step0 = (LogStep)log.Root.Children[0];
            LogStep step00 = (LogStep)step0.Children[0];
            Assert.AreEqual("ThreadedMsg1", step0.Entries[0].Message);
            Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, step0.Entries[0].ThreadId);
            Assert.AreEqual("ThreadedStep", step00.Name);
            Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, step00.ThreadId);
            Assert.AreEqual("ThreadedMsg2", step00.Entries[0].Message);
            Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, step00.Entries[0].ThreadId);
        }

        /// <summary>
        /// WhenBeginAndEndScopeOnThread_StartAndEndTimeAreWithinMainThreadScope
        /// </summary>
        [Test]
        public void WhenBeginAndEndScopeOnThread_StartAndEndTimeAreWithinMainThreadScope()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "TestStep", true))
            {
                var t = new Thread(() =>
                {
                    Thread.Sleep(1);
                    log.AddEntry(LogLevel.Info, "ThreadedMsg1");
                    Thread.Sleep(1);
                    using (log.ScopedStep(LogLevel.Info, "ThreadedStep"))
                    {
                        Thread.Sleep(2);
                        using (log.ScopedStep(LogLevel.Info, "ThreadedStepNested"))
                            Thread.Sleep(2);
                    }
                    Thread.Sleep(1);
                });
                t.Start();
                t.Join();
            }

            LogStep step0 = (LogStep)log.Root.Children[0];
            LogStep step00 = (LogStep)step0.Children[0];
            LogStep step000 = (LogStep)step00.Children[0];
            double testStepStart = step0.StartTime;
            double threadedMessageStart = step0.Entries[0].Time;
            double threadedScopeStart = step00.StartTime;
            double threadedScopeEnd = threadedScopeStart + step00.DurationMS;
            double threadedScopeNestedStart = step000.StartTime;
            double testStepEnd = testStepStart + step0.DurationMS;

            Assert.Less(threadedScopeStart, threadedScopeNestedStart);
            Assert.Less(testStepStart, threadedMessageStart);
            Assert.Less(threadedMessageStart, threadedScopeStart);
            Assert.Less(threadedScopeStart, threadedScopeEnd);
            Assert.Less(threadedScopeEnd, testStepEnd);
        }

        /// <summary>
        /// WhenConvertingToTraceEventFormat_BackslashesAreEscaped
        /// </summary>
        [Test]
        public void WhenConvertingToTraceEventFormat_BackslashesAreEscaped()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "TestStep\\AfterSlash"))
                log.AddEntry(LogLevel.Info, "TestEntry\\AfterSlash");
            string text = log.FormatForTraceEventProfiler();
            StringAssert.Contains("TestStep\\\\AfterSlash", text);
            StringAssert.Contains("TestEntry\\\\AfterSlash", text);
        }

        /// <summary>
        /// WhenConvertingToTraceEventFormat_MetaDataIsAdded
        /// </summary>
        [Test]
        public void WhenConvertingToTraceEventFormat_MetaDataIsAdded()
        {
            BuildLog log = new BuildLog();
            log.AddMetaData("SOMEKEY", "SOMEVALUE");
            string text = log.FormatForTraceEventProfiler();
            StringAssert.Contains("SOMEKEY", text);
            StringAssert.Contains("SOMEVALUE", text);
        }

        const string k_MinimalTEP = "{\n\"traceEvents\": [\n{\"name\": \"ExternalEvent\", \"ph\": \"X\", \"dur\": 1, \"tid\": 1, \"ts\": 100, \"pid\": 1}\n]\n}";

        /// <summary>
        /// WhenImportExternalTEP_EventsAppearInOrderInOutput
        /// </summary>
        [Test]
        public void WhenImportExternalTEP_EventsAppearInOrderInOutput()
        {
            string tempPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempPath, k_MinimalTEP);
                BuildLog log = new BuildLog();
                using (log.ScopedStep(LogLevel.Info, "StepA"))
                    log.ImportExternalTEP(tempPath);
                using (log.ScopedStep(LogLevel.Info, "StepB")) { }

                string output = log.FormatForTraceEventProfiler();
                int posA = output.IndexOf("StepA", StringComparison.Ordinal);
                int posExt = output.IndexOf("ExternalEvent", StringComparison.Ordinal);
                int posB = output.IndexOf("StepB", StringComparison.Ordinal);

                Assert.Greater(posExt, posA, "External events should appear after StepA in TEP output");
                Assert.Greater(posB, posExt, "StepB should appear after external events in TEP output");
            }
            finally { File.Delete(tempPath); }
        }

        /// <summary>
        /// WhenImportExternalTEP_NonexistentPath_IsNoOp
        /// </summary>
        [Test]
        public void WhenImportExternalTEP_NonexistentPath_IsNoOp()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "StepA"))
                log.ImportExternalTEP("/does/not/exist/file.json");

            Assert.DoesNotThrow(() => log.FormatForTraceEventProfiler());
            StringAssert.Contains("StepA", log.FormatForTraceEventProfiler());
        }

        /// <summary>
        /// WhenImportExternalTEP_FileWrittenAfterImportCall_EventsStillAppear
        /// </summary>
        [Test]
        public void WhenImportExternalTEP_FileWrittenAfterImportCall_EventsStillAppear()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            try
            {
                BuildLog log = new BuildLog();
                using (log.ScopedStep(LogLevel.Info, "StepA"))
                    log.ImportExternalTEP(tempPath);

                File.WriteAllText(tempPath, k_MinimalTEP);

                string output = log.FormatForTraceEventProfiler();
                StringAssert.Contains("ExternalEvent", output);
            }
            finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
        }

        /// <summary>
        /// WhenImportExternalTEP_InThreadedScope_ExternalEventsAreIncluded
        /// </summary>
        [Test]
        public void WhenImportExternalTEP_InThreadedScope_ExternalEventsAreIncluded()
        {
            string tempPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempPath, k_MinimalTEP);
                BuildLog log = new BuildLog();
                using (log.ScopedStep(LogLevel.Info, "OuterStep", true))
                {
                    var t = new Thread(() => log.ImportExternalTEP(tempPath));
                    t.Start();
                    t.Join();
                }
                StringAssert.Contains("ExternalEvent", log.FormatForTraceEventProfiler());
            }
            finally { File.Delete(tempPath); }
        }

        /// <summary>
        /// WhenImportExternalTEP_AbsoluteTimestampsAreRemappedToImportTime
        /// </summary>
        [Test]
        public void WhenImportExternalTEP_AbsoluteTimestampsAreRemappedToImportTime()
        {
            // Simulate Unity native pipeline TEP with absolute system-time ts (~543 billion us).
            // The two events span 1000 us; after remapping the last event should land at
            // approximately importTimeMs and the first at importTimeMs - 1000 us.
            const string tepAbsoluteTs = "{\n\"traceEvents\": [\n" +
                "{\"name\": \"EventFirst\", \"ph\": \"X\", \"dur\": 1, \"tid\": 1, \"ts\": 543514236458, \"pid\": 29224},\n" +
                "{\"name\": \"EventLast\",  \"ph\": \"X\", \"dur\": 1, \"tid\": 1, \"ts\": 543514237458, \"pid\": 29224}\n" +
                "]\n}";
            string tempPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempPath, tepAbsoluteTs);
                BuildLog log = new BuildLog();
                using (log.ScopedStep(LogLevel.Info, "StepA"))
                    log.ImportExternalTEP(tempPath);

                string output = log.FormatForTraceEventProfiler();
                StringAssert.Contains("EventFirst", output);
                StringAssert.Contains("EventLast", output);
                // The huge absolute ts values must be remapped to small relative values
                StringAssert.DoesNotContain("543514236458", output);
                StringAssert.DoesNotContain("543514237458", output);
            }
            finally { File.Delete(tempPath); }
        }

        /// <summary>
        /// FormatForTraceEventProfiler_HostEvents_UseCurrentProcessId
        /// </summary>
        [Test]
        public void FormatForTraceEventProfiler_HostEvents_UseCurrentProcessId()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "StepA")) { }

            string output = log.FormatForTraceEventProfiler();
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            StringAssert.Contains($"\"pid\": {pid}", output);
        }

        /// <summary>
        /// WhenBeginAndEndDeferredEventsDontMatchUp_HandleDeferredEventsStream_ThrowsException
        /// </summary>
        [Test]
        public void WhenBeginAndEndDeferredEventsDontMatchUp_HandleDeferredEventsStream_ThrowsException()
        {
            BuildLog log = new BuildLog();
            DeferredEvent startEvent = new DeferredEvent() { Type = DeferredEventType.Begin };
            List<DeferredEvent> events = new List<DeferredEvent>() { startEvent };

            Assert.Throws<Exception>(() => log.HandleDeferredEventStreamInternal(events));
        }

        /// <summary>
        /// WhenBeginAndEndDeferredEventsMatchUp_HandleDeferredEventsStream_CreatesLogEvents
        /// </summary>
        [Test]
        public void WhenBeginAndEndDeferredEventsMatchUp_HandleDeferredEventsStream_CreatesLogEvents()
        {
            BuildLog log = new BuildLog();
            DeferredEvent startEvent = new DeferredEvent() { Name = "Start", Type = DeferredEventType.Begin };
            DeferredEvent endEvent = new DeferredEvent() { Name = "End", Type = DeferredEventType.End };
            List<DeferredEvent> events = new List<DeferredEvent>() { startEvent, endEvent };

            log.HandleDeferredEventStreamInternal(events);
            Assert.AreEqual(startEvent.Name, ((LogStep)log.Root.Children[0]).Name);
        }

        /// <summary>
        /// WhenDeferredEventsAreOnlyInfoTypes_HandleDeferredEventsStream_CreatesLogEntry
        /// </summary>
        [Test]
        public void WhenDeferredEventsAreOnlyInfoTypes_HandleDeferredEventsStream_CreatesLogEntry()
        {
            BuildLog log = new BuildLog();
            DeferredEvent infoEvent = new DeferredEvent() { Name = "Info", Type = DeferredEventType.Info };
            List<DeferredEvent> events = new List<DeferredEvent>() { infoEvent };

            log.HandleDeferredEventStreamInternal(events);
            Assert.AreEqual(infoEvent.Name, log.Root.Entries[0].Message);
        }

        /// <summary>
        /// WhenAddArg_ArgAppearsAsNamedKeyInTEPOutput
        /// </summary>
        [Test]
        public void WhenAddArg_ArgAppearsAsNamedKeyInTEPOutput()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "TestStep"))
                log.AddArg("Compression", "LZ4");
            string text = log.FormatForTraceEventProfiler();
            StringAssert.Contains("\"Compression\":\"LZ4\"", text);
        }

        /// <summary>
        /// WhenAddArgAndAddEntry_BothAppearInSameArgsObject
        /// </summary>
        [Test]
        public void WhenAddArgAndAddEntry_BothAppearInSameArgsObject()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "TestStep"))
            {
                log.AddArg("Key", "Value");
                log.AddEntry(LogLevel.Info, "EntryMsg");
            }
            string text = log.FormatForTraceEventProfiler();
            // Named arg and numbered entry must appear inside one args block, on the same event line
            StringAssert.Contains("\"Key\":\"Value\"", text);
            StringAssert.Contains("\"0\":\"EntryMsg\"", text);
            // Both must appear between the same "args": { and closing }
            int argsStart = text.IndexOf("\"args\": {", StringComparison.Ordinal);
            int argsEnd = text.IndexOf("}", argsStart, StringComparison.Ordinal);
            Assert.Greater(argsStart, -1);
            Assert.Less(text.IndexOf("\"Key\"", argsStart, StringComparison.Ordinal), argsEnd);
            Assert.Less(text.IndexOf("\"0\"", argsStart, StringComparison.Ordinal), argsEnd);
        }

        /// <summary>
        /// WhenAddArgInThreadedScope_ArgAppearsOnChildStep
        /// </summary>
        [Test]
        public void WhenAddArgInThreadedScope_ArgAppearsOnChildStep()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "ParentStep", true))
            {
                var t = new Thread(() =>
                {
                    using (log.ScopedStep(LogLevel.Info, "ChildStep"))
                        log.AddArg("ThreadKey", "ThreadValue");
                });
                t.Start();
                t.Join();
            }
            string text = log.FormatForTraceEventProfiler();
            int traceEventsPos = text.IndexOf("traceEvents", StringComparison.Ordinal);
            int childStepPos = text.IndexOf("ChildStep", traceEventsPos, StringComparison.Ordinal);
            int argPos = text.IndexOf("\"ThreadKey\":\"ThreadValue\"", StringComparison.Ordinal);
            Assert.Greater(childStepPos, traceEventsPos, "ChildStep should appear inside traceEvents");
            Assert.Greater(argPos, childStepPos, "ThreadKey arg should appear after ChildStep name in its event");
        }

        /// <summary>
        /// WhenScopedStepWithInlineArgs_ArgsAppearOnStep
        /// </summary>
        [Test]
        public void WhenScopedStepWithInlineArgs_ArgsAppearOnStep()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "TestStep",
                ("Compression", "LZ4"),
                ("FileCount", "42")))
            { }
            string text = log.FormatForTraceEventProfiler();
            StringAssert.Contains("\"Compression\":\"LZ4\"", text);
            StringAssert.Contains("\"FileCount\":\"42\"", text);
        }

        /// <summary>
        /// WhenScopedStepWithInlineArgsAndMultiThreaded_ArgsAppearOnParentStep
        /// </summary>
        [Test]
        public void WhenScopedStepWithInlineArgsAndMultiThreaded_ArgsAppearOnParentStep()
        {
            BuildLog log = new BuildLog();
            using (log.ScopedStep(LogLevel.Info, "ParentStep", true,
                ("ParentKey", "ParentValue")))
            {
                var t = new Thread(() =>
                {
                    using (log.ScopedStep(LogLevel.Info, "ChildStep",
                        ("ChildKey", "ChildValue")))
                    { }
                });
                t.Start();
                t.Join();
            }
            string text = log.FormatForTraceEventProfiler();
            StringAssert.Contains("\"ParentKey\":\"ParentValue\"", text);
            StringAssert.Contains("\"ChildKey\":\"ChildValue\"", text);
        }
    }
}
