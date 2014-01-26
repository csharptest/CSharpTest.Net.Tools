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
using CSharpTest.Net.CustomTool.CodeGenerator;

namespace CSharpTest.Net.CustomTool
{
    class CmdToolBuilder : IDisposable
    {
        public void Dispose()
        { }

        public void Generate(IGeneratorArguments arguments)
        {
            arguments.WriteLine("Generating {0}", arguments.InputPath);

            IEnumerable<ICodeGenerator> generators = GetGenerators(arguments);
            foreach (ICodeGenerator generator in generators)
            {
                try
                {
                    generator.Generate(arguments);
                }
                catch (ApplicationException ae)
                {
                    arguments.WriteError(0, ae.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    arguments.WriteError(0, ex.ToString());
                    throw;
                }
            }
        }

		public void Clean(IGeneratorArguments arguments)
		{
            IEnumerable<ICodeGenerator> generators = GetGenerators(arguments);
            foreach (ICodeGenerator generator in generators)
				generator.EnumOutputFiles(arguments, FileDelete);
		}

		private static void FileDelete(string filepath)
		{
			if(File.Exists(filepath))
				File.Delete(filepath);
		}

        private static IEnumerable<ICodeGenerator> GetGenerators(IGeneratorArguments arguments)
        {
            ConfigurationLoader config = new ConfigurationLoader(arguments);

            if (config.Count == 0)
                arguments.WriteLine("{0}: warning: Unable to locate generators.", arguments.InputPath);
            
            return config;
        }
    }
}
