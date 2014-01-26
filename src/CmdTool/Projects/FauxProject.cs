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
using CSharpTest.Net.Html;

namespace CSharpTest.Net.CustomTool.Projects
{
	class FauxProject : IProjectInfo
	{
		readonly string _path;
		XmlLightElement _doc;
		Dictionary<string, string> _variables;

		public FauxProject(string file) : this(file, File.ReadAllText(file)) { }
		public FauxProject(string file, string projContent)
		{
			_path = Path.GetFullPath(Check.NotEmpty(file));
			Check.Assert<FileNotFoundException>(File.Exists(_path));
			_doc = new XmlLightDocument(projContent).Root;
		}

		public string FullFileName
		{
			get { return _path; }
		}

		public IProjectItem FindProjectItem(string filename, string nameSpace)
		{
			if (File.Exists(filename))
				filename = Path.GetFullPath(filename);

			foreach (IProjectItem item in this)
			{
				try
				{
					if (StringComparer.OrdinalIgnoreCase.Equals(item.FullFileName, filename) && File.Exists(item.FullFileName))
					{
						if (nameSpace == item.CustomNamespace || nameSpace == item.DefaultNamespace)
							return item;
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Trace.WriteLine(ex.ToString());
				}
			}
			throw new ApplicationException(String.Format("Unable to locate project item for: {0}", filename));
		}

		public Dictionary<string, string> GetProjectVariables()
		{
			if (_variables != null)
				return _variables;
			_variables = new Dictionary<string, string>();

			foreach (XmlLightElement group in _doc.Children)
			{
				if (group.LocalName == "PropertyGroup" && group.Attributes.ContainsKey("Condition") == false)
					foreach (XmlLightElement item in group.Children)
						if (!item.IsSpecialTag)//< ignore text/comments
							_variables[item.LocalName] = item.InnerText;
			}

			_variables["ProjectName"] = Path.GetFileNameWithoutExtension(_path);
			_variables["ProjectExt"] = Path.GetExtension(_path);
			_variables["ProjectPath"] = Path.GetFullPath(_path);
			_variables["ProjectDir"] = Path.GetDirectoryName(_path).TrimEnd('\\') + '\\';
			_variables["ProjectFileName"] = Path.GetFileName(_path);
			_variables["TargetName"] = _variables["AssemblyName"];
			_variables["TargetExt"] = _variables["OutputType"] == "Library" ? ".dll" : ".exe";
			_variables["TargetFileName"] = _variables["TargetName"] + _variables["TargetExt"];

			return _variables;
		}

		public IEnumerator<IProjectItem> GetEnumerator()
		{
			foreach (XmlLightElement group in _doc.Children)
			{
				if (group.LocalName == "ItemGroup")
				{
					foreach (XmlLightElement item in group.Children)
						if(!item.IsSpecialTag)//< ignore text/comments
							yield return new FauxProjectItem(this, item);
				}
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }
	}
}
