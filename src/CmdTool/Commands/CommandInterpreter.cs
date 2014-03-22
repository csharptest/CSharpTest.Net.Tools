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
using System.ComponentModel;
using System.Threading;
using System.Reflection;
using System.Text.RegularExpressions;
using System.IO;
using CommandTypes = global::CSharpTest.Net.Commands.DefaultCommands;
using System.Diagnostics;

namespace CSharpTest.Net.Commands
{
	/// <summary>
	/// The primary class involved in providing a command-line interpreter.
	/// </summary>
	public partial class CommandInterpreter : ICommandInterpreter
	{
        readonly Dictionary<string, ICommand> _commands;
		readonly Dictionary<string, IOption> _options;
		readonly List<ICommandFilter> _filters;
		readonly BuiltInCommands _buildInCommands;

		private ReadNextCharacter _fnNextCh;
		private ICommandChain _head;
		private string _prompt;
		private string _filterPrecedence;

	    /// <summary>
		/// Constructs a command-line interpreter from the objects and/or System.Types provided.
		/// </summary>
		public CommandInterpreter(params object[] handlers)
			: this(CommandTypes.Default, handlers) { }
		/// <summary>
		/// Constructs a command-line interpreter from the objects and/or System.Types provided.
		/// </summary>
		public CommandInterpreter(DefaultCommands defaultCmds, params object[] handlers)
		{
			_head = null;
			_prompt = "> ";
			_commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
			_options = new Dictionary<string, IOption>(StringComparer.OrdinalIgnoreCase);
			_filters = new List<ICommandFilter>();
			_fnNextCh = GetNextCharacter;

			//defaults to { Redirect, then Pipe, then everything else }
			_filterPrecedence = "<|*";

			_buildInCommands = new BuiltInCommands(
				Command.Make(this, this.GetType().GetMethod("Get")),
				Command.Make(this, this.GetType().GetMethod("Set", new Type[] { typeof(string), typeof(object), typeof(bool) } )),
				Command.Make(this, this.GetType().GetMethod("Help", new Type[] { typeof(string), typeof(bool) } )),
				Option.Make(this, this.GetType().GetProperty("ErrorLevel")),
				Option.Make(this, this.GetType().GetProperty("Prompt"))
			);

			_buildInCommands.Add(this, defaultCmds);

			foreach (object o in handlers)
				AddHandler(o);
		}

		#region AddHandler, AddCommand, AddOption

		/// <summary>
		/// Adds the static methods to the command list, and static properties to the list of
		/// global options (used with commands set/get)
		/// </summary>
		public void AddHandler(Type targetObject)
		{ this.AddHandler<Type>(targetObject); }

		/// <summary>
		/// Adds the instance methods to the command list, and instance properties to the list of
		/// global options (used with commands set/get)
		/// </summary>
		public void AddHandler<T>(T targetObject) where T : class
		{
			BindingFlags flags = BindingFlags.Public | BindingFlags.IgnoreCase;
			Type type = targetObject as Type;
			if (type == null)
			{
				flags |= BindingFlags.Instance;
				type = targetObject.GetType();
			}
			else
				flags |= BindingFlags.Static;

			MethodInfo[] methods = type.GetMethods(flags | BindingFlags.InvokeMethod);
			foreach (MethodInfo method in methods)
			{
				if (method.IsSpecialName || method.DeclaringType == typeof(Object) ||
                    method.GetCustomAttributes(typeof(IgnoreMemberAttribute),true).Length > 0)
					continue;
				ICommand command = Command.Make(targetObject, method);
				if (command is ICommandFilter)
					AddFilter((ICommandFilter)command);
				else
					AddCommand(command);
			}
			PropertyInfo[] props = type.GetProperties(flags | BindingFlags.GetProperty | BindingFlags.SetProperty);
			foreach (PropertyInfo prop in props)
			{
                if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length > 0 ||
                    prop.GetCustomAttributes(typeof(IgnoreMemberAttribute), true).Length > 0)
					continue;
				AddOption(Option.Make(targetObject, prop));
			}
		}

		/// <summary> Manually adds a command </summary>
		public void AddCommand(ICommand command)
		{
			foreach (string key in command.AllNames)
			{
				if (String.IsNullOrEmpty(key))
					continue;

				InterpreterException.Assert(false == _commands.ContainsKey(key), "Command {0} already exists.", key);
				_commands.Add(key, command);
			}
		}
		
