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
using System.Reflection;
using System.Xml;
using CSharpTest.Net.CustomTool.XmlConfig;
using System.Configuration;

namespace CSharpTest.Net.CustomTool.CodeGenerator
{
    class ConfigurationLoader : IEnumerable<ICodeGenerator>
	{
		const string CONFIG_SECTION = "CmdTool";
		const string CONFIG_FILE_NAME = "CmdTool.config";

	    private static DateTime _configLoadTime;
	    private static Configuration _configFile;

        private IGeneratorArguments _args;
        private List<ICodeGenerator> _generators;

        public ConfigurationLoader(IGeneratorArguments args)
        {
            _args = args;

            _generators = new List<ICodeGenerator>();

            string filename = _args.PseudoPath;
			DirectoryInfo di = new DirectoryInfo(Path.GetDirectoryName(filename));
            while (di != null && !di.Exists)
                di = di.Parent;
            if (di != null && di.Exists)
                SearchConfig(_generators, di);

            CmdToolConfig config = ReadAppConfig();
            bool ignore;
            PerformMatch(_generators, config, out ignore);
		}

        public int Count { get { return _generators.Count; } }

		private void SearchConfig(List<ICodeGenerator> generators, DirectoryInfo dir)
		{
            bool visitParent = true;
			FileInfo[] cfgfiles = dir.GetFiles(CONFIG_FILE_NAME, SearchOption.TopDirectoryOnly);
			foreach (FileInfo file in cfgfiles)//0 or 1
			{
				CmdToolConfig config;
                try
                {
                    using (XmlReader reader = new XmlTextReader(file.FullName))
                        config = Config.ReadXml(reader);
                }
                catch (Exception e)
                {
                    int line = 0, pos = 0;
                    XmlException xe = e as XmlException ?? e.InnerException as XmlException;
                    if (xe != null) { line = xe.LineNumber; pos = xe.LinePosition; }

                    _args.WriteLine("{0}({1},{2}): error: {3}", file.FullName, line, pos, e.GetBaseException().Message);
                    Log.Verbose("Error in xml file {0}: {1}", file.FullName, e.ToString());
                    throw new ApplicationException(String.Format("Unable to load configuration file: {0}\r\nReason: {1}", file.FullName, e.Message), e);
                }
				config.MakeFullPaths(dir.FullName);
			    bool stop;
                PerformMatch(generators, config, out stop);
                if (stop)
                    visitParent = false;
			}

            if (dir.Parent != null && visitParent)
				SearchConfig(generators, dir.Parent);
		}

		private void PerformMatch(List<ICodeGenerator> generators, CmdToolConfig config, out bool stop)
		{
            stop = false;
		    foreach (FileMatch match in config.Matches)
		    {
		        string directory = Path.GetDirectoryName(_args.InputPath);
                directory = CleanPath(directory);

		        bool ismatch = false;
                foreach (string file in Directory.GetFiles(directory, match.FileSpec))
                    ismatch |= StringComparer.OrdinalIgnoreCase.Equals(file, _args.InputPath);
                if(!ismatch)
                    continue;

		        ismatch = match.AppliesTo.Length == 0;
		        foreach (MatchAppliesTo appliesTo in match.AppliesTo)
                    ismatch |= directory.StartsWith(CleanPath(appliesTo.FolderPath), StringComparison.OrdinalIgnoreCase);
                if (!ismatch)
                    continue;

                if (match.StopHere)
                    stop = true;

                Dictionary<string, ICodeGenerator> usedExtensions = new Dictionary<string, ICodeGenerator>(StringComparer.OrdinalIgnoreCase);
                foreach (ICodeGenerator gen in generators)
                {
                    foreach (string ext in gen.PossibleExtensions)
                        usedExtensions.Add(ext, gen);
                }

                foreach (GeneratorConfig gen in match.Generators)
                {
                    bool alreadyExists = false;
                    ICodeGenerator codeGen = new OutOfProcessGenerator(gen);
                    foreach (string ext in codeGen.PossibleExtensions)
                        alreadyExists |= usedExtensions.ContainsKey(ext);

                    if(!alreadyExists)
                        generators.Add(codeGen);
                }
		    }
		}

        private string CleanPath(string directory)
        {
            directory = directory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            directory = Environment.ExpandEnvironmentVariables(directory);

            if (Directory.Exists(directory))
                directory = Path.GetFullPath(directory);

            directory = directory.TrimEnd('.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return directory + Path.DirectorySeparatorChar;
        }

        CmdToolConfig ReadAppConfig()
        {
            CmdToolConfig cfg = null;
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
            try
            {
                if (_configFile == null || _configLoadTime < File.GetLastWriteTime(_configFile.FilePath))
                {
                    _configFile = ConfigurationManager.OpenExeConfiguration(typeof(Config).Assembly.Location);
                    _configLoadTime = DateTime.Now;
                }
                if (_configFile != null)
                    cfg = _configFile.GetSection(CONFIG_SECTION) as Config;
                if (cfg != null)
                    cfg.MakeFullPaths(Path.GetDirectoryName(_configFile.FilePath));
            }
            catch (Exception ex)
            { _args.WriteLine("{0}: {1}", _configFile != null ? _configFile.FilePath : "configuration error", ex.ToString()); }
            finally
            { AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolve; }

            return cfg ?? new CmdToolConfig();
        }

        static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName asm = new AssemblyName(args.Name);
            if (asm.Name == typeof(Config).Assembly.GetName().Name)
                return typeof(Config).Assembly;
            return null;
        }

        public IEnumerator<ICodeGenerator> GetEnumerator()
        { return _generators.GetEnumerator(); }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        { return GetEnumerator(); }
    }

}
