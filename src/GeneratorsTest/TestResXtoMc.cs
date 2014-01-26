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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Resources;
using System.Text.RegularExpressions;
using CSharpTest.Net.IO;
using CSharpTest.Net.Processes;
using CSharpTest.Net.Utils;
using NUnit.Framework;

namespace CSharpTest.Net.GeneratorsTest
{
    [TestFixture]
    public class TestResXtoMc
    {
        [Test]
        public void TestEmptyResXToMc()
        {
            TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder1.Add("Ignored_Lack_of_MessageId", "");

            using (TempFile mc = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                {
                    builder1.BuildResX(resx1.TempPath);
                    Generators.Commands.ResXtoMc(mc.TempPath, new string[] { resx1.TempPath });
                }

                string contents = mc.ReadAllText();
                string[] lines = contents.TrimEnd().Split('\n');
                //no messages:
                Assert.AreEqual(";// MESSAGES", lines[lines.Length - 1].Trim());
            }
        }
        [Test]
        public void TestSimpleStringResXToMc()
        {
            TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder1.Add("Value1", "value for 1", "#MessageId = 1");
            TestResourceBuilder builder2 = new TestResourceBuilder("TestNamespace", "TestResXClass2");
            builder2.Add("Value2", "value for 2", "#MessageId = 2");

            using (TempFile mc = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                using (TempFile resx2 = TempFile.FromExtension(".resx"))
                {
                    builder1.BuildResX(resx1.TempPath);
                    builder2.BuildResX(resx2.TempPath);
                    Generators.Commands.ResXtoMc(mc.TempPath, new string[] { resx1.TempPath, resx2.TempPath });
                }

                string contents = mc.ReadAllText();

                Assert.IsTrue(contents.Contains("\r\nvalue for 1\r\n.\r\n"));
                Assert.IsTrue(contents.Contains("\r\nvalue for 2\r\n.\r\n"));
            }
        }
        [Test]
        public void TestResXToMcWithCategoryFacilityAndSource()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNamespace", "TestResXClass1");

            //The name of the event source to register, can be qualified with log: "Log-Name/Event-Source"
            builder.Add(".EventSource", "Application/YourAppName");

            //OPTIONAL (default=0): The category id and name for the category of the message, should be unique for this ResX file 
            builder.Add(".EventCategory", 0x0F, "MyCategory");

            //OPTIONAL (default=0): The facility code (256-2047) and name to define for these messages
            builder.Add(".Facility", 258, "MyFacility");
            builder.Add("SimpleText", "Message Text", "#MessageId=1");

