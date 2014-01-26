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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BuildEngine;

#pragma warning disable 618
namespace CSharpTest.Net.CustomTool.Projects
{
	class MsBuildProject : IProjectInfo
	{
		readonly Project _msProject;

		public MsBuildProject(Project msProject)
		{
			_msProject = msProject;
		}

		public string FullFileName { get { return _msProject.FullFileName; } }

		public IProjectItem FindProjectItem(string filename, string nameSpace)
		{
			if (File.Exists(filename))
				filename = Path.GetFullPath(filename);

			foreach (IProjectItem item in this)
			{
				try
				{
                    System.Diagnostics.Trace.TraceInformation("Comparing '{0}' == '{1}'", filename, item.FullFileName);
					if (StringComparer.OrdinalIgnoreCase.Equals(item.FullFileName, filename) && File.Exists(item.FullFileName))
					{
                        System.Diagnostics.Trace.TraceInformation("Comparing '{0}' == '{1}' || '{2}'", nameSpace, item.CustomNamespace, item.DefaultNamespace);
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
			Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			ArrayList groups = new ArrayList();
			groups.Add(_msProject.GlobalProperties);
			groups.AddRange(_msProject.PropertyGroups);

			foreach (BuildPropertyGroup grp in groups)
			{
				foreach (BuildProperty prop in grp)
					try { values[prop.Name] = _msProject.GetEvaluatedProperty(prop.Name); } catch { }
			}
			return values;
		}

		public IEnumerator<IProjectItem> GetEnumerator()
		{
			foreach (BuildItemGroup grp in _msProject.ItemGroups)
			{
				if (grp.IsImported) continue;
				foreach (BuildItem item in grp)
				{
					if (item.IsImported) continue;
					yield return new MsBuildProjectItem(_msProject, grp, item);
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }
	}
}