		/// <summary> Manually remove a command </summary>
		public void RemoveCommand(ICommand command)
		{
			foreach (string key in command.AllNames)
			{
				if (String.IsNullOrEmpty(key))
					continue;
				_commands.Remove(key);
			}
		}

		/// <summary>
		/// Adds a command 'filter' that is called for every command invoked enabling custom processing 
		/// of arguments and pre/post processing.
		/// </summary>
		public void AddFilter(ICommandFilter filter)
		{
			_filters.Remove(filter);
			_filters.Add(filter);
			_head = null;
		}
		
		/// <summary> Manually adds an option </summary>
		public void AddOption(IOption option)
		{
			foreach (string key in option.AllNames)
			{
				InterpreterException.Assert(false == _options.ContainsKey(key), "Option {0} already exists.", key);
				_options.Add(key, option);
			}
		}
		
		#endregion

		/// <summary> Gets/sets the exit code of the operation/process </summary>
		[Option(Category = "Built-in", Description = "Gets or sets the exit code of the operation.")]
		public int ErrorLevel { get { return Environment.ExitCode; } set { Environment.ExitCode = value; } }

		/// <summary> Gets/sets the prompt, use "$(OptionName)" to reference options </summary>
		[Option(Category = "Built-in", Description = "Gets or sets the text to display to prompt for input use \"$(OptionName)\" to reference options.")]
		public string Prompt { get { return _prompt; } set { _prompt = value ?? String.Empty; } }

		/// <summary>
		/// Lists all the commands that have been added to the interpreter
		/// </summary>
		public ICommand[] Commands
		{
			get
			{
				List<ICommand> cmds = new List<ICommand>();
				foreach (ICommand item in _commands.Values)
					if (!cmds.Contains(item)) cmds.Add(item);
				cmds.Sort(new OrderByName<ICommand>());
				return cmds.ToArray();
			}
		}
		/// <summary>
		/// Lists all the options that have been added to the interpreter, use the set/get commands
		/// to modify their values.
		/// </summary>
		public IOption[] Options
		{
			get
			{
				List<IOption> opts = new List<IOption>();
				foreach (IOption item in _options.Values)
					if (!opts.Contains(item)) opts.Add(item);
				opts.Sort(new OrderByName<IOption>());
				return opts.ToArray();
			}
		}
		/// <summary> Lists all the filters that have been added to the interpreter </summary>
		public ICommandFilter[] Filters
		{
			get
			{
				List<ICommandFilter> filters = new List<ICommandFilter>(_filters);
				filters.Sort(new OrderByName<ICommandFilter>());
				return filters.ToArray();
			}
		}

	    /// <summary>
	    /// Returns true if the command was found and cmd output parameter is set.
	    /// </summary>
	    public bool TryGetCommand(string name, out ICommand cmd)
	    {
	        return _commands.TryGetValue(name, out cmd);
	    }

	    /// <summary>
	    /// Returns true if the command was found and cmd output parameter is set.
	    /// </summary>
	    public bool TryGetOption(string name, out IOption cmd)
        {
            return _options.TryGetValue(name, out cmd);
        }

	    /// <summary> Command to get an option value </summary>
		[Command(Category = "Built-in", Description = "Gets a global option by name")]
		public object Get(string property)
		{
			IOption opt;
			InterpreterException.Assert(_options.TryGetValue(property, out opt), "The option {0} was not found.", property);
			object value = opt.Value;
			Console.Out.WriteLine("{0}", value);
			return value;
		}

	    /// <summary>
	    /// Sets all options to their defined DefaultValue if supplied.
	    /// </summary>
	    public void SetDefaults()
	    {
	        foreach (var opt in Options)
	        {
                if (!ReferenceEquals(null, opt.DefaultValue))
                    opt.Value = opt.DefaultValue;
	        }
	    }

	    /// <summary> Command to set the value of an option </summary>
        [IgnoreMember]
		public void Set(string property, object value) { Set(property, value, false); }

