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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace CSharpTest.Net.GeneratorsTest
{
    [TestFixture]
    public class TestGeneratorProgram
    {
        [Test]
        public void TestProgramMain()
        {
            Assembly a = typeof(Generators.Commands).Assembly;
            StringWriter sout = new StringWriter(), serr = new StringWriter();
            TextWriter saveout = Console.Out;
            Console.SetOut(sout);
            TextWriter saveerr = Console.Error;
            Console.SetError(serr);
            try
            {
                AppDomain.CurrentDomain.ExecuteAssembly(a.Location, 
#if NET20 || NET35
                    AppDomain.CurrentDomain.Evidence,
#endif
                    new string[] { "Config" });
            }
            finally
            {
                Console.SetOut(saveout);
                Console.SetError(saveerr);
            }

            string result = new StreamReader(a.GetManifestResourceStream(typeof(Generators.Commands).Namespace + ".CmdTool.config")).ReadToEnd();
            Assert.AreEqual(sout.ToString().Trim(), result.Trim());
        }
    }
}
