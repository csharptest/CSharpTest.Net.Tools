#region Copyright 2010-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.IO;
using NUnit.Framework;

namespace CSharpTest.Net.GeneratorsTest
{
    [TestFixture]
    public class TestResXGenerator
    {
        [Test]
        public void TestSimpleString()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestString", "TestValue");
            TestResourceResult result = builder.Compile();
            Assert.AreEqual("TestValue", result.GetValue("TestString"));
        }
        [Test]
        public void TestNonFormatString()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestString", "Test{Value}");
            TestResourceResult result = builder.Compile();
            Assert.AreEqual("Test{Value}", result.GetValue("TestString"));
        }
        [Test]
        public void TestFormatStringAnonymousArg()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestString", "Test{0}");
            TestResourceResult result = builder.Compile();
            Assert.AreEqual("TestX123X", result.GetValue("TestString", "X123X"));
        }
        [Test]
        public void TestFormatStringTypedArgComments()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestInt32", "Test-{0}", "(int value)");
            TestResourceResult result = builder.Compile();
            Assert.AreEqual("Test-42", result.GetValue("TestInt32", 42));
        }
        [Test]
        public void TestFormatStringTypedArgName()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestDouble(double value)", "Test-{0:n2}", "");
            TestResourceResult result = builder.Compile();
            Assert.AreEqual("Test-42.24", result.GetValue("TestDouble", 42.24));
        }
        [Test]
        public void TestFormatStringTypedArgConflictWinner()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");
            //if both name and comments specify arguments, the name wins
            builder.Add("TestString(string value)", "Test-{0:n2}", "(int value)");
            TestResourceResult result = builder.Compile();
            Assert.AreEqual("Test-Value", result.GetValue("TestString", "Value"));
        }
        [Test, ExpectedException(typeof(System.ApplicationException), ExpectedMessage = "One or more String.Format operations failed.")]
        public void TestInvalidFormatString()
        {
            TextWriter serr = Console.Error;
            try
            {
                Console.SetError(TextWriter.Null);
                TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");
                builder.Add("TestString(string value)", "Test-{0:n2} {}", "(int value)");
                builder.Compile();
            }
            finally
            {
                Console.SetError(serr);
            }
        }
        [Test]
        public void TestFormatStringTypedArgOverloads()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("Test(string value)", "Test-{0}");
            builder.Add("Test(int value)", "Test-{0}");
            builder.Add("Test(double value)", "Test-{0:n3}");
            builder.Add("Test(System.Version value)", "Test-{0}");
            builder.Add("Test(System.Uri value)", "Test-{0}");

            TestResourceResult result = builder.Compile();
            Assert.AreEqual("Test-Value", result.GetValue("Test", "Value"));
            Assert.AreEqual("Test-123", result.GetValue("Test", 123));
            Assert.AreEqual("Test-123.321", result.GetValue("Test", 123.321));
            Assert.AreEqual("Test-1.2.3.4", result.GetValue("Test", new Version(1, 2, 3, 4)));
            Assert.AreEqual("Test-http://csharptest.net/blog", result.GetValue("Test", new Uri("http://csharptest.net/blog", UriKind.Absolute)));
        }
    }
}
