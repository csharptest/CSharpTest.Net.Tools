#region Copyright 2009 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Runtime.InteropServices;
using System.Text;
using CSharpTest.Net.CustomTool.CodeGenerator;
using CSharpTest.Net.CustomTool.Projects;
using BE=Microsoft.Build.BuildEngine;

#pragma warning disable 618

namespace CSharpTest.Net.CustomTool.VsInterop
{
    [ComVisible(true)]
    [ProgId("CSharpTest.CmdTool")]
    [Guid("C0DE0000-3545-401d-821A-A6C0C5464F75")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CmdTool : BaseCodeGeneratorWithSite
    {
        private string _lastGeneratedExtension;

        public override string GetExtension()
        {
            try
            {
                return _lastGeneratedExtension ?? ".Generated.cs";
            }
            finally
            {
                _lastGeneratedExtension = null;
            }
        }

        protected override byte[] GenerateCode(string defaultNamespace, string inputFileName)
        {
            byte[] resultBytes = null;
            BE.Project project = null;

            if (Project != null)
            {
                if (!Project.Saved)
                {
                    //if (!String.IsNullOrEmpty(Project.FileName))
                    Project.Save(String.Empty);
                }

                project = BE.Engine.GlobalEngine.GetLoadedProject(Project.FullName);
                if (project == null)
                {
                    project = new BE.Project(BE.Engine.GlobalEngine);
                    try
                    {
                        project.Load(Project.FullName);
                    }
                    catch (Exception ex)
                    {
                        WriteLine(ex.ToString());
                    }
                }
            }

            if (project == null)
                project = new Microsoft.Build.BuildEngine.Project(BE.Engine.GlobalEngine);

            GeneratorArguments arguments = new GeneratorArguments(false, inputFileName, defaultNamespace,
                                                                  new MsBuildProject(project));
            arguments.OutputMessage += base.WriteLine;

            using (CmdToolBuilder builder = new CmdToolBuilder())
                builder.Generate(arguments);

            GeneratorArguments.OutputFile primaryFile;
            foreach (GeneratorArguments.OutputFile file in arguments.GetOutput(out primaryFile))
            {
                try
                {
                    if (file.AddToProject)
                        AddProjectFile(file.FileName);
                }
                catch (Exception ex)
                {
                    arguments.WriteError(0, ex.ToString());
                }
            }

            if (primaryFile != null)
            {
                string testPrefix = Path.ChangeExtension(Path.GetFullPath(inputFileName), ".");
                _lastGeneratedExtension = primaryFile.FileName.Substring(testPrefix.Length - 1);
                resultBytes = Encoding.UTF8.GetBytes(File.ReadAllText(primaryFile.FileName));
            }

            if (arguments.DisplayHelp)
            {
                string file = Path.Combine(Path.GetTempPath(), "CmdTool - Help.txt");
                File.WriteAllText(file, arguments.Help());
                try
                {
                    DTE.Documents.Open(file, "Auto", true);
                }
                catch
                {
                    System.Diagnostics.Process.Start(file);
                }

                //EnvDTE.Window window = DTE.ItemOperations.NewFile(@"General\Text File", "CmdTool - Help.txt", Guid.Empty.ToString("B"));
                //EnvDTE.TextSelection sel = window.Selection as EnvDTE.TextSelection;
                //if (sel != null)
                //{
                //    sel.Text = arguments.ReplaceVariables("$(help)");
                //}
            }

            return resultBytes;
        }
    }
}