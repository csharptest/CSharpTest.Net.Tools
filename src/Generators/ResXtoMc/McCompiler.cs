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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Resources.Tools;
using System.Text.RegularExpressions;
using CSharpTest.Net.Collections;
using CSharpTest.Net.IO;
using CSharpTest.Net.Processes;
using Microsoft.CSharp;

namespace CSharpTest.Net.Generators.ResXtoMc
{
    class McCompiler
    {
        private static readonly CSharpCodeProvider Csharp = new CSharpCodeProvider();
        private readonly McFileGenerator _genInfo;
        private readonly string _mcFile;
        private readonly VersionInfoBuilder _verInfo;
        private string _tempDir;
        private string _toolsBin;
        private string _namespace;
        private string _iconFile, _manifestFile, _resourceScript;

        public McCompiler(McFileGenerator genInfo, string mcFile)
        {
            _genInfo = genInfo;
            _mcFile = mcFile;
            _verInfo = new VersionInfoBuilder();

            string[] ns = Path.GetFileNameWithoutExtension(_mcFile).Trim('.').Split('.');
            for (int i = 0; i < ns.Length; i++)
                ns[i] = StronglyTypedResourceBuilder.VerifyResourceName(ns[i], Csharp);
            _namespace = String.Join(".", ns);
        }

        public VersionInfoBuilder VersionInfo { get { return _verInfo; } }
        public string IntermediateFiles { get { return _tempDir ?? Path.GetTempPath(); } set { _tempDir = value; } }
        public string ToolsBin { set { _toolsBin = value; } }
        public string Namespace { get { return _namespace; } set { _namespace = value; } }
        public string IconFile { get { return _iconFile; } set { _iconFile = value; } }
        public string ManifestFile { get { return _manifestFile; } set { _manifestFile = value; } }
        public string ResourceScript { get { return _resourceScript; } set { _resourceScript = value; } }

