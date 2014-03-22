#region Copyright 2010-2011 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Resources;
using CSharpTest.Net.IO;
using CSharpTest.Net.Utils;
using Microsoft.CSharp;

namespace CSharpTest.Net.GeneratorsTest
{
    class TestResourceBuilder
    {
        struct Item { public string Name, Comment; public object Value; }
        private readonly List<Item> _items = new List<Item>();
        public TestResourceBuilder(string nameSpace, string className)
        {
            ResxNamespace = Namespace = nameSpace;
            ClassName = className;
        }

        public readonly string Namespace;
        public readonly string ClassName;
        public string ResxNamespace;
        public bool Public = true;
        public bool Partial = true;
        public bool Test = true;

        public void Add(string name, object value) { Add(name, value, ""); }
        public void Add(string name, object value, string comment)
        {
            Item i = new Item();
            i.Name = name;
            i.Value = value;
            i.Comment = comment;
            _items.Add(i);
        }

        public TestResourceResult Compile()
        {
            string ignored1, ignored2;
            return Compile(String.Empty, out ignored1, out ignored2);
        }

        public void BuildResX(string outputPath)
        {
            using (ResXResourceWriter w2 = new ResXResourceWriter(outputPath))
            {
                foreach (Item item in _items)
                {
                    ResXDataNode node = new ResXDataNode(item.Name, item.Value);
                    node.Comment = item.Comment;
                    w2.AddResource(item.Name, node);
                }
            }
        }

        public void BuildResource(string outputPath)
        {
            using (ResourceWriter w1 = new ResourceWriter(outputPath))
            {
                foreach (Item item in _items)
                    w1.AddResource(item.Name, item.Value);
            }
        }

        public TestResourceResult Compile(string cmdline, out string generatedCode, out string modifiedResX)
        {
            using (TempFile resx = TempFile.FromExtension(".resx"))
            using (TempFile resources = TempFile.Attach(Path.ChangeExtension(resx.TempPath, ".resources")))
            using (TempFile rescs = TempFile.Attach(Path.ChangeExtension(resx.TempPath, ".Designer.cs")))
            {
                BuildResX(resx.TempPath);
                BuildResource(resources.TempPath);

                TextWriter stdout = Console.Out;
                try
                {
                    using (TextWriter tw = new StreamWriter(rescs.Open()))
                    {
                        Console.SetOut(tw);
                        Generators.Commands.ResX(resx.TempPath, Namespace, ClassName, ResxNamespace, Public, Partial, Test, false, typeof(ApplicationException).FullName);
                    }

                    generatedCode = rescs.ReadAllText();
                    modifiedResX = resx.ReadAllText();
                }
                finally
                { Console.SetOut(stdout); }

                Assembly asm = Compile(
                    String.Format("/reference:{0} /resource:{1},{2}.{3}.resources {4}", GetType().Assembly.Location, resources.TempPath, ResxNamespace, ClassName, cmdline),
                    rescs.TempPath);

                return new TestResourceResult(asm, Namespace, ClassName);
            }
        }

        private static Assembly Compile(string options, params string[] files)
        {
            using (TempFile asm = TempFile.FromExtension(".dll"))
            {
                asm.Delete();

                CSharpCodeProvider csc = new CSharpCodeProvider();
                CompilerParameters args = new CompilerParameters();
                args.GenerateExecutable = false;
                args.IncludeDebugInformation = false;
                args.ReferencedAssemblies.Add("System.dll");
                args.OutputAssembly = asm.TempPath;
                args.CompilerOptions = options;
                CompilerResults results = csc.CompileAssemblyFromFile(args, files);

                StringWriter sw = new StringWriter();
                foreach (CompilerError ce in results.Errors)
                {
                    if (ce.IsWarning) continue;
                    string msg = String.Format("{0}({1},{2}: error {3}: {4}", ce.FileName, ce.Line, ce.Column,
                                               ce.ErrorNumber, ce.ErrorText);
                    System.Diagnostics.Trace.WriteLine(msg);
                    sw.WriteLine(msg);
                }
                string errorText = sw.ToString();
                if (errorText.Length > 0)
                    throw new ApplicationException(errorText);

                if (!asm.Exists)
                    throw new FileNotFoundException(new FileNotFoundException().Message, asm.TempPath);

                return Assembly.Load(asm.ReadAllBytes());
            }
        }

        public static string FindExe(string exeName)
        {
            try { return Processes.ProcessRunner.FindFullPath(exeName); }
            catch { }

            foreach (string env in new string[] { "ProgramFiles(x86)", "ProgramFiles", "ProgramW6432" })
            {
                string baseDir = Environment.GetEnvironmentVariable(env, EnvironmentVariableTarget.Process);
                if (String.IsNullOrEmpty(baseDir))
                    continue;

                foreach (string test in Directory.GetDirectories(baseDir, "Microsoft*", SearchOption.TopDirectoryOnly))
                    foreach (string path in Directory.GetFiles(test, exeName, SearchOption.AllDirectories))
                        return path;
            }
            throw new FileNotFoundException("Unable to locate mc.exe", exeName);
        }
    }
}