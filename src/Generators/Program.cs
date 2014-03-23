#region Copyright 2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Diagnostics;
using CSharpTest.Net.Commands;

namespace CSharpTest.Net.Generators
{
    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            int result = -1;

            string temp;
            var wait = ArgumentList.Remove(ref args, "wait", out temp);
       
            try
            {
                // Construct the CommandInterpreter and initialize
                ICommandInterpreter ci = new CommandInterpreter(
                    DefaultCommands.Help |
                    DefaultCommands.IORedirect |
                    0,
                    // Add the types to use static members
                    typeof(Filters), 
                    // Add classes to use instance members
                    typeof(Commands)
                    );

                // If you want to hide some options/commands at runtime you can:
                foreach (var name in new[] {"prompt", "errorlevel"})
                {
                    IOption opt;
                    if (ci.TryGetOption(name, out opt))
                        opt.Visible = false;
                }
                foreach (var name in new[] {"echo", "set", "get"})
                {
                    ICommand opt;
                    if (ci.TryGetCommand(name, out opt))
                        opt.Visible = false;
                }

                // Apply all DefaultValue values to properties
                ci.SetDefaults();

                // If we have arguments, just run with those arguments...
                if (args.Length > 0)
                    ci.Run(args);
                else
                {
                    //When run without arguments you can either display help:
                    //ci.Help(null);
                    //... or run the interpreter:
                    ci.Run(Console.In);
                }

                result = ci.ErrorLevel;
            }
            catch (OperationCanceledException)
            { result = 3; }
            catch (ApplicationException ex)
            {
                Trace.TraceError("{0}", ex);
                Console.Error.WriteLine(ex.Message);
                result = 1;
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0}", ex);
                Console.Error.WriteLine(ex.ToString());
                result = 2;
            }
            finally
            {
                if (wait)
                {
                    Console.WriteLine("Exited with result = {0}, Press Enter to quit.", result);
                    Console.ReadLine();
                }
            }

            return Environment.ExitCode = result;
        }

        /// <summary>
        /// Internally defined static command filters provide pre-post processing...
        /// </summary>
        private static class Filters
        {
            /// <summary> Ensure exceptions are captured in the trace output </summary>
            [CommandFilter('\x0000', Visible = false)]
            public static void ExceptionFilter(ICommandInterpreter ci, ICommandChain chain, string[] args)
            {
                try { chain.Next(args); }
                catch (System.Threading.ThreadAbortException) { throw; }
                catch (CommandInterpreter.QuitException) { throw; }
                catch (OperationCanceledException) { throw; }
                catch (InterpreterException)
                {
                    // Incorrect useage or bad command name...
                    throw;
                }
                catch (Exception ex)
                {
                    if (args.Length > 0)
                        Trace.TraceError("[{0}]: {1}", args[0], ex);
                    else
                        Trace.TraceError("{0}", ex);
                    throw;
                }
            }
        }
    }
}