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
using System.IO;
using Microsoft.Build.BuildEngine;

#pragma warning disable 618
namespace CSharpTest.Net.CustomTool.Projects
{
	class MsBuildProjectItem : IProjectItem
	{
		readonly Project _msProject;
		readonly BuildItem _item;

		public MsBuildProjectItem(Project project, BuildItemGroup grp, BuildItem item)
		{
			_msProject = Check.NotNull(project);
			Check.NotNull(grp);
			_item = Check.NotNull(item);
		}

		public string BuildAction
		{
			get { return _item.Name; }
		}

		public string CustomTool
		{
			get { return !_item.HasMetadata("Generator") ? null : _item.GetMetadata("Generator"); }
		}

		public string FullFileName
		{
			get
			{
				string test = Path.Combine(Path.GetDirectoryName(_msProject.FullFileName), _item.Include);
				if (File.Exists(test))
					return Path.GetFullPath(test);

				string tryAlso = _item.GetEvaluatedMetadata("FullPath");
				if (File.Exists(tryAlso))
					return Path.GetFullPath(tryAlso);

				//? huh. that sux ;)
				return test;
			}
		}

		public string FullPseudoPath
		{
			get
			{
				if (_item.HasMetadata("Link"))
					return Path.Combine(Path.GetDirectoryName(_msProject.FullFileName), _item.GetMetadata("Link"));
				return FullFileName;
			}
		}

		public string Namespace
		{
			get { return CustomNamespace ?? DefaultNamespace; }
		}

		public string CustomNamespace
		{
			get
			{
				if (_item.HasMetadata("CustomToolNamespace"))
				{
					string ns = _item.GetMetadata("CustomToolNamespace");
					if (!String.IsNullOrEmpty(ns))
						return ns;
				}
				return null;
			}
		}

		public string DefaultNamespace
		{
			get
			{
				string ns = _msProject.GetEvaluatedProperty("RootNamespace");
				string relName = _item.Include;
				if (_item.HasMetadata("Link")) relName = _item.GetMetadata("Link");

				string[] parts = relName.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				for (int i = 0; i < parts.Length - 1; i++)
					ns += '.' + parts[i];
				return ns.Trim('.');
			}
		}
	}
}