		/// <summary> Command to set the value of an option </summary>
		[Command(Category = "Built-in", Description = "Sets a global option by name or lists options available.")]
		public void Set([DefaultValue(null)] string property, [DefaultValue(null)] object value, 
			[DefaultValue(false),Description("Read from std::in lines formatted as NAME=VALUE")]bool readInput)
		{
			if (readInput)
			{
				string line;
				while (null != (line = Console.In.ReadLine()))
					Set(line, null, false);
				return;
			}
			if (property == null)
			{
				foreach (IOption opt in Options)
					Console.WriteLine("{0}={1}", opt.DisplayName, opt.Value);
				return;
			}
			else if (value == null && property.IndexOf('=') < 0)
			{
				Get(property);
				return;
			}
			else if (value == null)
			{
				string[] args = property.Split(new char[] { '=' }, 2);
				property = args[0].TrimEnd();
				value = args[1].TrimStart();
			}

			IOption option;
			InterpreterException.Assert(_options.TryGetValue(property, out option), "The option {0} was not found.", property);
			option.Value = value;
		}

		/// <summary>
		/// The last link in the command chain
		/// </summary>
		internal void ProcessCommand(string[] arguments)
		{
            if (arguments == null || arguments.Length == 0)
			{
				Help(null);
				return;
			}

			string commandName = arguments[0];

			ICommand command;
			InterpreterException.Assert(_commands.TryGetValue(commandName, out command), "Invalid command name: {0}", commandName);

			List<string> args = new List<string>();
			for (int i = 1; i < arguments.Length; i++)
				args.Add(ExpandOptions(arguments[i]));

			command.Run(this, args.ToArray());
		}

        /// <summary> Used to stop running the interpreter </summary>
        public sealed class QuitException : OperationCanceledException { }

		[Command("Quit", "Exit", Visible = false)]
		private void Quit() { throw new QuitException(); }

		/// <summary> called to handle error events durring processing </summary>
		protected virtual void OnError(Exception error)
		{
			if(error is OperationCanceledException)
			{/* Silent */}
			else
				Console.Error.WriteLine(error is ApplicationException ? error.Message : error.ToString());

			if (ErrorLevel == 0)
				ErrorLevel = 1;
		}

		/// <summary> Defines the filter precedence by appearance order of key character </summary>
		public string FilterPrecedence
		{
			get { return _filterPrecedence; }
			set { _filterPrecedence = value ?? String.Empty; _head = null; }
		}

		/// <summary> returns the chained filters </summary>
		private ICommandChain GetHead()
		{
			ICommandChain chain = _head;
			if (chain == null)
			{
				chain = new LastFilter(this);
				List<ICommandFilter> filters = new List<ICommandFilter>(_filters);
				filters.Sort(PrecedenceOrder);
				filters.Reverse();//add in reverse order
				foreach (ICommandFilter filter in filters)
					chain = new FilterChainItem(this, filter, chain);
				_head = chain;
			}
			return chain;
		}

		/// <summary> Compares the command filters in order of precendence </summary>
		private int PrecedenceOrder(ICommandFilter x, ICommandFilter y)
		{
			int posX = _filterPrecedence.IndexOfAny(x.Keys);
			posX = posX >= 0 ? posX : int.MaxValue;
			int posY = _filterPrecedence.IndexOfAny(y.Keys);
			posY = posY >= 0 ? posY : int.MaxValue;
			return posX.CompareTo(posY);
		}

		/// <summary> 
		/// Run the command whos name is the first argument with the remaining arguments provided to the command
		/// as needed.
		/// </summary>
		public void Run(params string[] arguments)
		{
            try
            {
                GetHead().Next(arguments ?? new string[0]);
            }
            catch (System.Threading.ThreadAbortException) { throw; }
            catch (QuitException) { throw; }
            catch (Exception e)
            {
                OnError(e);
            }
		}

        /// <summary> 
        /// Run the command whos name is the first argument with the remaining arguments provided to the command
        /// as needed.
        /// </summary>
        public void Run(string[] arguments, TextWriter mapstdout, TextWriter mapstderr, TextReader mapstdin)
        {
            TextWriter stdout = ConsoleOutput.Capture(mapstdout);
            TextWriter stderr = ConsoleError.Capture(mapstderr);
            TextReader stdin = ConsoleInput.Capture(mapstdin);
            try
            {
                GetHead().Next(arguments ?? new string[0]);
            }
            finally
            {
                ConsoleOutput.Restore(mapstdout, stdout);
                ConsoleError.Restore(mapstderr, stderr);
                ConsoleInput.Restore(mapstdin, stdin);
            }
        }

