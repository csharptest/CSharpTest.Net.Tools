#region Copyright 2009-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using CSharpTest.Net.Commands;
using CSharpTest.Net.CustomTool.XmlConfig;
using CSharpTest.Net.IO;
using CSharpTest.Net.Processes;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.CustomTool.CodeGenerator
{
    class OutOfProcessGenerator : ICodeGenerator
    {
        private readonly GeneratorConfig _config;
        public OutOfProcessGenerator(GeneratorConfig config)
        {
            _config = Check.NotNull(config);
            Check.NotNull(config.Script);
        }

        public override string ToString()
        {
            if (!String.IsNullOrEmpty(_config.Script.Include))
                return _config.Script.Include;
            return _config.Script.Text.Trim();
        }

        public string CreateFullPath(string path)
        {
            path = path.Trim();
            path = Environment.ExpandEnvironmentVariables(path);
            if (!Path.IsPathRooted(path))
            {
                if (File.Exists(Path.Combine(_config.BaseDirectory, path)))
                    path = Path.GetFullPath(Path.Combine(_config.BaseDirectory, path));
                else if (File.Exists(path))
                    path = Path.GetFullPath(path);
                else 
                    path = Processes.ProcessRunner.FindFullPath(path);
            }
            return path;
        }

        public void Generate(IGeneratorArguments input)
        {
            //Couple of assertions about PowerShell
            if (_config.Script.Type == ScriptEngine.Language.PowerShell &&
                (_config.StandardInput.Redirect || _config.Arguments.Length > 0))
                throw new ApplicationException(
                    @"Currently PowerShell integration does not support input streams or arguments.
Primarily this is due to circumventing the script-signing requirements. By 
using the '-Command -' argument we avoid signing or setting ExecutionPolicy.");

            using (DebuggingOutput debug = new DebuggingOutput(_config.Debug, input.WriteLine))
            {
                debug.WriteLine("ConfigDir = {0}", _config.BaseDirectory);
                input.ConfigDir = _config.BaseDirectory;

                //Inject arguments into the script
                string script = input.ReplaceVariables(Check.NotNull(_config.Script).Text.Trim());

                if (!String.IsNullOrEmpty(_config.Script.Include))
                    script = File.ReadAllText(CreateFullPath(_config.Script.Include));
                if (_config.Script.Type == ScriptEngine.Language.Exe)
                    script = CreateFullPath(script);

                StringWriter swOutput = new StringWriter();

                List<string> arguments = new List<string>();
                foreach (GeneratorArgument arg in _config.Arguments)
                    arguments.Add(input.ReplaceVariables(arg.Text ?? String.Empty));

                debug.WriteLine("Prepared Script:{0}{1}{0}{2}{0}{1}",
                    Environment.NewLine,
                    "---------------------------------------------",
                    script
                );

                using (ScriptRunner scriptEngine = new ScriptRunner(_config.Script.Type, script))
                {
                    IRunner runner = scriptEngine;
                    if (input.AllowAppDomains && _config.Script.InvokeAssembly)
                    {
                        runner = AssemblyRunnerCache.Fetch(scriptEngine.ScriptEngine.Executable);
                        arguments.InsertRange(0, scriptEngine.ScriptArguments);
                    }

                    runner.WorkingDirectory = _config.BaseDirectory;
                    string lastErrorMessage = null;
                    ProcessOutputEventHandler handler =
                        delegate(object o, ProcessOutputEventArgs args)
                        {
                            if (args.Error)
                                input.WriteLine(lastErrorMessage = args.Data);
                            else if (_config.StandardOut != null)
                            {
                                debug.WriteLine("std::out: {0}", args.Data);
                                swOutput.WriteLine(args.Data);
                            }
                            else
                                input.WriteLine(args.Data);
                        };

                    int exitCode = -1;
                    debug.WriteLine("Executing {0} {1}", runner, ArgumentList.EscapeArguments(arguments.ToArray()));
                    try
                    {
                        runner.OutputReceived += handler;
                        if (_config.StandardInput.Redirect)
                            exitCode = runner.Run(new StringReader(File.ReadAllText(input.InputPath)), arguments.ToArray());
                        else exitCode = runner.Run(arguments.ToArray());
                    }
                    finally
                    {
                        debug.WriteLine("Exited = {0}", exitCode);
                        runner.OutputReceived -= handler;
                    }

                    if (_config.StandardOut != null)
                    {
                        string target = Path.ChangeExtension(input.InputPath, _config.StandardOut.Extension);
                        using (TempFile file = TempFile.FromExtension(_config.StandardOut.Extension))
                        {
                            file.WriteAllText(swOutput.ToString());
                            File.Copy(file.TempPath, target, true);
                            input.AddOutputFile(target);
                        }
                    }

                    if (exitCode != 0)
                    {
                        string message = "The script returned a non-zero result: " + exitCode;
                        input.WriteLine(message);
                        throw new ApplicationException(String.IsNullOrEmpty(lastErrorMessage) ? message : lastErrorMessage);
                    }
                }

                EnumOutputFiles(input, input.AddOutputFile);
            }
        }

        public void EnumOutputFiles(IGeneratorArguments input, Action<string> outputFile)
        {
            if (_config.StandardOut != null)
                outputFile(Path.ChangeExtension(input.InputPath, _config.StandardOut.Extension));
            foreach (GeneratorOutput output in _config.Output)
                outputFile(Path.ChangeExtension(input.InputPath, output.Extension));
        }

        public IEnumerable<string> PossibleExtensions
        {
            get
            {
                if (_config.StandardOut != null)
                    yield return _config.StandardOut.Extension;
                foreach (GeneratorOutput output in _config.Output)
                    yield return output.Extension;
            }
        }
    }
}
