#region Copyright 2010-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.IO;
using System.Runtime.InteropServices;
using CSharpTest.Net.IO;
using CSharpTest.Net.Utils;
using Microsoft.CSharp;
using Microsoft.VisualBasic;

namespace CSharpTest.Net.Processes
{
    /// <summary>
    /// Defines the information needed to run various types of scripts on a Windows host
    /// </summary>
    public class ScriptEngine
    {
        /// <summary>
        /// Defines the languages we know how to run, or 'Other' when user-defined
        /// </summary>
        public enum Language
        {
            /// <summary> .JS Javascript file </summary>
            JScript = 1,
            /// <summary> .VBS VBScript file </summary>
            VBScript = 2,
            /// <summary> .CMD Shell Script </summary>
            Cmd = 3,
            /// <summary> PowerShell (v2, or v1) </summary>
            PowerShell = 4,
            /// <summary> .CS C# Program </summary>
            CSharp = 5,
            /// <summary> Visual Basic .Net Program </summary>
            VBNet = 6,
			/// <summary> The script is an executable's path </summary>
        	Exe = 7,
        }
        /// <summary>
        /// Options for script execution
        /// </summary>
        [Flags]
        public enum Options : int
        {
            /// <summary></summary>
            None = 0,
            /// <summary> Sends the script to the process via std::in rather than using a temp file </summary>
            UsesStandardInputScript = 0x0001,
        }

        internal static ScriptEngine[] AllEngines
        {
            get
            {
                return new ScriptEngine[]
                    {
                        new ScriptEngine((Language) 0, "<undefined>", "", ".", Options.None),
                        new ScriptEngine(Language.JScript, null, "//B //E:Javascript //Nologo \"{SCRIPT}\"", ".js",
                                         Options.None),
                        new ScriptEngine(Language.VBScript, null, "//B //E:VBscript //Nologo \"{SCRIPT}\"", ".vbs",
                                         Options.None),
                        new ScriptEngine(Language.Cmd, null, "/C \"{SCRIPT}\"", ".cmd", Options.None),
                        new ScriptEngine(Language.PowerShell, null, "-Command -", ".psh",
                                         Options.UsesStandardInputScript),
                        new ScriptEngine(Language.CSharp, "{SCRIPT}", "", ".exe", Options.None),
                        new ScriptEngine(Language.VBNet, "{SCRIPT}", "", ".exe", Options.None),
                        new ScriptEngine(Language.Exe, "{SCRIPT}", "", ".exe", Options.None),
                    };
            }
        }

        /// <summary>
		/// Returns the default execution options for the specified scripting type
		/// </summary>
		public static ScriptEngine GetDefaults(Language type)
		{
            return AllEngines[(int)type];
		}

		private readonly string _argumentFormat;
		private readonly Options _runOptions;
		private readonly Language _language;
		private readonly Converter<string, TempFile> _compile;
		private string _executable;
		private string _fileExtension;

        private ScriptEngine(Language type, string executable, string argumentFormat, string fileExtension, Options options)
            : this(type, executable, argumentFormat, fileExtension, options, null) { }

		private ScriptEngine(Language type, string executable, string argumentFormat, string fileExtension, Options options, Converter<string, TempFile> compiler)
        {
			_language = type;
			_executable = Check.NotEmpty(executable ?? FindExecutable(type));
			_argumentFormat = argumentFormat ?? String.Empty;
			_fileExtension = fileExtension ?? ".";
			_runOptions = options;
			_compile = compiler;
			if(_compile == null)
			{
				if(type == Language.Exe)
					_compile = SetExePath;
				else if (type == Language.CSharp)
					_compile = CodeCompiler<CSharpCodeProvider>;
				else if (type == Language.VBNet)
					_compile = CodeCompiler<VBCodeProvider>;
				else
					_compile = ScriptWriter;
			}
        }

