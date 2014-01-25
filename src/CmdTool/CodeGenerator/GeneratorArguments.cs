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
using System.Collections.Generic;
using System.IO;
using System.Resources.Tools;
using System.Text.RegularExpressions;
using CSharpTest.Net.CustomTool.Projects;
using CSharpTest.Net.Utils;
using Microsoft.CSharp;

namespace CSharpTest.Net.CustomTool.CodeGenerator
{
    class GeneratorArguments : IGeneratorArguments
    {
        private readonly List<OutputFile> _files;
        private readonly IDictionary<string, string> _variables;
    	private readonly StringWriter _help;
    	public bool DisplayHelp;
		private bool _domainsAllowed;

        public GeneratorArguments(bool domainsAllowed, string inputFile, string nameSpace, IProjectInfo projectInfo)
			: this(domainsAllowed, inputFile, Check.NotNull(projectInfo).FindProjectItem(inputFile, nameSpace), projectInfo)
        { }

        public GeneratorArguments(bool domainsAllowed, string inputFile, IProjectItem item, IProjectInfo projectInfo)
        {
			_domainsAllowed = domainsAllowed;
        	_help = new StringWriter();
            _files = new List<OutputFile>();
            Check.NotNull(item);
            _variables = new Dictionary<string, string>(Check.NotNull(projectInfo).GetProjectVariables(), StringComparer.OrdinalIgnoreCase);

            inputFile = Path.GetFullPath(inputFile);

            _variables["CmdToolDir"] = Path.GetDirectoryName(GetType().Assembly.Location).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
			_variables["DefaultNamespace"] = item.DefaultNamespace;
            _variables["Namespace"] = item.Namespace;
            _variables["ClassName"] = StronglyTypedResourceBuilder.VerifyResourceName(Path.GetFileNameWithoutExtension(inputFile), new CSharpCodeProvider());
			_variables["PseudoPath"] = item.FullPseudoPath;
            _variables["InputPath"] = inputFile;
            _variables["InputName"] = Path.GetFileName(inputFile);
            _variables["InputDir"] = Path.GetDirectoryName(inputFile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public event Action<string> OutputMessage;

		public bool AllowAppDomains { get { return _domainsAllowed; } }

        public void WriteLine(string message) { OutputMessage(message); }
        public void WriteLine(string format, params object[] args) { OutputMessage(String.Format(format, args)); }
        public void WriteError(int line, string message) { WriteLine("{0}({1}): error: {2}", InputPath, line, message); }
        public void WriteError(int line, string format, params object[] args) { WriteError(line, String.Format(format, args)); }

        public class OutputFile
        {
            public OutputFile(string fileName, bool addToProject)
            {
                FileName = fileName;
                AddToProject = addToProject;
            }
            public readonly bool AddToProject;
            public readonly string FileName;
        }

        internal IEnumerable<OutputFile> GetOutput(out OutputFile primary)
        {
            primary = null;
            List<OutputFile> response = new List<OutputFile>();

            string testPrefix = Path.ChangeExtension(Path.GetFullPath(InputPath), ".");

            foreach (OutputFile file in _files)
            {
                string fullName = Path.GetFullPath(file.FileName);
                if(file.AddToProject && primary == null && fullName.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase))
                    primary = file;
                else
                    response.Add(file);
            }

            return response;
        }

        #region IGeneratorInput Members

        public string Namespace
        {
            get { return _variables["Namespace"]; }
        }

        public string ClassName
        {
            get { return _variables["ClassName"]; }
        }

        public string PseudoPath
        {
            get { return _variables["PseudoPath"]; }
        }

        public string InputPath
        {
            get { return _variables["InputPath"]; }
        }

        public string InputName
        {
            get { return _variables["InputName"]; }
        }

        public string InputDir
        {
            get { return _variables["InputDir"]; }
        }

		public string ConfigDir
		{
			get { return _variables["ConfigDir"]; }
			set { _variables["ConfigDir"] = value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; }
		}

		public string ReplaceVariables(string input)
		{
			return RegexPatterns.MakefileMacro.Replace(input, ReplaceVariable);
		}

		public string Help()
		{
			using (StringWriter sw = new StringWriter())
			{
				sw.WriteLine("CmdTool - Variable replacement help:");
				sw.WriteLine();
				if (_help.ToString().Length > 0)
				{
					sw.WriteLine("You are recieving this message because of the following errors:");
					sw.WriteLine(_help.ToString());
					sw.WriteLine();
				}
				sw.WriteLine("General formatting:");
				sw.WriteLine("$(VARIABLE) = will be replaced with the variable from the list below");
				sw.WriteLine("$(VARIABLE:xxx=yyy) = will be replaced with the variable from the list below,");
				sw.WriteLine("                      replacing the literal string 'xxx' with the substitution");
				sw.WriteLine("                      value of 'yyy'");
				sw.WriteLine("$(VARIABLE:xxx=yyy:aaa=bbb) = will be replaced with the variable from the list");
				sw.WriteLine("                      below, replacing the literal string 'xxx' with the");
				sw.WriteLine("                      substitution value of 'yyy' and again replacing 'aaa' ");
				sw.WriteLine("                      with 'bbb'");
				sw.WriteLine();
				sw.WriteLine("VARIABLES DEFINED:");

				List<string> keys = new List<string>(_variables.Keys);
				keys.Sort();
				foreach (string key in keys)
					sw.WriteLine("{0} = '{1}'", key, _variables[key]);

				return sw.ToString();
			}
		}

		private string ReplaceVariable(Match m)
		{
			string value;
			string fld = m.Groups["field"].Value;

			if (StringComparer.OrdinalIgnoreCase.Equals(fld, "help"))
				return Help();

			if (!_variables.TryGetValue(fld, out value))
			{
				DisplayHelp = true;
				WriteLine("{0}: warning: Unknown variable {1}", InputPath, m.Value);
				_help.WriteLine("Unknown variable {0}", m.Value);
				value = String.Empty;
			}

			if (value != null && m.Groups["replace"].Success)
			{
				for (int i = 0; i < m.Groups["replace"].Captures.Count; i++)
				{
					string replace = m.Groups["name"].Captures[i].Value;
					string with = m.Groups["value"].Captures[i].Value;
					value = value.Replace(replace, with);
				}
			}
			return value;
		}

        public void AddOutputFile(string fileName)
        {
			if(!File.Exists(fileName))
			{
				WriteLine("Missing output file: " + fileName);
				return;
			}

        	foreach (OutputFile file in _files)
        	{
				if(StringComparer.OrdinalIgnoreCase.Equals(
					Path.GetFullPath(file.FileName),
					Path.GetFullPath(fileName)))
					return;
        	}

            _files.Add(new OutputFile(fileName, true));
        }

        #endregion
    }
}