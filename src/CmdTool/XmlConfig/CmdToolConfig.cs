#region Copyright 2009-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Xml.Serialization;
using System.IO;

namespace CSharpTest.Net.CustomTool.XmlConfig
{
	/// <summary> Root configuration element </summary>
	[XmlRoot("CmdTool")]
	public sealed class CmdToolConfig
	{
		FileMatch[] _matches;

		/// <summary> file pattern matching </summary>
		[XmlElement("match")]
		public FileMatch[] Matches
		{
			get { return _matches ?? new FileMatch[0]; }
			set { _matches = value; }
		}

		internal void MakeFullPaths(string basePath)
		{
			foreach (FileMatch m in Matches)
			{
				foreach (GeneratorConfig cfg in m.Generators)
					cfg.BaseDirectory = basePath;

				foreach (MatchAppliesTo applies in m.AppliesTo)
				{
					if(String.IsNullOrEmpty(applies.FolderPath))
						continue;

					if (!Path.IsPathRooted(applies.FolderPath))
						applies.FolderPath = Path.Combine(basePath, applies.FolderPath);
				}
			}
		}
	}
}
