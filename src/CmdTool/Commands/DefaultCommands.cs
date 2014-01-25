#region Copyright 2009-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using CSharpTest.Net.Utils;
using System.IO;
using System.Reflection;

namespace CSharpTest.Net.Commands
{
	/// <summary>
	/// A list of built-in commands that can be added to the interpreter
	/// </summary>
	[Flags]
	public enum DefaultCommands : uint
	{
		/// <summary> Not a command, indicates no default commands </summary>
		None = 0,
		/// <summary> Not a command, indicates the default commands added if not specified </summary>
		Default = Get | Set | Help,
		/// <summary> Not a command, indicates to use all default commands </summary>
		All = 0xFFFFFFFF,

		/// <summary> A command to get the value of an option </summary>
		Get = 0x00000001,
		/// <summary> A command to set the value of an option </summary>
		Set = 0x00000002,
		/// <summary> A command to display help about the commands and their options </summary>
		Help = 0x00000004,
		/// <summary> An option to set and get the environment error-level </summary>
		ErrorLevel = 0x00000008,
		/// <summary> An option that provides customization of the command prompt for interactive mode </summary>
		Prompt = 0x00000010,
		/// <summary> A command to echo back to std::out the arguments provided. </summary>
		Echo = 0x00000020,
		/// <summary> A command to read the input stream and show one screen at a time to standard output. </summary>
		More = 0x00000040,
		/// <summary> A command to search for a text string in a file or the standard input stream. </summary>
		Find = 0x00000080,
		/// <summary> A command filter that allows piping the output of one command into the input of another. </summary>
		PipeCommands = 0x00000100,
		/// <summary> A command filter that allows redirect of std in/out to files. </summary>
		IORedirect = 0x00000200,
        /// <summary> A command that allows the console application to be hosted as a restful HTTP server. </summary>
        HostHTTP = 0x00000400
	}

    partial class CommandInterpreter
    {
        sealed class BuiltInCommands
        {
            readonly Dictionary<DefaultCommands, IDisplayInfo> _contents;

            internal BuiltInCommands(params IDisplayInfo[] all)
            {
                _contents = new Dictionary<DefaultCommands, IDisplayInfo>();
                foreach (IDisplayInfo d in BuiltIn.Commands)
                    _contents.Add((DefaultCommands)Enum.Parse(typeof(DefaultCommands), d.DisplayName, true), d);
                AddRange(all);
            }

            public void AddRange(params IDisplayInfo[] all)
            {
                foreach (IDisplayInfo d in all)
                    _contents.Add((DefaultCommands)Enum.Parse(typeof(DefaultCommands), d.DisplayName, true), d);
            }

            public void Add(CommandInterpreter ci, DefaultCommands cmds)
            {
                foreach (DefaultCommands key in Enum.GetValues(typeof(DefaultCommands)))
                {
                    IDisplayInfo item;
                    if (key == (key & cmds) && _contents.TryGetValue(key, out item))
                    {
                        if (item is ICommandFilter)
                            ci.AddFilter(item as ICommandFilter);
                        else if (item is ICommand)
                            ci.AddCommand(item as ICommand);
                        else if (item is IOption)
                            ci.AddOption(item as IOption);
                    }
                }
            }

            internal static class BuiltIn
            {
                public static ICommand[] Commands
                {
                    get
                    {
                        Type t = typeof(BuiltIn);
                        List<ICommand> cmds = new List<ICommand>();
                        foreach (MethodInfo mi in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod))
                            if (!mi.IsSpecialName)
                                cmds.Add(Command.Make(t, mi));
                        return cmds.ToArray();
                    }
                }

                [Command("Echo", Category = "Built-in", Description = "Writes the arguments to standard output.", Visible = true)]
                public static void Echo(
                    [AllArguments, Argument(Category = "Built-in", Description = "The text to write to standard out.")]
				    string[] args)
                {
                    Console.WriteLine(ArgumentList.EscapeArguments(args));
                }

                private static int NextRedirect(string[] args)
                {
                    for (int ix = args.Length - 1; ix >= 0; ix--)
                        if (args[ix].StartsWith("<") || args[ix].StartsWith(">")) return ix;
                    return -1;
                }