            using (TempFile mc = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                {
                    builder.BuildResX(resx1.TempPath);
                    Generators.Commands.ResXtoMc(mc.TempPath, new string[] { resx1.TempPath });
                }

                string contents = mc.ReadAllText();
                Assert.IsTrue(contents.Contains("\r\nMessage Text\r\n."));

                //Facility defined
                Assert.IsTrue(new Regex(@"FACILITY_MYFACILITY\s*=\s*0x102").IsMatch(contents));
                //Category defined
                Assert.IsTrue(new Regex(@"SymbolicName\s*=\s*CATEGORY_MYCATEGORY").IsMatch(contents) && contents.Contains("\r\nMyCategory\r\n.\r\n"));
                //Facility used
                Assert.IsTrue(new Regex(@"Facility\s*=\s*FACILITY_MYFACILITY").IsMatch(contents));
                //Message defined
                Assert.IsTrue(contents.Contains("\r\nMessage Text\r\n."));
            }
        }
        [Test]
        public void TestFormatResXToMc()
        {
            TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder1.Add("WhatsUp(string s, int i)", "Format!%0\n\tstring={0},\n.\r\n\tnumber={1}.", "#messageId=23");

            using (TempFile mc = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                {
                    builder1.BuildResX(resx1.TempPath);
                    Generators.Commands.ResXtoMc(mc.TempPath, new string[] { resx1.TempPath });
                }

                string contents = mc.ReadAllText();
                Assert.IsTrue(contents.Contains(@"
Format%!%%0%n
	string=%1,%n
%.%n
	number=%2.
.
"));
            }
        }
        [Test]
        public void TestFormatWithSuffixResXToMc()
        {
            TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder1.Add("WhatsUp(string s, int i)", "Format!%0\n\tstring={0},\n.\r\n\tnumber={1}.", "#messageId=23");
            builder1.Add(".EventMessageFormat", "Suffix\nTest {0} Format");

            using (TempFile mc = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                {
                    builder1.BuildResX(resx1.TempPath);
                    Generators.Commands.ResXtoMc(mc.TempPath, new string[] { resx1.TempPath });
                }

                string contents = mc.ReadAllText();
                Assert.IsTrue(contents.Contains(@"
Format%!%%0%n
	string=%2,%n
%.%n
	number=%3.%n
Suffix%n
Test %1 Format
"));
            }
        }
        [Test]
        public void TestBuildMcFromResX()
        {
            TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder1.Add("Testing", "test value 1", "#MessageId=42");

            using (TempDirectory intermediateFiles = new TempDirectory())
            using (TempFile mctxt = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                {
                    builder1.BuildResX(resx1.TempPath);
                    Generators.Commands.ResXtoMc(mctxt.TempPath, new string[] { resx1.TempPath });
                }

                string mcexe = TestResourceBuilder.FindExe("mc.exe");

                using (ProcessRunner mc = new ProcessRunner(mcexe, "-U", "{0}", "-r", "{1}", "-h", "{1}"))
                {
                    mc.OutputReceived += delegate(object o, ProcessOutputEventArgs e) { Trace.WriteLine(e.Data, mcexe); };
                    Assert.AreEqual(0, mc.RunFormatArgs(mctxt.TempPath, intermediateFiles.TempPath), "mc.exe failed.");
                }

                string rcfile = Path.Combine(intermediateFiles.TempPath, Path.GetFileNameWithoutExtension(mctxt.TempPath) + ".rc");
                Assert.IsTrue(File.Exists(rcfile));
                Assert.IsTrue(File.Exists(Path.ChangeExtension(rcfile, ".h")));
                Assert.IsTrue(File.Exists(Path.Combine(intermediateFiles.TempPath, "MSG00409.bin")));

                string rcexe = Path.Combine(Path.GetDirectoryName(mcexe), "rc.exe");
                if (!File.Exists(rcexe))
                    rcexe = TestResourceBuilder.FindExe("rc.exe");

                using (ProcessRunner rc = new ProcessRunner(rcexe, "{0}"))
                {
                    rc.OutputReceived += delegate(object o, ProcessOutputEventArgs e) { Trace.WriteLine(e.Data, rcexe); };
                    Assert.AreEqual(0, rc.RunFormatArgs(rcfile), "rc.exe failed.");
                }

                string resfile = Path.ChangeExtension(rcfile, ".res");
                Assert.IsTrue(File.Exists(resfile));
                Assert.IsTrue(File.ReadAllText(resfile).Contains("\0t\0e\0s\0t\0 \0v\0a\0l\0u\0e\0 \01"));
            }
        }

        [Test, ExpectedException(typeof(ApplicationException))]
        public void TestDuplicateHResult()
        {
            TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder1.Add("TestName", "value", "#messageId=23,Severity=Error");
            builder1.Add("TestException", "value", "#messageId=23");

            using (TempFile mc = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                {
                    builder1.BuildResX(resx1.TempPath);
                    Generators.Commands.ResXtoMc(mc.TempPath, new string[] { resx1.TempPath });
                }
            }
        }
        [Test, ExpectedException(typeof(ApplicationException))]
        public void TestDuplicateNameInFiles()
        {
            TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder1.Add("DuplicateName", "value", "#messageId=23");
            TestResourceBuilder builder2 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder2.Add("DuplicateName", "value", "#messageId=24");

            using (TempFile mc = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                using (TempFile resx2 = TempFile.FromExtension(".resx"))
                {
                    builder1.BuildResX(resx1.TempPath);
                    builder2.BuildResX(resx2.TempPath);
                    Generators.Commands.ResXtoMc(mc.TempPath, new string[] { resx1.TempPath, resx2.TempPath });
                }
            }
        }
        [Test]
        public void TestDuplicateCategorySameIdAndName()
        {
            TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder1.Add(".EventCategory", 1, "MyCategory");
            builder1.Add("name1", "value", "#messageId=23");
            TestResourceBuilder builder2 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder2.Add(".EventCategory", 1, "MyCategory");
            builder2.Add("name2", "value", "#messageId=24");

            using (TempFile mc = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                using (TempFile resx2 = TempFile.FromExtension(".resx"))
                {
                    builder1.BuildResX(resx1.TempPath);
                    builder2.BuildResX(resx2.TempPath);
                    Generators.Commands.ResXtoMc(mc.TempPath, new string[] { resx1.TempPath, resx2.TempPath });
                }
            }
        }
        [Test, ExpectedException(typeof(ApplicationException))]
        public void TestDuplicateCategorySameIdDifferentName()
        {
            TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder1.Add(".EventCategory", 1, "MyCategory");
            builder1.Add("name1", "value", "#messageId=23");
            TestResourceBuilder builder2 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
            builder2.Add(".EventCategory", 1, "NotMyCategory");
            builder2.Add("name2", "value", "#messageId=24");

            using (TempFile mc = TempFile.FromExtension(".mc"))
            {
                using (TempFile resx1 = TempFile.FromExtension(".resx"))
                using (TempFile resx2 = TempFile.FromExtension(".resx"))
                {
                    builder1.BuildResX(resx1.TempPath);
                    builder2.BuildResX(resx2.TempPath);
                    Generators.Commands.ResXtoMc(mc.TempPath, new string[] { resx1.TempPath, resx2.TempPath });
                }
            }
        }
    }
}
