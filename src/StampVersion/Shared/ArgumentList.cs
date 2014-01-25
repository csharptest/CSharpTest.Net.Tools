#region Copyright 2008-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Text.RegularExpressions;
using System.Text;

namespace CSharpTest.Net.Utils
{
	/// <summary>
	/// This is a private class as the means of sharing is to simply include the source file not
	/// reference a library.
	/// </summary>
	[System.Diagnostics.DebuggerNonUserCode]
    partial class ArgumentList : System.Collections.ObjectModel.KeyedCollection<string, ArgumentList.Item>
	{
		#region Static Configuration Options
		static StringComparer _defaultCompare = StringComparer.OrdinalIgnoreCase;
		static char[] _prefix = new char[] { '/', '-' };
		static char[] _delim = new char[] { ':', '=' };
		static readonly string[] EmptyList = new string[0];

        /// <summary>
        /// Controls the default string comparer used for this class
        /// </summary>
        public static StringComparer DefaultComparison
		{
			get { return _defaultCompare; }
			set
			{
				if (value == null) throw new ArgumentNullException();
				_defaultCompare = value;
			}
		}

        /// <summary>
        /// Controls the allowable prefix characters that will preceed named arguments
        /// </summary>
        public static char[] PrefixChars
		{
			get { return (char[])_prefix.Clone(); }
			set
			{
				if (value == null) throw new ArgumentNullException();
				if (value.Length == 0) throw new ArgumentOutOfRangeException();
				_prefix = (char[])value.Clone();
			}
        }
        /// <summary>
        /// Controls the allowable delimeter characters seperate argument names from values
        /// </summary>
        public static char[] NameDelimeters
		{
			get { return (char[])_delim.Clone(); }
			set
			{
				if (value == null) throw new ArgumentNullException();
				if (value.Length == 0) throw new ArgumentOutOfRangeException();
				_delim = (char[])value.Clone();
			}
		}
		#endregion Static Configuration Options

		readonly List<string> _unnamed;
		/// <summary>
		/// Initializes a new instance of the ArgumentList class using the argument list provided
		/// </summary>
        public ArgumentList(params string[] arguments) : this(DefaultComparison, arguments) { }
        /// <summary>
        /// Initializes a new instance of the ArgumentList class using the argument list provided
        /// and using the string comparer provided, by default this is case-insensitive
        /// </summary>
		public ArgumentList(StringComparer comparer, params string[] arguments)
			: base(comparer, 0)
		{
			_unnamed = new List<string>();
			this.AddRange(arguments);
		}

		/// <summary>
		/// Returns a list of arguments that did not start with a character in the PrefixChars
		/// static collection.  These arguments can be modified by the methods on the returned
		/// collection, or you set this property to a new collection (a copy is made).
		/// </summary>
		public IList<string> Unnamed
		{
			get { return _unnamed; }
			set 
			{
				_unnamed.Clear();
				if (value != null)
					_unnamed.AddRange(value);
			}
		}

		/// <summary>
		/// Parses the strings provided for switch names and optionally values, by default in one
		/// of the following forms: "/name=value", "/name:value", "-name=value", "-name:value"
		/// </summary>
		public void AddRange(params string[] arguments)
		{
			if (arguments == null) throw new ArgumentNullException();

			foreach (string arg in arguments)
			{
				string name, value;
				if (TryParseNameValue(arg, out name, out value))
					Add(name, value);
				else
					_unnamed.Add(CleanArgument(arg));
			}
		}

		/// <summary>
		/// Adds a name/value pair to the collection of arguments, if value is null the name is
		/// added with no values.
		/// </summary>
		public void Add(string name, string value)
		{
			if (name == null)
				throw new ArgumentNullException();

			Item item;
			if (!TryGetValue(name, out item))
				base.Add(item = new Item(name));

			if (value != null)
				item.Add(value);
		}

        /// <summary>
        /// A string collection of all keys in the arguments
        /// </summary>
		public string[] Keys
		{
			get
			{
				if (Dictionary == null) return new string[0];
				List<string> list = new List<string>(Dictionary.Keys);
				list.Sort();
				return list.ToArray();
			}
		}

		/// <summary>
		/// Returns true if the value was found by that name and set the output value
		/// </summary>
		public bool TryGetValue(string name, out Item value)
		{
			if (name == null)
				throw new ArgumentNullException();

			if (Dictionary != null)
				return Dictionary.TryGetValue(name, out value);
			value = null;
			return false;
		}

