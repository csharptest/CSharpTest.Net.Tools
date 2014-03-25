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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.AssemblyInfoPatcher
{
	static class Program
    {
        #region string[] KnownAttributes
        public static readonly string[] KnownAttributes = new[]
	    {
	        "AssemblyTitle",
	        "AssemblyDescription",
	        "AssemblyConfiguration",
	        "AssemblyCompany",
	        "AssemblyProduct",
	        "AssemblyCopyright",
	        "AssemblyTrademark",
	        "AssemblyCulture",
	        "Guid",
	        "AssemblyVersion",
	        "AssemblyFileVersion",
            "AssemblyInformationalVersion"
	    };
        #endregion

        static int DoHelp()
		{
			Console.WriteLine("");
			Console.WriteLine("Usage:");
            Console.WriteLine("    AssemblyInfoPatcher.exe [/nologo] [/wait] /build:{Number}|{File Path} [/revision={Number}|{File Path}]");
            Console.WriteLine("");
			return 0;
		}

		[STAThread]
		static int Main(string[] raw)
        {
            String temp;
		    bool wait = ArgumentList.Remove(ref raw, "wait", out temp);

			try
			{
                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null && !ArgumentList.Remove(ref raw, "nologo", out temp))
                {
                    Console.WriteLine("{0}", entryAssembly.GetName());
                    foreach (AssemblyCopyrightAttribute a in entryAssembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false))
                        Console.WriteLine("{0}", a.Copyright);
                    Console.WriteLine();
                }

                if (ArgumentList.Remove(ref raw, "verbose", out temp) || ArgumentList.Remove(ref raw, "verbosity", out temp))
                {
                    SourceLevels traceLevel;
                    try { traceLevel = (SourceLevels)Enum.Parse(typeof(SourceLevels), temp); }
                    catch { traceLevel = SourceLevels.All; }

                    Trace.Listeners.Add(new ConsoleTraceListener()
                    {
                        Filter = new EventTypeFilter(traceLevel),
                        IndentLevel = 0,
                        TraceOutputOptions = TraceOptions.None
                    });
                }
                    
		        var argsList = new List<string>();
			    foreach (string arg in raw)
			    {
			        if (arg.StartsWith("@"))
			        {
			            foreach (var line in File.ReadAllLines(arg.Substring(1)))
			            {
                            if (!String.IsNullOrEmpty(line) && line.Trim().Length > 0)
    			                argsList.Add(line.Trim());
			            }
			        }
			        else
			        {
			            argsList.Add(arg);
			        }
			    }

			    var args = new ArgumentList(argsList.ToArray());

				if ((args.Unnamed.Count == 0 || args.Count == 0) || args.Contains("?") || args.Contains("help"))
					return DoHelp();

				var files = new FileList();
                files.ProhibitedAttributes = FileAttributes.Hidden | FileAttributes.System;
			    files.RecurseFolders = true;
                files.FileFound +=
                    delegate(object sender, FileList.FileFoundEventArgs eventArgs) 
                    { eventArgs.Ignore = !eventArgs.File.Name.StartsWith("AssemblyInfo"); };
                
                foreach (var arg in args.Unnamed)
			        files.Add(arg);

                var processor = new AssemblyFileProcessor(args);
				foreach (FileInfo file in files.ToArray())
				{
                    processor.ProcessFile(file);
				}
			}
			catch (ApplicationException ae)
			{
				Trace.TraceError("{0}", ae);
				Console.Error.WriteLine();
				Console.Error.WriteLine(ae.Message);
				Environment.ExitCode = -1;
			}
			catch (Exception e)
			{
				Trace.TraceError("{0}", e);
				Console.Error.WriteLine();
				Console.Error.WriteLine(e.ToString());
				Environment.ExitCode = -1;
			}

			if (wait)
			{
				Console.WriteLine();
				Console.WriteLine("Press [Enter] to continue...");
				Console.ReadLine();
			}

			return Environment.ExitCode;
		}

	}
}