		/// <summary>
		/// Runs each line from the reader until EOF, can be used with Console.In
		/// </summary>
		public void Run(System.IO.TextReader input)
		{
			ICommand quit = Command.Make(this, this.GetType().GetMethod("Quit", BindingFlags.NonPublic| BindingFlags.Instance | BindingFlags.InvokeMethod));
			AddCommand(quit);

			try
			{
				while (true)
				{
					try
					{
						Console.Write(ExpandOptions(Prompt));

						string nextLine = input.ReadLine();
						if (nextLine == null)
							break;

						string[] arguments = ArgumentList.Parse(nextLine);
						Run(arguments);
					}
					catch (System.Threading.ThreadAbortException) { throw; }
					catch (QuitException) { break; }
					catch (Exception e)
					{
						OnError(e);
						return;
					}
					Console.WriteLine();
				}
			}
			finally
			{
				RemoveCommand(quit);
			}
		}

		static readonly Regex _optionName = new Regex(@"(?<!\$)\$\((?<Name>[\w]+)\)");
		/// <summary>
		/// Expands '$(OptionName)' within the input string to the named option's value.
		/// </summary>
		public string ExpandOptions(string input)
		{
			// replaces $(OptionName) with value of OptionName
            return _optionName.Replace(input,
				delegate(Match m)
				{
					string optionName = m.Groups["Name"].Value;
					InterpreterException.Assert(_options.ContainsKey(optionName), "Unknown option specified: {0}", optionName);
					return String.Format("{0}", _options[optionName].Value);
				}
			).Replace("$$", "$");
		}

		/// <summary> Default inplementation of get keystroke </summary>
		private Char GetNextCharacter() 
		{
			return Console.ReadKey(true).KeyChar; 
		}

		/// <summary>
		/// Reads a keystroke, not from the std:in stream, rather from the console or ui.
		/// </summary>
		public ReadNextCharacter ReadNextCharacter
		{
			get { return _fnNextCh; }
			set 
            {
                if (value == null) throw new ArgumentNullException();
                _fnNextCh = value; 
            }
		}

        /// <summary>
        /// Adds the specified attribute to every command argument by the given name.
        /// </summary>
	    public void AddGlobalArgumentAttribute(string argumentName, Attribute attribute)
	    {
	        foreach (var command in _commands.Values)
	        {
	            foreach (var argument in command.Arguments)
	            {
	                if (argument.DisplayName == argumentName)
	                    argument.AddAttribute(attribute);
	            }
	        }
	    }

