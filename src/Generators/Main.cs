using System;
using System.Diagnostics;
using System.Reflection;
using CSharpTest.Net.Commands;
using CSharpTest.Net.Generators;

/// <summary>
/// The actual program class can be simplified, but the following demostrates most of the capability
/// that can be used in the CommandInterpreter as well as default implementations for some standard
/// options: /nologo, /verbose, and /wait
/// </summary>
class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        int result = -1;

        string temp;
        var wait = ArgumentList.Remove(ref args, "wait", out temp);
        var nologo = ArgumentList.Remove(ref args, "nologo", out temp);
       
        try
        {
            // Display logo/header information
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null && nologo == false)
            {
                Console.WriteLine("{0}", entryAssembly.GetName());
                foreach (AssemblyCopyrightAttribute a in entryAssembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false))
                    Console.WriteLine("{0}", a.Copyright);
                Console.WriteLine();
            }

            // If verbose output was specified, attach a trace listener
            if (ArgumentList.Remove(ref args, "verbose", out temp) || ArgumentList.Remove(ref args, "verbosity", out temp))
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
        /// <summary> Augment the default help description </summary>
        [CommandFilter('?', Visible = false)]
        public static void HelpFilter(ICommandInterpreter ci, ICommandChain chain, string[] args)
        {
            if (args.Length == 1 && StringComparer.OrdinalIgnoreCase.Equals("help", args[0]) || args[0] == "?")
            {
                chain.Next(args);
                Console.WriteLine(@"Global Options:
     /nologo:  Suppress the logo/copyright message
  /verbosity:  [All] Verbosity level: Off, Error, Warning, Information, Verbose, or All
");
            }
            else
            {
                chain.Next(args);
            }
        }

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