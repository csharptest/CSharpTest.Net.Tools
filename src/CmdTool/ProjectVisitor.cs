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
using System.Runtime.InteropServices;
using CSharpTest.Net.CustomTool.Projects;
using CSharpTest.Net.Utils;
using Microsoft.Build.BuildEngine;

#pragma warning disable 618 //Obsolete warning on new Engine()

namespace CSharpTest.Net.CustomTool
{
	public delegate bool ProjectFileFilter(FileInfo file);

	public class ProjectVisitor
	{
		private readonly bool _fastLoader;
		private readonly FileList _projects;
		public ProjectVisitor(bool fastLoader, string[] projects) : this(fastLoader, projects, null) { }
		public ProjectVisitor(bool fastLoader, string[] projects, ProjectFileFilter filter)
		{
			_fastLoader = fastLoader;
			_projects = new FileList();
			_projects.ProhibitedAttributes = FileAttributes.Hidden;
			_projects.FileFound += FileFound;
			if (filter != null)
				_projects.FileFound += delegate(object o, FileList.FileFoundEventArgs e) { e.Ignore |= filter(e.File); };
			_projects.Add(projects);
		}

		public int Count { get { return _projects.Count; } }

		static void FileFound(object sender, FileList.FileFoundEventArgs e)
		{
			e.Ignore = false == StringComparer.OrdinalIgnoreCase.Equals(e.File.Extension, ".csproj");
		}

		public void VisitProjects(VisitProject visitor)
		{
#if !MSVISITOR
			if (_fastLoader) FastVisitProjects(visitor);
			else 
#endif                
                MsVisitProjects(visitor);
		}

		void MsVisitProjects(VisitProject visitor)
		{
			Engine e = new Engine(RuntimeEnvironment.GetRuntimeDirectory());
            if(e.GetType().Assembly.GetName().Version.Major == 2)
				try { e.GlobalProperties.SetProperty("MSBuildToolsPath", RuntimeEnvironment.GetRuntimeDirectory()); }
				catch { }

			foreach (FileInfo file in _projects)
			{
				Project prj = new Project(e);
				try
				{
					prj.Load(file.FullName);
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Unable to open project: {0}", file);
					Log.Verbose(ex.ToString());
					continue;
				}

				visitor(new MsBuildProject(prj));
				e.UnloadProject(prj);
			}
		}

#if !MSVISITOR
		void FastVisitProjects(VisitProject visitor)
		{
			VisitProjectList proc = FauxVisitProjects;
			List<IAsyncResult> results = new List<IAsyncResult>();
			List<FileInfo> projects = new List<FileInfo>(_projects);
			int count = Math.Max(1, projects.Count / Math.Max(1, Environment.ProcessorCount));
			while (projects.Count > 0)
			{
				FileInfo[] set = new FileInfo[Math.Min(count, projects.Count)];
				projects.CopyTo(0, set, 0, set.Length);
				projects.RemoveRange(0, set.Length);

				results.Add(proc.BeginInvoke(set, visitor, null, null));
			}

			foreach (IAsyncResult r in results)
				proc.EndInvoke(r);
		}

		static void FauxVisitProjects(IEnumerable<FileInfo> projects, VisitProject visitor)
		{
			foreach (FileInfo file in projects)
				FauxVisit(file, visitor);
		}

		static void FauxVisit(FileInfo file, VisitProject visitor)
		{
			IProjectInfo prj;
			try
			{
				prj = new FauxProject(file.FullName);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Unable to open project: {0}", file);
				Log.Verbose(ex.ToString());
				return;
			}

			visitor(prj);
		}
#endif

        public void VisitProjectItems(VisitProjectItem visitor)
		{
			VisitProjects(
				delegate(IProjectInfo p)
				{
					foreach (IProjectItem item in p)
					{
						visitor(p, item);
					}
				}
			);
		}
	}

    delegate void VisitProjectList(IEnumerable<FileInfo> projects, VisitProject visitor);
    public delegate void VisitProject(IProjectInfo prj);
    public delegate void VisitProjectItem(IProjectInfo prj, IProjectItem itm);
}
