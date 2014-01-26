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
using System.Reflection;
using NUnit.Framework;

namespace CSharpTest.Net.GeneratorsTest
{
    [TestFixture]
    public class TestResXExceptions
    {
        [Test]
        public void TestBasicException()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestException", "TestValue");
            TestResourceResult result = builder.Compile();
            Exception e = result.CreateException("TestException");

            Assert.AreEqual("TestValue", e.Message);
        }
        [Test, ExpectedException("TestNs.TestException", ExpectedMessage = "(Test Exception:Message)")]
        public void TestBasicAssertFails()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestException", "(Test Exception:{0})", "(string message) : ArgumentException");
            TestResourceResult result = builder.Compile();
            try
            {
                result.Assert("TestException", false, "Message");
            }
            catch (ArgumentException ae)
            {
                Assert.AreEqual("TestNs.TestException", ae.GetType().FullName);
                throw;
            }
        }
        [Test]
        public void TestBasicAssertPasses()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestException", "(Test Exception:{0})", "(string message) : ArgumentException");
            TestResourceResult result = builder.Compile();
            result.Assert("TestException", true, "Message");
        }
        [Test, ExpectedException(typeof(MissingMethodException))]
        public void TestBasicArgumentsRequired()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestException", "(Test Exception:{0})");
            TestResourceResult result = builder.Compile();
            result.CreateException("TestException");
        }
        [Test]
        public void TestBasicTypeArgumentsInName()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestException(string message)", "(Test:{0})");
            TestResourceResult result = builder.Compile();
            Exception e = result.CreateException("TestException", "Message");
            Assert.AreEqual("(Test:Message)", e.Message);
        }
        [Test]
        public void TestBasicTypeArgumentOverloads()
        {
            Exception e;
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestException", "(Test)");
            builder.Add("TestException(string message)", "(Test:{0})");
            builder.Add("TestException(double value)", "(Test:{0:n2})");
            TestResourceResult result = builder.Compile();

            e = result.CreateException("TestException");
            Assert.AreEqual("(Test)", e.Message);

            e = result.CreateException("TestException", "Message");
            Assert.AreEqual("(Test:Message)", e.Message);

            e = result.CreateException("TestException", 1.2);
            Assert.AreEqual("(Test:1.20)", e.Message);
        }
        [Test]
        public void TestDerivedException()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("BaseException", "{0}");
            builder.Add("TestException(string message)", "(Test:{0})", ": BaseException");
            TestResourceResult result = builder.Compile();

            Exception e = result.CreateException("TestException", "Message");
            Assert.AreEqual("TestNs.BaseException", e.GetType().BaseType.FullName);
            Assert.AreEqual("(Test:Message)", e.Message);
        }
        [Test]
        public void TestMemberNotExposed()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");
            builder.Add("TestException(int extraData)", "(Test:{0})");
            TestResourceResult result = builder.Compile();
            Exception e = result.CreateException("TestException", 42);
            Assert.IsNull(e.GetType().GetProperty("extradata", BindingFlags.Public | BindingFlags.GetProperty | 
                                                  BindingFlags.Instance | BindingFlags.IgnoreCase));
        }
        [Test]
        public void TestDerivedExceptionWithInterface()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            //The parameter starting with a capital 'E'xtraData denotes that this should be stored and a property accessor exposted.
            builder.Add("TestException(int ExtraData)", "(Test:{0})", ": ApplicationException, CSharpTest.Net.GeneratorsTest.IHaveExtraData<int>");
            TestResourceResult result = builder.Compile();

            Exception e = result.CreateException("TestException", 42);
            Assert.AreEqual(typeof(ApplicationException), e.GetType().BaseType);
            Assert.IsTrue(e is IHaveExtraData<int>, "Interface not found: IHaveExtraData<int>");
            Assert.AreEqual("(Test:42)", e.Message);
            Assert.AreEqual(42, ((IHaveExtraData<int>)e).ExtraData);
        }
        [Test]
        public void TestWithInnerException()
        {
            TestResourceBuilder builder = new TestResourceBuilder("TestNs", "ResXClass");

            builder.Add("TestException()", "(Test)");
            TestResourceResult result = builder.Compile();

            Exception inner = new Exception("My inner exception");
            Exception e = result.CreateException("TestException", inner);

            Assert.AreEqual(inner, e.InnerException);
        }
    }
    public interface IHaveExtraData<T>
    {
        T ExtraData { get; }
    }
}
