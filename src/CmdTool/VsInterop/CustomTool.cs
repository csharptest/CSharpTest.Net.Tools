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
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CSharpTest.Net.CustomTool.CodeGenerator;
using CSharpTest.Net.CustomTool.Projects;

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
            var Project = this.Project;
            byte[] resultBytes = null;
            FauxProject project = null;

            if (Project != null)
            {
                if (!Project.Saved)
                {
                    //if (!String.IsNullOrEmpty(Project.FileName))
                    Project.Save(String.Empty);
                }

                project = new FauxProject(Project.FullName);
            }

            if (project == null)
                project = FauxProject.CreateEmpty(inputFileName);

            GeneratorArguments arguments = new GeneratorArguments(false, inputFileName, defaultNamespace, project);
            arguments.OutputMessage += base.WriteLine;

            if (Project != null)
            {
                try
                {
                    if (File.Exists(DTE.Solution.FileName))
                        arguments.SolutionDir = Path.GetDirectoryName(DTE.Solution.FileName);
                }
                catch { }
            }

            using (CmdToolBuilder builder = new CmdToolBuilder())
                builder.Generate(arguments);

            var addToProject = new List<string>();
            GeneratorArguments.OutputFile primaryFile;
            foreach (GeneratorArguments.OutputFile file in arguments.GetOutput(out primaryFile))
            {
                try
                {
                    if (file.AddToProject && File.Exists(file.FileName))
                        addToProject.Add(file.FileName);
                }
                catch (Exception ex)
                {
                    arguments.WriteError(0, ex.ToString());
                }
            }

            string primaryFileName = null;
            if (primaryFile != null)
            {
                string testPrefix = Path.ChangeExtension(Path.GetFullPath(inputFileName), ".");
                _lastGeneratedExtension = primaryFile.FileName.Substring(testPrefix.Length - 1);
                resultBytes = Encoding.UTF8.GetBytes(File.ReadAllText(primaryFile.FileName));
                addToProject.Add(primaryFile.FileName);
                primaryFileName = Path.GetFileName(primaryFile.FileName);
            }

            AddFilesToProject(addToProject.ToArray(), primaryFileName);

            string actualOutFile;
            if (Project != null && !IsLastGenOutputValid(primaryFileName, out actualOutFile))
            {
                Project.Save(String.Empty);
                // Complete hack, we don't have the ability to edit these values...
                string projectText = File.ReadAllText(project.FullFileName);
                projectText = projectText.Replace(
                    "<LastGenOutput>" + actualOutFile + "</LastGenOutput>",
                    "<LastGenOutput>" + primaryFileName + "</LastGenOutput>");

                arguments.WriteError(
                    0, "The project item has an incorrect value for LastGenOutput, expected \"{0}\" found \"{1}\".\r\n" +
                        "Press 'Discard' to correct or unload and edit the project.",
                        primaryFileName, actualOutFile
                    );

                File.WriteAllText(project.FullFileName, projectText);
            }
            
            if (arguments.DisplayHelp)
            {
                string file = Path.Combine(Path.GetTempPath(), "CmdTool - Help.txt");
                using (var wtr = new StreamWriter(file))
                {
                    wtr.WriteLine(arguments.Help());
                    wtr.WriteLine("ENVIRONMENT:");
                    foreach (System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
                        wtr.WriteLine("{0} = '{1}'", variable.Key, variable.Value);
                    wtr.Flush();
                }

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