                [CommandFilter('>', '<', Category = "Built-in", Description = "A command filter that allows redirect of std in/out to files.", Visible = false)]
                public static void IORedirect(ICommandInterpreter ci, ICommandChain chain, string[] args)
                {
                    TextReader rin = null;
                    TextWriter rout = null;
                    int pos;
                    try
                    {
                        while ((pos = NextRedirect(args)) > 0)
                        {
                            List<string> cmd1 = new List<string>(args);
                            List<string> cmd2 = new List<string>(args);
                            cmd1.RemoveRange(pos, cmd1.Count - pos);
                            cmd2.RemoveRange(0, pos);

                            args = cmd1.ToArray();
                            string file = String.Join(" ", cmd2.ToArray());
                            bool isout = file.StartsWith(">");
                            bool isappend = isout && file.StartsWith(">>");
                            file = file.TrimStart('>', '<').Trim();

                            if (!isout)
                                rin = File.OpenText(file);
                            else
                                rout = isappend ? File.AppendText(file) : File.CreateText(file);
                        }
                    }
                    catch
                    {
                        using (rin)
                        using (rout)
                            throw;
                    }
                    TextReader stdin = ConsoleInput.Capture(rin);
                    TextWriter stdout = ConsoleOutput.Capture(rout);
                    try
                    {
                        chain.Next(args);
                    }
                    finally
                    {
                        using (rin)
                            ConsoleInput.Restore(rin, stdin);
                        using (rout)
                            ConsoleOutput.Restore(rout, stdout);
                    }
                }

                [Command("Find", Category = "Built-in", Description = "Reads the input stream and shows any line containing the text specified.", Visible = true)]
                public static void Find(
                    [Argument("text", Category = "Built-in", Description = "The text to search for in the input stream.")]
				    string text,
                    [Argument("filename", "f", Category = "Built-in", Description = "Specifies a file read and search, omit to use standard input.", DefaultValue = null)]
				    string filename,
                    [Argument("V", Category = "Built-in", Description = "Displays all lines NOT containing the specified string.", DefaultValue = false)]
				    bool invert,
                    [Argument("C", Category = "Built-in", Description = "Displays only the count of lines containing the string.", DefaultValue = false)]
				    bool count,
                    [Argument("I", Category = "Built-in", Description = "Ignores the case of characters when searching for the string.", DefaultValue = false)]
				    bool ignoreCase
                    )
                {
                    StringComparison cmp = StringComparison.Ordinal;
                    if (ignoreCase) cmp = StringComparison.OrdinalIgnoreCase;

                    StreamReader disposeMe = null;
                    TextReader rdr = Console.In;
                    if (!String.IsNullOrEmpty(filename))
                        rdr = disposeMe = File.OpenText(filename);
                    try
                    {
                        int counter = 0;
                        string line;
                        while (null != (line = rdr.ReadLine()))
                        {
                            bool found = (line.IndexOf(text, cmp) >= 0);
                            found = invert ? !found : found;
                            if (found)
                            {
                                counter++;
                                if (!count) Console.WriteLine(line);
                            }
                        }

                        if (count)
                            Console.WriteLine(counter);
                    }
                    finally
                    {
                        if (disposeMe != null)
                            disposeMe.Dispose();
                    }
                }

                [Command("More", Category = "Built-in", Description = "Reads the input stream and shows one screen at a time to standard output.", Visible = true)]
                public static void More(ICommandInterpreter ci)
                {
                    int pos = 2;
                    int lines;
                    try
                    {
                        lines = Console.WindowHeight;
                    }
                    catch (System.IO.IOException)
                    {
                        lines = 25;
                    }

                    string line;
                    while (null != (line = Console.ReadLine()))
                    {
                        Console.WriteLine(line);
                        if (++pos >= lines)
                        {
                            Console.Write("-- More --");
                            ci.ReadNextCharacter();
                            Console.WriteLine();
                            pos = 1;
                        }
                    }
                }

                private static int NextPipe(string[] args)
                {
                    for (int ix = 0; ix < args.Length; ix++)
                        if (args[ix].StartsWith("|")) return ix;
                    return -1;
                }

                [CommandFilter('|', Category = "Built-in", Description = "A command filter that allows piping the output of one command into the input of another.", Visible = false)]
                public static void PipeCommands(ICommandInterpreter ci, ICommandChain chain, string[] args)
                {
                    TextReader rdr = null, stdin = null;
                    int pos;
                    while ((pos = NextPipe(args)) > 0)
                    {
                        List<string> cmd1 = new List<string>(args);
                        cmd1.RemoveRange(pos, cmd1.Count - pos);
                        List<string> cmd2 = new List<string>(args);
                        cmd2.RemoveRange(0, pos);
                        cmd2[0] = cmd2[0].TrimStart('|');
                        if (cmd2[0].Length == 0)
                            cmd2.RemoveAt(0);
                        if (cmd2.Count == 0)
                        {
                            args = cmd1.ToArray();
                            break;
                        }
                        else
                            args = cmd2.ToArray();

                        StringWriter wtr = new StringWriter();
                        TextWriter stdout = ConsoleOutput.Capture(wtr);
                        stdin = ConsoleInput.Capture(rdr);

                        try { chain.Next(cmd1.ToArray()); }
                        finally 
                        { 
                            ConsoleInput.Restore(rdr, stdin);
                            ConsoleOutput.Restore(wtr, stdout); 
                        }

                        rdr = new StringReader(wtr.ToString());
                    }

                    stdin = ConsoleInput.Capture(rdr);
                    try { chain.Next(args); }
                    finally
                    {
                        ConsoleInput.Restore(rdr, stdin);
                    }
                }
            }
        }
    }
}
