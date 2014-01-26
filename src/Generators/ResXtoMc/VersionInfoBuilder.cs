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
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CSharpTest.Net.Collections;
using CSharpTest.Net.Html;
using CSharpTest.Net.IO;
using CSharpTest.Net.Utils;
using Microsoft.CSharp;

namespace CSharpTest.Net.Generators.ResXtoMc
{
    /// <summary>
    /// Given an assembly, a project that contains a file named AssemblyInfo.cs, or an AssemblyInfo.cs file this
    /// produces both a new AssemblyInfo.cs as well as the Win32 file version resource.
    /// </summary>
    class VersionInfoBuilder
    {
        private string _assemblyInfo;
        private FileVersionInfo _fileVersion;

        public void AppendToRc(string filename, string rcFile)
        {
            if (_fileVersion == null)
                return;

            string rcText = "";
            using (StringWriter sw = new StringWriter())
            {
                sw.WriteLine();
                sw.WriteLine();
                sw.WriteLine("/////////////////////////////////////////////////////////////////////////////");
                sw.WriteLine("//");
                sw.WriteLine("// Version");
                sw.WriteLine("//");
                sw.WriteLine("");
                sw.WriteLine("1 VERSIONINFO");
                sw.WriteLine(" FILEVERSION {0},{1},{2},{3}", _fileVersion.FileMajorPart, _fileVersion.FileMinorPart, _fileVersion.FileBuildPart, _fileVersion.FilePrivatePart);
                sw.WriteLine(" PRODUCTVERSION {0},{1},{2},{3}", _fileVersion.ProductMajorPart, _fileVersion.ProductMinorPart, _fileVersion.ProductBuildPart, _fileVersion.ProductPrivatePart);
                sw.WriteLine(" FILEFLAGSMASK 0x3fL");
                sw.WriteLine(" FILEFLAGS 0x0L");
                sw.WriteLine(" FILEOS 0x4L");
                sw.WriteLine(" FILETYPE 0x1L");
                sw.WriteLine(" FILESUBTYPE 0x0L");
                sw.WriteLine("BEGIN");
                sw.WriteLine("    BLOCK \"StringFileInfo\"");
                sw.WriteLine("    BEGIN");
                sw.WriteLine("        BLOCK \"000004b0\"");
                sw.WriteLine("        BEGIN");
                sw.WriteLine("            VALUE \"Assembly Version\", \"{0}\"", Escape(_fileVersion.FileVersion));
                sw.WriteLine("            VALUE \"CompanyName\", \"{0}\"", Escape(_fileVersion.CompanyName));
                sw.WriteLine("            VALUE \"FileDescription\", \"{0}\"", Escape(_fileVersion.FileDescription));
                sw.WriteLine("            VALUE \"FileVersion\", \"{0}\"", Escape(_fileVersion.FileVersion));
                sw.WriteLine("            VALUE \"InternalName\", \"{0}\"", Escape(filename));
                sw.WriteLine("            VALUE \"LegalCopyright\", \"{0}\"", Escape(_fileVersion.LegalCopyright));
                sw.WriteLine("            VALUE \"LegalTrademarks\", \"{0}\"", Escape(_fileVersion.LegalTrademarks));
                sw.WriteLine("            VALUE \"OriginalFilename\", \"{0}\"", Escape(filename));
                sw.WriteLine("            VALUE \"ProductName\", \"{0}\"", Escape(_fileVersion.ProductName));
                sw.WriteLine("            VALUE \"ProductVersion\", \"{0}\"", Escape(_fileVersion.ProductVersion));
                sw.WriteLine("        END");
                sw.WriteLine("    END");
                sw.WriteLine("    BLOCK \"VarFileInfo\"");
                sw.WriteLine("    BEGIN");
                sw.WriteLine("        VALUE \"Translation\", 0x0, 1200");
                sw.WriteLine("    END");
                sw.WriteLine("END");
                sw.WriteLine("");
                sw.WriteLine("");

                rcText = sw.ToString();
            }

            File.AppendAllText(rcFile, rcText);
        }