        public void CreateResFile(string output, out string resFile)
        {
            int code;
            string mcexe = "mc.exe";
            if (!String.IsNullOrEmpty(_toolsBin)) mcexe = Path.Combine(_toolsBin, mcexe);
            using (ProcessRunner mc = new ProcessRunner(mcexe, "-U", "{0}", "-r", "{1}", "-h", "{1}"))
            using (StringWriter stdio = new StringWriter())
            {
                mc.OutputReceived += delegate(object o, ProcessOutputEventArgs e) { stdio.WriteLine(e.Data); };
                if (0 != (code = mc.RunFormatArgs(_mcFile, IntermediateFiles)))
                {
                    Trace.WriteLine(stdio.ToString());
                    throw new ApplicationException(String.Format("mc.exe failed ({0:x}):\r\n{1}", code, stdio));
                }
            }

            string rcFile = Path.Combine(IntermediateFiles, Path.ChangeExtension(Path.GetFileName(_mcFile), ".rc"));
            VersionInfo.AppendToRc(Path.GetFileName(output), rcFile);
            if (!String.IsNullOrEmpty(IconFile) && File.Exists(IconFile))
                File.AppendAllText(rcFile, String.Format("\r\n1 ICON \"{0}\"\r\n", IconFile.Replace(@"\", @"\\")));
            if (!String.IsNullOrEmpty(ManifestFile) && File.Exists(ManifestFile))
                File.AppendAllText(rcFile, String.Format("\r\n1 24 \"{0}\"\r\n", ManifestFile.Replace(@"\", @"\\")));
            if (!String.IsNullOrEmpty(ResourceScript))
                File.AppendAllText(rcFile, "\r\n" + ResourceScript + "\r\n");

            string rcexe = "rc.exe";
            if (!String.IsNullOrEmpty(_toolsBin)) rcexe = Path.Combine(_toolsBin, rcexe);
            using (ProcessRunner rc = new ProcessRunner(rcexe, "{0}"))
            using (StringWriter stdio = new StringWriter())
            {
                rc.OutputReceived += delegate(object o, ProcessOutputEventArgs e) { stdio.WriteLine(e.Data); Trace.WriteLine(e.Data, rcexe); };
                if (0 != (code = rc.RunFormatArgs(rcFile)))
                    throw new ApplicationException(String.Format("mc.exe failed ({0:x}):\r\n{1}", code, stdio));
            }

            resFile = Path.ChangeExtension(rcFile, ".res");
        }

        public void Compile(string output, string cscOptions)
        {
            string resFile;
            CreateResFile(output, out resFile);
            string hdrFile = Path.ChangeExtension(resFile, ".h");
            RunCsc(output, resFile, hdrFile, cscOptions);
        }

        public void RunCsc(string output, string win32res, string header, string cscOptions)
        {
            using (DisposingList<TempFile> files = new DisposingList<TempFile>())
            {
                TempFile asm = TempFile.FromExtension(".dll");
                files.Add(asm);

                TempFile assemblyInfo = TempFile.FromExtension(".cs");
                files.Add(assemblyInfo);
                assemblyInfo.WriteAllText(VersionInfo.AssemblyInfoCode);

                TempFile installer = TempFile.FromExtension(".cs");
                files.Add(installer);
                installer.WriteAllText(CreateInstaller());

                TempFile constants = TempFile.FromExtension(".cs");
                files.Add(constants);
                constants.WriteAllText(CreateConstants(header));

                CSharpCodeProvider csc = new CSharpCodeProvider();
                CompilerParameters args = new CompilerParameters();
                args.GenerateExecutable = false;
                args.IncludeDebugInformation = false;
                args.OutputAssembly = asm.TempPath;
                args.Win32Resource = win32res;
                args.CompilerOptions = cscOptions;
                args.ReferencedAssemblies.Add("System.dll");
                args.ReferencedAssemblies.Add("System.Configuration.Install.dll");
                CompilerResults results = csc.CompileAssemblyFromFile(args,
                    assemblyInfo.TempPath,
                    installer.TempPath,
                    constants.TempPath);

                StringWriter sw = new StringWriter();
                foreach (CompilerError ce in results.Errors)
                {
                    if (ce.IsWarning) continue;
                    String line = String.Format("{0}({1},{2}): error {3}: {4}", ce.FileName, ce.Line, ce.Column, ce.ErrorNumber, ce.ErrorText);
                    Trace.WriteLine(line);
                    sw.WriteLine(line);
                }
                string errorText = sw.ToString();

                try
                {
                    if (errorText.Length > 0)
                        throw new ApplicationException(errorText);

                    if (!asm.Exists)
                        throw new FileNotFoundException(new FileNotFoundException().Message, asm.TempPath);
                }
                catch
                {
                    GC.KeepAlive(files);
                    throw;
                }
                File.Copy(asm.TempPath, output, true);
            }
        }

        public string CreateInstaller()
        {
            if (_genInfo == null)
                return String.Empty;

            Dictionary<string, string> sources = _genInfo.EventSources;
            Dictionary<string, bool> logNames = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (string val in sources.Values)
                logNames[val] = val != "Application" && val != "Security" && val != "Setup" && val != "System";

            int maxCategory = _genInfo.Categories.Count;

            using (CsWriter code = new CsWriter())
            {
                using (code.WriteNamespace(Namespace))
                {
                    code.WriteSummaryXml("Installer class for the Event Log messages");
                    code.WriteLine("[global::System.ComponentModel.RunInstaller(true)]");
                    using (code.WriteClass("public class Installer : System.Configuration.Install.Installer"))
                    {
                        foreach (KeyValuePair<string, string> kv in sources)
                            code.WriteLine("readonly System.Diagnostics.EventLogInstaller _install{0}{1};",
                                           kv.Value, kv.Key);
                        code.WriteLine();
                        code.WriteSummaryXml("Constructs the installer for the Event Log");
                        using (code.WriteBlock("public Installer()"))
                        {
                            foreach (KeyValuePair<string, string> kv in sources)
                            {
                                string name = String.Format("_install{0}{1}", kv.Value, kv.Key);
                                code.WriteLine("{0} = new System.Diagnostics.EventLogInstaller();", name);
                                code.WriteLine("{0}.Log = @\"{1}\";", name, kv.Value.Replace("\"", "\"\""));
                                code.WriteLine("{0}.Source = @\"{1}\";", name, kv.Key.Replace("\"", "\"\""));
                                code.WriteLine("{0}.CategoryCount = {1};", name, maxCategory);
                                code.WriteLine(
                                    "{0}.UninstallAction = System.Configuration.Install.UninstallAction.Remove;", name);
                                code.WriteLine("Installers.Add({0});", name);
                                code.WriteLine();
                            }
                        }
                        code.WriteLine();
                        code.WriteSummaryXml("Customizes the MessageResourceFile durring installation");
                        using (code.WriteBlock("public override void Install(System.Collections.IDictionary state)"))
                        {
                            foreach (KeyValuePair<string, string> kv in sources)
                                code.WriteLine(
                                    "_install{0}{1}.CategoryResourceFile = _install{0}{1}.MessageResourceFile =",
                                    kv.Value, kv.Key);
                            code.WriteLine(
                                "    System.IO.Path.GetFullPath(Context.Parameters[\"assemblypath\"].Trim('\"'));");
                            code.WriteLine();
                            code.WriteLine("base.Install(state);");
                            foreach (KeyValuePair<string, bool> kv in logNames)
                            {
                                if (!kv.Value) continue;//do not configure default logs

                                code.WriteLine();

                                using (code.WriteBlock("using (System.Diagnostics.EventLog log = new System.Diagnostics.EventLog(@\"{0}\", \".\"))", kv.Key.Replace("\"", "\"\"")))
                                {
                                    code.WriteLine("log.MaximumKilobytes = 1024 * 10;");
                                    code.WriteLine("log.ModifyOverflowPolicy(System.Diagnostics.OverflowAction.OverwriteAsNeeded, 30);");
                                }
                            }
                        }
                    }
                }
                return code.ToString();
            }
        }

        public string CreateConstants(string header)
        {
            string content = File.ReadAllText(header);

            Regex pattern = new Regex(@"^#define\s+(?<id>[\w_]*)\s+\(?(?:\(long\))?(?<value>.*?)\)?\s*?$",
                                      RegexOptions.Multiline | RegexOptions.IgnoreCase);

            Dictionary<string, string> defines = new Dictionary<string, string>();
            foreach (Match m in pattern.Matches(content))
                if (!m.Groups["id"].Value.StartsWith("CATEGORY_",StringComparison.Ordinal) && 
                    !m.Groups["id"].Value.StartsWith("FACILITY_",StringComparison.Ordinal))
                defines.Add(m.Groups["id"].Value, m.Groups["value"].Value);


            using (CsWriter code = new CsWriter())
            {
                code.WriteLine("// ReSharper disable InconsistentNaming");
                code.WriteLine("#pragma warning disable 1591 //disable missing xml comments");
                code.WriteLine();

                using (code.WriteNamespace(Namespace))
                {
                    if (_genInfo != null)
                    {
                        Dictionary<int, string> items = _genInfo.Facilities;
                        using (code.WriteBlock("public enum Facilities"))
                        {
                            List<int> sorted = new List<int>(items.Keys);
                            sorted.Sort();
                            foreach (int key in sorted)
                                code.WriteLine("{0} = {1},", StronglyTypedResourceBuilder.VerifyResourceName(items[key], Csharp), key);
                        }
                        code.WriteLine();
                        items = _genInfo.Categories;
                        using (code.WriteBlock("public enum Categories"))
                        {
                            List<int> sorted = new List<int>(items.Keys);
                            sorted.Sort();
                            foreach (int key in sorted)
                                code.WriteLine("{0} = {1},", StronglyTypedResourceBuilder.VerifyResourceName(items[key], Csharp), key);
                        }
                        code.WriteLine();
                    }

                    using (code.WriteBlock("public enum HResults : long"))
                    {
                        List<string> sorted = new List<string>(defines.Keys);
                        sorted.Sort();
                        foreach (string key in sorted)
                            code.WriteLine("{0} = {1},", StronglyTypedResourceBuilder.VerifyResourceName(key, Csharp), defines[key]);
                    }
                }
                return code.ToString();
            }
        } 
    }
}
