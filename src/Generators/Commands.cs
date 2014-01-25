#region Copyright 2010 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Text;
using CSharpTest.Net.Commands;
using CSharpTest.Net.Generators.ResX;
using CSharpTest.Net.Generators.ResXtoMc;
using CSharpTest.Net.Html;
using CSharpTest.Net.IO;
using CSharpTest.Net.Processes;

namespace CSharpTest.Net.Generators
{
    public static class Commands
    {
        [Command("Config", Description = "Writes a default CmdTool.config for this toolset to the console.")]
        public static void Config()
        {
            Console.WriteLine(Properties.Resources.CmdTool_csproj);
        }

        [Command("ResX", Description = "Generates strongly typed resources from .resx files with formatting and excpetions.")]
        public static void ResX(
            [Argument("input", "in", Description = "The input resx file to generate resources from.")]
            string inputFile,
            [Argument("namespace", "ns", Description = "The resulting namespace to use when generating resource classes.")]
            string nameSpace,
            [Argument("class", "c", Description = "The name of the containing class to use for the generated resources.")]
            string className,
            [Argument("resxNamespace", "rxns", Description = "The namespace that the resource file will be embeded with.", DefaultValue = null)]
            string resxNamespace,
            [Argument("public", Description = "Determines if the output resource class should be public or internal.", DefaultValue = false)]
            bool makePublic,
            [Argument("partial", Description = "Markes generated resource classes partial.", DefaultValue = true)]
            bool makePartial,
            [Argument("test", Description = "Attempts to run String.Format over all formatting strings.", DefaultValue = true)]
            bool testFormat,
			[Argument("sealed", Description = "Generates exceptions as sealed classes.", DefaultValue = false)]
            bool sealedExceptions,
            [Argument("base", Description = "The default base exception to derive from.", DefaultValue = "System.ApplicationException")]
            string baseException
			//[Argument("extension", "ext", Description = "The output extension to use, otherwise '.Generated.cs'.", DefaultValue = ".Generated.cs")]
			//string extension
            )
        {
			ResxGenWriter writer = new ResxGenWriter(inputFile, nameSpace, resxNamespace ?? nameSpace, makePublic, makePartial, sealedExceptions, className, baseException);
			writer.Write(Console.Out);

            if (testFormat && !writer.Test(Console.Error))
                throw new ApplicationException("One or more String.Format operations failed.");
        }