		/// <summary>
		/// Returns true if the value was found by that name and set the output value
		/// </summary>
		public bool TryGetValue(string name, out string value)
		{
			if (name == null)
				throw new ArgumentNullException();

			Item test;
			if (Dictionary != null && Dictionary.TryGetValue(name, out test))
			{ 
				value = test.Value; 
				return true; 
			}
			value = null;
			return false;
		}

		/// <summary>
		/// Returns an Item of name even if it does not exist
		/// </summary>
		public Item SafeGet(string name)
		{
			Item result;
			if (TryGetValue(name, out result))
				return result;
			return new Item(name, null);
		}

		#region Protected / Private operations...

		static string CleanArgument(string argument)
		{
			if (argument == null) throw new ArgumentNullException();
			if (argument.Length >= 2 && argument[0] == '"' && argument[argument.Length - 1] == '"')
				argument = argument.Substring(1, argument.Length - 2).Replace("\"\"", "\"");
			return argument;
		}

		/// <summary>
		/// Attempts to parse a name value pair from '/name=value' format
		/// </summary>
		public static bool TryParseNameValue(string argument, out string name, out string value)
		{
			argument = CleanArgument(argument);//strip quotes
			name = value = null;

			if (String.IsNullOrEmpty(argument) || 0 != argument.IndexOfAny(_prefix, 0, 1))
				return false;

			name = argument.Substring(1);
			if (String.IsNullOrEmpty(name))
				return false;

			int endName = name.IndexOfAny(_delim, 1);

			if (endName > 0)
			{
				value = name.Substring(endName + 1);
				name = name.Substring(0, endName);
			}

			return true;
		}