        #region ConsoleWriter/ConsoleOutput/ConsoleError/ConsoleInput
        private abstract class ConsoleWriter : TextWriter
        {
            protected abstract TextWriter Writer { get; }
            public override void Close() { Writer.Close(); }
            protected override void Dispose(bool disposing) { }
            public override void Flush() { Writer.Flush(); }
            public override void Write(char value) { Writer.Write(value); }
            public override void Write(char[] buffer) { Writer.Write(buffer); }
            public override void Write(char[] buffer, int index, int count) { Writer.Write(buffer, index, count); }
            public override void Write(string value) { Writer.Write(value); }
            public override System.Text.Encoding Encoding { get { return Writer.Encoding; } }
        }
        private sealed class ConsoleOutput : ConsoleWriter
        {
            private static readonly ConsoleOutput _instance = new ConsoleOutput();
            private static TextWriter _default, _expected;
            private static TextWriter _global;
            private static int _referenceCount = 0;
            [ThreadStatic]
            private static TextWriter _writer;
            public static TextWriter Capture(TextWriter output)
            {
                if (output == null) return null;
                lock (typeof (Console))
                {
                    if (1 == Interlocked.Increment(ref _referenceCount))
                    {
                        _default = Console.Out;
                        Console.SetOut(_instance);
                        _expected = Console.Out;
                    }
                    else if (!ReferenceEquals(_expected, Console.Out))
                    {
                        Console.SetOut(_instance);
                        _expected = Console.Out;
                    }

                    Interlocked.Exchange(ref _global, output);
                    return Interlocked.Exchange(ref _writer, output);
                }
            }
            public static void Restore(TextWriter replaced, TextWriter original)
            {
                if (replaced == null) return;
                lock (typeof (Console))
                {
                    Interlocked.CompareExchange(ref _writer, original, replaced);
                    Interlocked.CompareExchange(ref _global, original, replaced);

                    if (0 == Interlocked.Decrement(ref _referenceCount))
                    {
                        Console.SetOut(_default);
                        _default = null;
                    }
                    else if (!ReferenceEquals(_expected, Console.Out))
                    {
                        Console.SetOut(_instance);
                        _expected = Console.Out;
                    }
                }
            }
            protected override TextWriter Writer { get { return _writer ?? _global ?? _default; } }
	    }
        private sealed class ConsoleError : ConsoleWriter
        {
            private static readonly ConsoleError _instance = new ConsoleError();
            private static TextWriter _default, _expected;
            private static TextWriter _global;
            private static int _referenceCount = 0;
            private ConsoleError() { }
            [ThreadStatic]
            private static TextWriter _writer;
            public static TextWriter Capture(TextWriter output)
            {
                if (output == null) return null;
                lock (typeof(Console))
                {
                    if (1 == Interlocked.Increment(ref _referenceCount))
                    {
                        _default = Console.Error;
                        Console.SetError(_instance);
                        _expected = Console.Error;
                    }
                    else if (!ReferenceEquals(_expected, Console.Error))
                    {
                        Console.SetError(_instance);
                        _expected = Console.Error;
                    }

                    Interlocked.Exchange(ref _global, output);
                    return Interlocked.Exchange(ref _writer, output);
                }
            }
            public static void Restore(TextWriter replaced, TextWriter original)
            {
                if (replaced == null) return;
                lock (typeof(Console))
                {
                    Interlocked.CompareExchange(ref _writer, original, replaced);
                    Interlocked.CompareExchange(ref _global, original, replaced);

                    if (0 == Interlocked.Decrement(ref _referenceCount))
                    {
                        Console.SetError(_default);
                        _default = null;
                    }
                    else if (!ReferenceEquals(_expected, Console.Error))
                    {
                        Console.SetError(_instance);
                        _expected = Console.Error;
                    }
                }
            }
            protected override TextWriter Writer { get { return _writer ?? _global ?? _default; } }
        }
        private sealed class ConsoleInput : TextReader
        {
            private static readonly ConsoleInput _instance = new ConsoleInput();
            private static TextReader _default, _expected;
            private static TextReader _global;
            private static int _referenceCount = 0;
            private ConsoleInput() { }
            [ThreadStatic]
            private static TextReader _reader;
            public static TextReader Capture(TextReader output)
            {
                if (output == null) return null;
                lock (typeof(Console))
                {
                    if (1 == Interlocked.Increment(ref _referenceCount))
                    {
                        _default = Console.In;
                        Console.SetIn(_instance);
                        _expected = Console.In;
                    }
                    else if (!ReferenceEquals(_expected, Console.In))
                    {
                        Console.SetIn(_instance);
                        _expected = Console.In;
                    }

                    Interlocked.Exchange(ref _global, output);
                    return Interlocked.Exchange(ref _reader, output);
                }
            }
            public static void Restore(TextReader replaced, TextReader original)
            {
                if (replaced == null) return;
                lock (typeof(Console))
                {
                    Interlocked.CompareExchange(ref _reader, original, replaced);
                    Interlocked.CompareExchange(ref _global, original, replaced);

                    if (0 == Interlocked.Decrement(ref _referenceCount))
                    {
                        Console.SetIn(_default);
                        _default = null;
                    }
                    else if (!ReferenceEquals(_expected, Console.In))
                    {
                        Console.SetIn(_instance);
                        _expected = Console.In;
                    }
                }
            }
            private TextReader Reader { get { return _reader ?? _global ?? _default; } }
            public override void Close() { Reader.Close(); }
            protected override void Dispose(bool disposing) { }
            public override int Peek() { return Reader.Peek(); }
            public override int Read() { return Reader.Read(); }
            public override int Read(char[] buffer, int index, int count) { return Reader.Read(buffer, index, count); }
            public override string ReadToEnd() { return Reader.ReadToEnd(); }
            public override int ReadBlock(char[] buffer, int index, int count) { return Reader.ReadBlock(buffer, index, count); }
            public override string ReadLine() { return Reader.ReadLine(); }
        }
        #endregion
    }

}
