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

namespace CSharpTest.Net.Commands
{
    /// <summary>
	/// Used for obtaining input directly from user rather than from the std:in stream
	/// </summary>
	public delegate Char ReadNextCharacter();

	/// <summary>
	/// Defines the interface for the command interpreter.  If you use this as a parameter
	/// it will be provided auto-magically to your command.  To avoid conflicts with ordinal
	/// argument matching, make this your last argument. 
	/// </summary>
	public interface ICommandInterpreter
	{
		/// <summary> 
		/// Gets/sets the exit code of the operation/process
		/// </summary>
		int ErrorLevel { get; set; }

		/// <summary> 
		/// Gets/sets the prompt, use "$(OptionName)" to reference options
		/// </summary>
		string Prompt { get; set; }

		/// <summary>
		/// Lists all the commands that have been added to the interpreter
		/// </summary>
		ICommand[] Commands { get; }

        /// <summary>
        /// Returns true if the command was found and cmd output parameter is set.
        /// </summary>
	    bool TryGetCommand(string name, out ICommand cmd);

		/// <summary>
		/// Lists all the options that have been added to the interpreter, use the set/get commands
		/// to modify their values.
		/// </summary>
		IOption[] Options { get; }

        /// <summary>
        /// Returns true if the command was found and cmd output parameter is set.
        /// </summary>
        bool TryGetOption(string name, out IOption cmd);

		/// <summary> 
		/// Command to get an option value by name
		/// </summary>
		object Get(string property);

		/// <summary> 
		/// Command to set the value of an option value by name
		/// </summary>
		void Set(string property, object value);

		/// <summary> 
		/// Run the command whos name is the first argument with the remaining arguments provided to the command
		/// as needed.
		/// </summary>
		void Run(params string[] arguments);

        /// <summary>
        /// Run the command whos name is the first argument with the remaining arguments provided to the command
        /// as needed.
        /// </summary>
	    void Run(string[] arguments, TextWriter mapstdout, TextWriter mapstderr, TextReader mapstdin);

		/// <summary>
		/// Runs each line from the reader until EOF, can be used with Console.In
		/// </summary>
		void Run(System.IO.TextReader input);

		/// <summary>
		/// Expands '$(OptionName)' within the input string to the named option's value.
		/// </summary>
		string ExpandOptions(string input);

		/// <summary>
		/// Reads a keystroke, not from the std:in stream, rather from the console or ui.
		/// </summary>
		ReadNextCharacter ReadNextCharacter { get; }

        /// <summary>
        /// Returns an HTML document for help on all items (when item == null) or a specific item.
        /// </summary>
	    string GetHtmlHelp(string item);

        /// <summary>
        /// Sets all options to their defined DefaultValue if supplied.
        /// </summary>
	    void SetDefaults();
	}

	/// <summary>
	/// Defines an interface that allows a command filter to call to next filter in the chain
	/// </summary>
	public interface ICommandChain
	{
		/// <summary>
		/// Calls the next command filter in the chain, eventually processing the command
		/// </summary>
		void Next(string[] arguments);
	}

	/// <summary> A base interface that provides name and display information </summary>
	public interface IDisplayInfo
	{
        /// <summary> Returns the type this was reflected from, or null if created without reflection </summary>
	    Type ReflectedType { get; }
	    /// <summary> Returns the display name of the item </summary>
		string DisplayName { get; }
		/// <summary> Returns the name of the item </summary>
		string[] AllNames { get; }
		/// <summary> Returns the category if defined, or the type name if not </summary>
		string Category { get; }
		/// <summary> Returns the description of the item </summary>
		string Description { get; }
		/// <summary> Returns true if the items should be displayed. </summary>
        bool Visible { get; set; }
        /// <summary> Dynamically adds an attribute to the item. </summary>
        void AddAttribute<T>(T attribute) where T : Attribute;
	    /// <summary> Returns true if the attribute was found </summary>
	    bool TryGetAttribute<T>(out T found) where T : Attribute;
		/// <summary> Renders the help information to Console.Out </summary>
		void Help();
	}

	/// <summary>
	/// Represents a static or instance method that will be invoked as a command
	/// </summary>
	public interface IArgument : IDisplayInfo
	{
		/// <summary> Returns true if the argument is required </summary>
		bool Required { get; }
		/// <summary> Returns the default value if Required == false </summary>
		object DefaultValue { get; }
		/// <summary> Returns the type of the argument </summary>
        Type Type { get; }
        /// <summary> Returns true if the property is a boolean switch </summary>
	    bool IsFlag { get; }
        /// <summary> Returns true if this parameter is of type ICommandInterpreter </summary>
        bool IsInterpreter { get; }
        /// <summary> Returns true if this parameter is decorated with the [AllArguments] attribute </summary>
        bool IsAllArguments { get; }
        /// <summary> Writes the default syntax formatting for the argument using the provided name/alias </summary>
	    string FormatSyntax(string name);
	}

	/// <summary>
	/// Represents a static or instance method that will be invoked as a command
	/// </summary>
	public interface ICommand : IDisplayInfo
	{
		/// <summary> Returns the arguments defined on this command. </summary>
		IArgument[] Arguments { get; }

		/// <summary> Runs this command with the supplied arguments </summary>
		void Run(ICommandInterpreter interpreter, string[] arguments);
	}

	/// <summary>
	/// Represents a static or instance method that is used to filter or pre/post process commands
	/// </summary>
	public interface ICommandFilter : ICommand
	{
		/// <summary> Returns the possible character keys for this filter when setting the precedence </summary>
		Char[] Keys { get; } 

		/// <summary>
		/// Used to run a command through a set of filters, call chain.Next() to continue processing
		/// </summary>
		/// <param name="interpreter">The command interpreter running the command</param>
		/// <param name="chain">The next link in the chain of filters</param>
		/// <param name="arguments">The input arguments to the command-line</param>
		void Run(ICommandInterpreter interpreter, ICommandChain chain, string[] arguments);
	}

	/// <summary>
	/// Defines an Option that can be configued/set independantly of the commands.  Used with the set/get
	/// commands defined by the interpreter.
	/// </summary>
	public interface IOption : IDisplayInfo
	{
		/// <summary>
		/// Gets/sets the value of the option
		/// </summary>
		object Value { get; set; }
		/// <summary> Returns the type of the option value </summary>
		Type Type { get; }
        /// <summary> Returns the default value or NULL if undefined </summary>
	    object DefaultValue { get; }
	}

	// Internal sorter for display name
	class OrderByName<T> : IComparer<T>
		where T : IDisplayInfo
	{
		int IComparer<T>.Compare(T a, T b)
		{ return StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName); }
	}
}
