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
using CSharpTest.Net.Html;

namespace CSharpTest.Net.CustomTool.Projects
{
	class FauxProjectItem : IProjectItem
	{
		private readonly FauxProject _project;
		private readonly XmlLightElement _item;
		private Dictionary<string, string> _meta;

		public FauxProjectItem(FauxProject project, XmlLightElement item)
		{
			_project = project;
			_item = item;
			_meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (XmlLightElement meta in _item.Children)
				_meta[meta.LocalName] = meta.InnerText;
		}

		public string BuildAction
		{
			get { return _item.LocalName; }
		}

		public string FullFileName
		{
			get { return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_project.FullFileName), _item.Attributes["Include"])); }
		}

		public string FullPseudoPath
		{
			get 
			{
				return _meta.ContainsKey("Link")
					? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_project.FullFileName), _meta["Link"]))
					: Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_project.FullFileName), _item.Attributes["Include"])); 
			}
		}

		public string CustomTool
		{
			get { return _meta.ContainsKey("Generator") ? _meta["Generator"] : null; }
		}

		public string CustomNamespace
		{
			get { return _meta.ContainsKey("CustomToolNamespace") ? _meta["CustomToolNamespace"] : null; }
		}

		public string Namespace
		{
			get { return CustomNamespace ?? DefaultNamespace; }
		}

		public string DefaultNamespace
		{
			get
			{
				string ns = _project.GetProjectVariables()["RootNamespace"];
				string relName = _item.Attributes["Include"];
				if (_meta.ContainsKey("Link")) relName = _meta["Link"];

				string[] parts = relName.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				for (int i = 0; i < parts.Length - 1; i++)
					ns += '.' + parts[i];
				return ns.Trim('.');
			}
		}
	}
}
