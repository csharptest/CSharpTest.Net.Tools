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
using System.Threading;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.AssemblyInfoPatcher
{
	static class Program
    {
        static int DoHelp()
        {
            Console.WriteLine(@"
Usage:
  > AssemblyInfoPatcher.exe [/nologo] [/add-missing] <dir> -<Attr>=<Value>

  - using the option /add-missing will append missing attributes to the file.

  - Replace <dir> with a root directory to crawl for AssemblyInfo.?? files...

  - Replace <Attr> with any assembly-level attribute that takes a single
    parameter, for example, set the version:

  > AssemblyInfoPatcher.exe . -AssemblyVersion=1.0.0.0

  - You can use environment variables as values in the replacement.  For
    example, use TeamCity's VCS number as the configuration:

  > AssemblyInfoPatcher.exe . -AssemblyConfiguration=%system.build.vcs.number%
    
  - For projects in the same directory or one level above the AssemblyInfo.cs
    you can use the parameters that are non-conditional.  For example to use
    the project's Guid as the assembly Guid:

  > AssemblyInfoPatcher.exe . -Guid=$(ProjectGuid)

  - Set the assembly title to assembly name:

  > AssemblyInfoPatcher.exe . -AssemblyTitle=$(AssemblyName)

  - Using the $(xxx) syntax will first read properties from the command line,
    then from the relative project file (if found), and finally from the 
    environment.  If the variable was not found a warning message is printed.
  
  - You can replace text in the expanded value by using the following syntax:
        -AssemblyConfiguration=$(TargetFrameworkVersion:v=version )
    This results in replacing 'v' with 'version ' in the expanded value of the
    TargetFrameworkVersion variable.

  - Lastly all of the above can be provided via a response file(s) by using:

  > AssemblyInfoPatcher.exe . @myoptions.txt

    Where myoptions.txt contains one argument per line in a text file.

");//                                                                         ^
			return 0;
		}

		[STAThread]
		static int Main(string[] raw)
        {
            String temp;
		    bool wait = ArgumentList.Remove(ref raw, "wait", out temp);
            bool addMissing = ArgumentList.Remove(ref raw, "add-missing", out temp);

            try
		    {
		        var entryAssembly = Assembly.GetEntryAssembly();
		        if (entryAssembly != null && !ArgumentList.Remove(ref raw, "nologo", out temp))
		        {
		            var aname = entryAssembly.GetName();
		            aname.CultureInfo = null;
		            Console.WriteLine("{0}", aname);
		            foreach (
		                AssemblyCopyrightAttribute a in
		                    entryAssembly.GetCustomAttributes(typeof (AssemblyCopyrightAttribute), false))
		                Console.WriteLine("{0}", a.Copyright);
		            Console.WriteLine();
		        }

		        if (ArgumentList.Remove(ref raw, "verbose", out temp) || ArgumentList.Remove(ref raw, "verbosity", out temp))
		        {
		            SourceLevels traceLevel;
		            try
		            {
		                traceLevel = (SourceLevels) Enum.Parse(typeof (SourceLevels), temp);
		            }
		            catch
		            {
		                traceLevel = SourceLevels.All;
		            }

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

		        raw = argsList.ToArray();
		        if (ArgumentList.Remove(ref raw, "?", out temp) || ArgumentList.Remove(ref raw, "help", out temp))
		            return DoHelp();

                var args = new ArgumentList(StringComparer.Ordinal, raw);
		        if (args.Unnamed.Count == 0 || args.Count == 0)
		            return DoHelp();

		        var files = new FileList();
		        files.ProhibitedAttributes = FileAttributes.Hidden | FileAttributes.System;
		        files.RecurseFolders = true;
		        files.FileFound +=
		            delegate(object sender, FileList.FileFoundEventArgs eventArgs)
		            {
		                eventArgs.Ignore = !eventArgs.File.Name.StartsWith("AssemblyInfo");
		            };

		        foreach (var arg in args.Unnamed)
		            files.Add(arg);

		        var processor = new AssemblyFileProcessor(args, addMissing);
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
		    finally
		    {
		        if (wait)
		        {
		            Console.WriteLine();
		            Console.WriteLine("Press [Enter] to continue...");
		            Console.ReadLine();
		        }
		    }
		    return Environment.ExitCode;
		}

	}
}