		/// <summary>
		/// Searches the arguments until it finds a switch or value by the name in find and
		/// if found it will:
		/// A) Remove the item from the arguments
		/// B) Set the out parameter value to any value found, or null if just '/name'
		/// C) Returns true that it was found and removed.
		/// </summary>
		public static bool Remove(ref string[] arguments, string find, out string value)
		{
			value = null;
			for (int i = 0; i < arguments.Length; i++)
			{
				string name, setting;
				if (TryParseNameValue(arguments[i], out name, out setting) &&
					_defaultCompare.Equals(name, find))
				{
					List<string> args = new List<string>(arguments);
					args.RemoveAt(i);
					arguments = args.ToArray();
					value = setting;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Abract override for extracting key
		/// </summary>
		protected override string GetKeyForItem(ArgumentList.Item item)
		{
			return item.Name;
		}

		#endregion

		#region Item class used for collection
		/// <summary>
		/// This is a single named argument within an argument list collection, this
		/// can be implicitly assigned to a string, or a string[] array
		/// </summary>
		[System.Diagnostics.DebuggerNonUserCode]
		public class Item : System.Collections.ObjectModel.Collection<string>
		{
			private readonly string _name;
			private readonly List<string> _values;

			/// <summary>
			/// Constructs an item for the name and values provided.
			/// </summary>
			public Item(string name, params string[] items)
				: this(new List<string>(), name, items) { }

			private Item(List<string> impl, string name, string[] items)
				: base(impl)
			{
				if (name == null)
					throw new ArgumentNullException();

				_name = name;
				_values = impl;
				if (items != null)
					_values.AddRange(items);
			}

			/// <summary>
			/// Returns the name of this item
			/// </summary>
			public string Name { get { return _name; } }

			/// <summary>
			/// Returns the first value of this named item or null if one doesn't exist
			/// </summary>
			public string Value
			{
				get { return _values.Count > 0 ? _values[0] : null; }
				set
				{
					_values.Clear();
					if (value != null)
						_values.Add(value);
				}
			}

			/// <summary>
			/// Returns the collection of items in this named slot
			/// </summary>
			public string[] Values
			{
				get { return _values.ToArray(); }
				set
				{
					_values.Clear();
					if (value != null)
						_values.AddRange(value);
				}
			}

			/// <summary>
			/// Same as the .Values property, returns the collection of items in this named slot
			/// </summary>
			/// <returns></returns>
			public string[] ToArray() { return _values.ToArray(); }
			/// <summary>
			/// Add one or more values to this named item
			/// </summary>
			public void AddRange(IEnumerable<string> items) { _values.AddRange(items); }

			/// <summary>
			/// Converts this item to key-value pair to rem to a dictionary
			/// </summary>
			public static implicit operator KeyValuePair<string, string[]>(Item item)
			{
				if (item == null) throw new ArgumentNullException();
				return new KeyValuePair<string, string[]>(item.Name, item.Values);
			}

			/// <summary>
			/// Converts this item to a string by getting the first value or null if none
			/// </summary>
			public static implicit operator string(Item item) { return item == null ? null : item.Value; }

			/// <summary>
			/// Converts this item to array of strings
			/// </summary>
			public static implicit operator string[](Item item) { return item == null ? null : item.Values; }
		}

		#endregion Item class used for collection

		private class ArgReader
		{
			const char CharEmpty = (char)0;
			char[] _chars;
			int _pos;
			public ArgReader(string data)
			{
				_chars = data.ToCharArray();
				_pos = 0;
			}

			public bool MoveNext() { _pos++; return _pos < _chars.Length; }
			public char Current { get { return (_pos < _chars.Length) ? _chars[_pos] : CharEmpty; } }
			public bool IsWhiteSpace { get { return Char.IsWhiteSpace(Current); } }
			public bool IsQuote { get { return (Current == '"'); } }
			public bool IsEOF { get { return _pos >= _chars.Length; } }
		}

		/// <summary> Parses the individual arguments from the given input string. </summary>
		public static string[] Parse(string rawtext)
		{
			List<String> list = new List<string>();
			if (rawtext == null)
				throw new ArgumentNullException("rawtext");
			ArgReader characters = new ArgReader(rawtext.Trim());

			while (!characters.IsEOF)
			{
				if (characters.IsWhiteSpace)
				{
					characters.MoveNext();
					continue;
				}
				
				StringBuilder sb = new StringBuilder();

				if (characters.IsQuote)
				{//quoted string
					while (characters.MoveNext())
					{
						if (characters.IsQuote)
						{
							if (!characters.MoveNext() || characters.IsWhiteSpace)
								break;
						}
						sb.Append(characters.Current);
					}
				}
				else
				{
					sb.Append(characters.Current);
					while (characters.MoveNext())
					{
						if (characters.IsWhiteSpace)
							break;
						sb.Append(characters.Current);
					}
				}

				list.Add(sb.ToString());
			}
			return list.ToArray();
		}

		/// <summary> The inverse of Parse, joins the arguments together and properly escapes output </summary>
		[Obsolete("Consider migrating to EscapeArguments as it correctly escapes some situations that Join does not.")]
		public static string Join(params string[] arguments)
		{
			if (arguments == null)
				throw new ArgumentNullException("arguments");
			char[] escaped = " \t\"&()[]{}^=;!'+,`~".ToCharArray();

			StringBuilder sb = new StringBuilder();
			foreach (string argument in arguments)
			{
				string arg = argument;

				if( arg.IndexOfAny(escaped) >= 0 ) 
					sb.AppendFormat("\"{0}\"", arg.Replace("\"", "\"\""));
				else
					sb.Append(arg);
	
				sb.Append(' ');
			}

			return sb.ToString(0, Math.Max(0, sb.Length-1));
		}

        /// <summary> The 'more' correct escape/join for arguments </summary>
        public static string EscapeArguments(params string[] args)
        {
            StringBuilder arguments = new StringBuilder();
            Regex invalidChar = new Regex("[\x00\x0a\x0d]");//  these can not be escaped
            Regex needsQuotes = new Regex(@"\s|""");//          contains whitespace or two quote characters
            Regex escapeQuote = new Regex(@"(\\*)(""|$)");//    one or more '\' followed with a quote or end of string
            for (int carg = 0; args != null && carg < args.Length; carg++)
            {
                if (args[carg] == null) { throw new ArgumentNullException("args[" + carg + "]"); }
                if (invalidChar.IsMatch(args[carg])) { throw new ArgumentOutOfRangeException("args[" + carg + "]"); }
                if (args[carg] == String.Empty) { arguments.Append("\"\""); }
                else if (!needsQuotes.IsMatch(args[carg])) { arguments.Append(args[carg]); }
                else
                {
                    arguments.Append('"');
                    arguments.Append(escapeQuote.Replace(args[carg], 
						delegate(Match m)
						{
							return m.Groups[1].Value + m.Groups[1].Value +
								  (m.Groups[2].Value == "\"" ? "\\\"" : "");
						}
                    ));
                    arguments.Append('"');
                }
                if (carg + 1 < args.Length)
                    arguments.Append(' ');
            }
            return arguments.ToString();
        }
	}
}