        [Command("ResXtoMc", Description = "Generates a Windows event log message file from a resx input file.")]
        public static void ResXtoMc(
            [Argument("output", "out", Description = "The target file path of the mc file to generate.")]
            string outputFile,
            [Argument("input", "in", Description = "The file path of either a resx file, or a project to scan for resx files within.")]
            string[] files
            )
        {
            McFileGenerator gen = new McFileGenerator(files);
            using (Stream io = File.Open(outputFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(io))
                gen.Write(writer);
        }

        [Command("ProjectResX", Description = "Generates the message files from a project and injects them into the project.")]
        public static void ProjectResX(
            [Argument("csproj", Description = "The csproj to generate message file for.")]
            string project,
            [Argument("name", Description = "The relative path and name of the generated resource files.", DefaultValue = "Resources")]
            string naming,
            [Argument("versionInfo", Description = "A csproj containing or an AssemblyInfo.cs file -- or -- a dll to extract version info from.", DefaultValue = null)]
            string versionInfo,
            [Argument("resources", Description = "A string to inject into the resource script file prior to compilation.", DefaultValue = null)]
            string resourceScript,
            [Argument("tools", Description = "The directory used to locate the mc.exe and rc.exe command line tools.", DefaultValue = null)]
            string toolsBin
            )
        {
            FauxProject proj = new FauxProject(project);
            Dictionary<string, string> vars = proj.GetProjectVariables();

            string mcFile = Path.Combine(Path.GetDirectoryName(project), naming + ".mc");
            string dir = Path.GetDirectoryName(mcFile);
            string projDir = Path.GetDirectoryName(project);
            Directory.CreateDirectory(dir);

            string nsSuffix = Path.GetDirectoryName(naming).Replace('/', '.').Replace('\\', '.').Trim('.');
            if (!String.IsNullOrEmpty(nsSuffix))
                nsSuffix = "." + nsSuffix;

            McFileGenerator gen = new McFileGenerator(new string[] { project });
            using (Stream io = File.Open(mcFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(io))
                gen.Write(writer);

            McCompiler mc = new McCompiler(gen, mcFile);
            mc.IntermediateFiles = dir;
            mc.Namespace = String.Format("{0}{1}", vars["RootNamespace"], nsSuffix);
            
            if (!String.IsNullOrEmpty(versionInfo))
                mc.VersionInfo.ReadFrom(versionInfo);
            else
                mc.VersionInfo.ReadFrom(project);
    
            if (!String.IsNullOrEmpty(toolsBin))
                mc.ToolsBin = toolsBin;

            if (vars.ContainsKey("ApplicationIcon"))
                mc.IconFile = Path.Combine(projDir, vars["ApplicationIcon"]);

            if (vars.ContainsKey("ApplicationManifest"))
                mc.ManifestFile = Path.Combine(projDir, vars["ApplicationManifest"]);

            string rcFile;
            mc.ResourceScript = resourceScript;
            mc.CreateResFile(vars["TargetFileName"], out rcFile);
            File.WriteAllText(Path.ChangeExtension(rcFile, ".Constants.cs"), mc.CreateConstants(Path.ChangeExtension(rcFile, ".h")));
            File.WriteAllText(Path.ChangeExtension(rcFile, ".InstallUtil.cs"), mc.CreateInstaller());
        }

        [Command("ResXtoMessageDll", Description = "Generates and compiles a Windows event log message assembly.")]
        public static void ResXtoMessageDll(
            [Argument("output", "out", Description = "The target assembly name to generate.")]
            string outputFile,
            [Argument("input", "in", Description = "The file path of either a resx file, or a project to scan for resx files within.")]
            string[] files,
            [Argument("intermediate", "temp", Description = "A directory used for intermediate files.", DefaultValue = "%TEMP%")]
            string intermediateFiles,
            [Argument("versionInfo", Description = "A csproj containing or an AssemblyInfo.cs file -- or -- a dll to extract version info from.", DefaultValue = null)]
            string versionInfo,
            [Argument("namespace", Description = "The namespace to use for the embedded constants and install utility classes.", DefaultValue = null)]
            string nameSpace,
            [Argument("tools", Description = "The directory used to locate the mc.exe and rc.exe command line tools.", DefaultValue = null)]
            string toolsBin,
            [Argument("csc", Description = "Additional options to provide to the csc command line compiler.", DefaultValue = "")]
            string cscOptions
            )
        {
            TempDirectory tempDir = null;
            try
            {
                intermediateFiles = intermediateFiles == "%TEMP%"
                                        ? (tempDir = new TempDirectory()).TempPath
                                        : Path.GetFullPath(Environment.ExpandEnvironmentVariables(intermediateFiles));

                string mcFile = Path.Combine(intermediateFiles, Path.GetFileNameWithoutExtension(outputFile) + ".mc");

                McFileGenerator gen = new McFileGenerator(files);
                using (Stream io = File.Open(mcFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(io))
                    gen.Write(writer);

                McCompiler mc = new McCompiler(gen, mcFile);
                mc.IntermediateFiles = intermediateFiles;

                if (!String.IsNullOrEmpty(nameSpace))
                    mc.Namespace = nameSpace;

                if (!String.IsNullOrEmpty(versionInfo))
                    mc.VersionInfo.ReadFrom(versionInfo);
                
                if (!String.IsNullOrEmpty(toolsBin))
                    mc.ToolsBin = toolsBin;

                mc.Compile(outputFile, cscOptions ?? "");
            }
            catch
            {
                if(tempDir != null)
                    tempDir.Detatch();// for diagnostics we allow this to leak if we fail
                tempDir = null;
            }
            finally
            {
                if (tempDir != null)
                    tempDir.Dispose();
            }
        }
    }
}
