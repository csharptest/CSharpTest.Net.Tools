#region Copyright 2010-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using CSharpTest.Net.Commands;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Generators
{
	static class Program
    {
        [STAThread]
        static int Main(string[] arguments)
        {
            try
            {
                CommandInterpreter ci = new CommandInterpreter(DefaultCommands.Help | DefaultCommands.IORedirect, typeof(Commands));
                ci.Run(arguments);
                
                Environment.ExitCode = ci.ErrorLevel;
            }
            catch (ApplicationException ae)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(ae.Message);
                Environment.ExitCode = -1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(e.ToString());
                Environment.ExitCode = -1;
            }

            return Environment.ExitCode;
        }
    }
}