        private string Escape(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in str)
            {
                if (ch >= 32 && ch < 128)
                {
                    if (ch == '\\' || ch == '\'' || ch == '"')
                        sb.Append('\\');
                    sb.Append(ch);
                    continue;
                }
                if (ch == '\r') { sb.Append("\\r"); continue; }
                if (ch == '\n') { sb.Append("\\n"); continue; }
                if (ch == '\t') { sb.Append("\\t"); continue; }

                sb.Append('\\');
                sb.Append((char) ('0' + ((ch >> 6) & 3)));
                sb.Append((char) ('0' + ((ch >> 3) & 7)));
                sb.Append((char) ('0' + (ch & 7)));
            }
            return sb.ToString();
        }

        public string AssemblyInfoCode
        {
            get { return _assemblyInfo ?? String.Empty; }
        }

        public void ReadFrom(string versionInfo)
        {
            StringWriter asmInfo = new StringWriter();
            asmInfo.WriteLine("using System;");
            asmInfo.WriteLine();

            using (DisposingList disposable = new DisposingList())
            {
                if (versionInfo.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    string dir = Path.GetDirectoryName(versionInfo);
                    XmlLightDocument proj = new XmlLightDocument(File.ReadAllText(versionInfo));
                    foreach (XmlLightElement xref in proj.Select("/Project/ItemGroup/Compile"))
                    {
                        if (!xref.Attributes.ContainsKey("Include") ||
                            !xref.Attributes["Include"].EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase))
                            continue;
                        string include = xref.Attributes["Include"];
                        versionInfo = Path.Combine(dir, include);
                        break;
                    }
                    if (versionInfo.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                        throw new ApplicationException("Unable to locate AssemblyInfo.cs");
                }
                if (versionInfo.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    _assemblyInfo = File.ReadAllText(versionInfo);

                    TempFile dll = TempFile.FromExtension(".dll");
                    disposable.Add(dll);
                    dll.Delete();

                    CSharpCodeProvider csc = new CSharpCodeProvider();
                    CompilerParameters args = new CompilerParameters();
                    args.GenerateExecutable = false;
                    args.IncludeDebugInformation = false;
                    args.OutputAssembly = dll.TempPath;
                    args.ReferencedAssemblies.Add("System.dll");
                    CompilerResults results = csc.CompileAssemblyFromFile(args, versionInfo);

                    StringWriter sw = new StringWriter();
                    foreach (CompilerError ce in results.Errors)
                    {
                        if (ce.IsWarning) continue;
                        String line = String.Format("{0}({1},{2}): error {3}: {4}", ce.FileName, ce.Line, ce.Column,
                                                    ce.ErrorNumber, ce.ErrorText);
                        Trace.WriteLine(line);
                        sw.WriteLine(line);
                    }
                    string errorText = sw.ToString();
                    if (errorText.Length > 0)
                        throw new ApplicationException(errorText);
                    versionInfo = dll.TempPath;
                }
                if (!File.Exists(versionInfo) || (!versionInfo.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !versionInfo.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                    throw new ApplicationException("Expected an existing dll: " + versionInfo);

                try
                {
                    Regex truefalse = new Regex(@"(?<=[^\w_\.])(?:True)|(?:False)(?=[^\w_\.])");
                    _fileVersion = FileVersionInfo.GetVersionInfo(versionInfo);

                    if (_assemblyInfo == null)
                    {
                        Assembly asm = Assembly.ReflectionOnlyLoad(File.ReadAllBytes(versionInfo));
                        IList<CustomAttributeData> attrs = CustomAttributeData.GetCustomAttributes(asm);
                        foreach (CustomAttributeData data in attrs)
                        {
                            if (!data.ToString().StartsWith("[System.Runtime.CompilerServices."))
                            {
                                string attribute = data.ToString().Trim();
                                if (attribute[0] != '[')
                                    continue; //unexpected...
                                attribute = attribute.Insert(1, "assembly: ");
                                attribute = truefalse.Replace(attribute, delegate(Match x) { return x.Value.ToLower(); });
                                asmInfo.WriteLine(attribute);
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            _assemblyInfo = _assemblyInfo ?? asmInfo.ToString();
        }
    }
}
