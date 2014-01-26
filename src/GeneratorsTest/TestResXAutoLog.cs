#region Copyright 2011-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace CSharpTest.Net.GeneratorsTest
{
    [TestFixture]
    public class TestResXAutoLog
    {
        [Test]
        public void TestSimpleResXLogEvent()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            string messageText = String.Format("NUnit {0} {1}", GetType(), Guid.NewGuid());
            //Enables the LOG feature to automagically generate log calls on all exception constructors.
            builder.Add(".AutoLog", true);
            builder.Add(".EventSource", "CSharpTest - NUnit");

            //If .NextMessageId is not defined you must specify one for logging to enable on that item.
            builder.Add("SimpleLog(string text)", "{0}", "#MessageId=1");

            TestResourceResult result = builder.Compile();

            Assert.AreEqual(messageText, result.GetValue("SimpleLog", messageText));

            using (EventLog applog = new EventLog("Application"))
            {
                EventLogEntry found = null;
                EventLogEntryCollection entries = applog.Entries;
                int stop = Math.Max(0, entries.Count - 50);
                for (int i = entries.Count - 1; i >= stop; i--)
                    if (entries[i].Message.Contains(messageText))
                    {
                        found = entries[i];
                        break;
                    }
                Assert.IsNotNull(found);
                Assert.AreEqual("CSharpTest - NUnit", found.Source);
                Assert.AreEqual(1, found.ReplacementStrings.Length);
                Assert.AreEqual(messageText, found.ReplacementStrings[0]);
            }
        }

        static EventLog _lastLog;
        static EventInstance _lastEvent;
        static string[] _lastArgs;

        /// <summary>
        /// The signature of this method does not change, it provides everything you need to know about the
        /// log event, you may prepend fixed arguments to the arguments parameters, then simply call write.
        /// BTW, since this is called from the formatter method, or from the exception constructor, you may
        /// assume the current stack is the execution context for the exception.  The exception's StackTrace
        /// will be empty since the exception has not been thrown.
        /// </summary>
        public static void TestCustomEventWriter(string eventLog, string eventSource, int category, EventLogEntryType eventType, long eventId, object[] arguments, Exception error)
        {
            _lastLog = new EventLog(eventLog, ".", eventSource);
            _lastEvent = new EventInstance(eventId, category, eventType);
            _lastArgs = (string[])arguments;
        }

        [Test]
        public void TestFormatResxOptions()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            //the next message id to use when autoLog is enabled.
            builder.Add(".NextMessageId", 5);

            //the format string for the full hresult to a uri.
            builder.Add(".HelpLink", new Uri("http://mydomain/errorcodes.aspx?id={0:x8}"));

            //Trailing message text for event log, can include {0} formats. which are specified before the message-specific values
            builder.Add(".EventMessageFormat", "Trailing message text for event log, can include {0} formats.");

            //Enables the LOG feature to automagically generate log calls on all exception constructors.
            builder.Add(".AutoLog", true);

            //The fully-qualified method used to write the log to the event log
            builder.Add(".AutoLogMethod", String.Format("{0}.TestCustomEventWriter", GetType().FullName));

            //The name of the event source to register, can be qualified with log: "Log-Name/Event-Source"
            builder.Add(".EventSource", "Application/YourAppName");

            //OPTIONAL (default=0): The category id and name for the category of the message, should be unique for this ResX file 
            builder.Add(".EventCategory", 0x0F, "MyCategory");

            //OPTIONAL (default=0): The facility code (256-2047) and name to define for these messages
            builder.Add(".Facility", 258, "MyFacility");

            builder.Add("TestFormatting(int value)", "TestFormatting-{0:x8}", "#MessageId=3");
            builder.Add("TestWarning", "TestWarning", "(int unprinted) #MessageId=251, Severity=Warn");
            builder.Add("TestException(string s, int i)", "TestException-{0}-{1}", ":System.Runtime.InteropServices.COMException");

            string code, resx;
            TestResourceResult result;
            StringWriter captureErr = new StringWriter();
            TextWriter stderr = Console.Error;
            try
            {
                Console.SetError(captureErr);
                result = builder.Compile("-reference:" + GetType().Assembly.Location, out code, out resx);
            }
            finally { Console.SetError(stderr); }

            _lastEvent = null;
            Assert.AreEqual("TestFormatting-000e1234", result.GetValue("TestFormatting", 0x0e1234));
            Assert.IsNotNull(_lastEvent);
            Assert.AreEqual("Application", _lastLog.Log);
            Assert.AreEqual("YourAppName", _lastLog.Source);
            Assert.AreEqual(0x0F, _lastEvent.CategoryId);
            Assert.AreEqual(EventLogEntryType.Information, _lastEvent.EntryType);
            Assert.AreEqual(0x41020003L, _lastEvent.InstanceId);
            Assert.AreEqual(1, _lastArgs.Length);
            Assert.AreEqual("000e1234", _lastArgs[0]);

            _lastEvent = null;
            Assert.AreEqual("TestWarning", result.GetValue("TestWarning", 3579));
            Assert.IsNotNull(_lastEvent);
            Assert.AreEqual("Application", _lastLog.Log);
            Assert.AreEqual("YourAppName", _lastLog.Source);
            Assert.AreEqual(0x0F, _lastEvent.CategoryId);
            Assert.AreEqual(EventLogEntryType.Warning, _lastEvent.EntryType);
            Assert.AreEqual(0x810200fbL, _lastEvent.InstanceId);
            Assert.AreEqual(1, _lastArgs.Length);
            Assert.AreEqual("3579", _lastArgs[0]);

            _lastEvent = null;
            COMException error = (COMException)result.CreateException("TestException", "error", 1234);
            Assert.AreEqual("TestException-error-1234", error.Message);
            Assert.AreEqual(unchecked((int)0xe1020005), error.ErrorCode);//5 was auto-assigned by NextMessageId
            Assert.AreEqual("http://mydomain/errorcodes.aspx?id=e1020005", error.HelpLink);

            Assert.IsNotNull(_lastEvent);
            Assert.AreEqual("Application", _lastLog.Log);
            Assert.AreEqual("YourAppName", _lastLog.Source);
            Assert.AreEqual(0x0F, _lastEvent.CategoryId);
            Assert.AreEqual(EventLogEntryType.Error, _lastEvent.EntryType);
            Assert.AreEqual(0xC1020005L, _lastEvent.InstanceId);
            Assert.AreEqual(2, _lastArgs.Length);
            Assert.AreEqual("error", _lastArgs[0]);
            Assert.AreEqual("1234", _lastArgs[1]);
        }
    }
}
