#region Copyright 2010 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using CSharpTest.Net.Generators;
using NUnit.Framework;

namespace CSharpTest.Net.GeneratorsTest
{
    [TestFixture]
    public class CsWriterTest
    {
        [Test]
        public void TestBasicWriter()
        {
            using (CsWriter w = new CsWriter())
            {
                w.AddNamespaces("System");
                using(w.WriteNamespace("CSharpTest"))
                using (w.WriteClass("class Net"))
                { }

                string msg = w.ToString();
                Assert.IsTrue(msg.Contains("using System;"));
                Assert.IsTrue(msg.Contains("DebuggerNonUserCodeAttribute"));
                Assert.IsTrue(msg.Contains("CompilerGenerated"));
                Assert.IsTrue(msg.Contains("class Net"));
            }
        }
    }
}