		/// <summary> Returns the type/language of the script </summary>
		public Language ScriptType { get { return _language; } }

		/// <summary> The script engine executable </summary>
		public string Executable
		{
			get { return _executable; }
		}

		/// <summary> The arguments to run the script </summary>
		public string ArgumentFormat
		{
			get { return _argumentFormat; }
		}

		/// <summary> The file extension of the script </summary>
		public string FileExtension
		{
			get { return _fileExtension; }
		}

		/// <summary> The run options </summary>
		public Options RunOptions
		{
			get { return _runOptions; }
		}

		/// <summary> Preprocessing/Compiler routine </summary>
		public TempFile Compile(String script)
		{
			return _compile(script);
		}

        /// <summary>
        /// Returns true if the script should be fed into the std::in stream of the script process
        /// </summary>
        public bool UsesStandardInputScript { get { return ((RunOptions & Options.UsesStandardInputScript) == Options.UsesStandardInputScript); } }

		private TempFile SetExePath(string script)
		{
			_executable = script.Trim();
			_executable = FileUtils.ExpandEnvironment(_executable);
			if (!Path.IsPathRooted(_executable))
			{
				string found;
				if (File.Exists(_executable))
					_executable = Path.GetFullPath(_executable);
				else if(FileUtils.TrySearchPath(_executable, out found))
					_executable = found;
				else
					throw new FileNotFoundException(new FileNotFoundException().Message, script.Trim());
			}
            
			_fileExtension = Path.GetExtension(script.Trim());

			TempFile temp = new TempFile();
			temp.Delete();
			return temp;
		}

		private TempFile ScriptWriter(string script)
        {
            TempFile f = TempFile.FromExtension(FileExtension);
            f.WriteAllBytes(System.Text.Encoding.ASCII.GetBytes(script));
            return f;
        }

		private TempFile CodeCompiler<TCompiler>(string script) where TCompiler : CodeDomProvider, new()
        {
            TempFile exe = TempFile.FromExtension(".exe");
            exe.Delete();

            TCompiler csc = new TCompiler();
            CompilerParameters args = new CompilerParameters();
            args.GenerateExecutable = true;
            args.IncludeDebugInformation = false;
            args.ReferencedAssemblies.Add("System.dll");
            args.OutputAssembly = exe.TempPath;
            CompilerResults results = csc.CompileAssemblyFromSource(args, script);

            StringWriter sw = new StringWriter();
            foreach (CompilerError ce in results.Errors)
            {
                if(ce.IsWarning) continue;
                sw.WriteLine("{0}({1},{2}: error {3}: {4}", ce.FileName, ce.Line, ce.Column, ce.ErrorNumber, ce.ErrorText);
            }
            string errorText = sw.ToString();
            if (errorText.Length > 0)
                throw new ApplicationException(errorText);

            if (!exe.Exists)
                throw new FileNotFoundException(new FileNotFoundException().Message, exe.TempPath);

			_executable = Path.GetFullPath(exe.TempPath);
            return exe;
        }

        private static string FindExecutable(Language type)
        {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            switch (type)
            {
                case Language.CSharp:
                    return Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "CSC.exe");
                case Language.JScript:
                case Language.VBScript:
                    return Path.Combine(windir, "CScript.exe");
                case Language.Cmd:
                    return Path.Combine(windir, "Cmd.exe");
                case Language.PowerShell:
                    {
                        string path = null;
                        path =
                            Microsoft.Win32.Registry.GetValue(
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PowerShell\2\PowerShellEngine",
                                @"ApplicationBase", null) as string;
                        if (path == null)
                            path =
                                Microsoft.Win32.Registry.GetValue(
                                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PowerShell\1\PowerShellEngine",
                                    @"ApplicationBase", null) as string;
                        if (path == null)
                            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                                                @"WindowsPowerShell\v1.0\");

                        return Path.Combine(path ?? String.Empty, "powershell.exe");
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
	}
}