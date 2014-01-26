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

namespace CSharpTest.Net.Commands
{
	/// <summary>
	/// Defines an alias name for a command
	/// </summary>
	[Serializable]
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
	public class AliasNameAttribute : Attribute
	{
		private readonly string _alias;
		/// <summary> Constructs an AliasNameAttribute </summary>
		public AliasNameAttribute(string commandAlias)
		{
			_alias = commandAlias;
		}

		/// <summary> Returns the name of the alias </summary>
		public string Name { get { return _alias; } }
	}

    /// <summary>
    /// Instructs the CommandInterpreter to ignore a specific method/property
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    public class IgnoreMemberAttribute : Attribute
    {
        /// <summary> Constructs an IgnoreMemberAttribute </summary>
        public IgnoreMemberAttribute()
        {
        }
    }
	/// <summary>
	/// Defines that the string[] argument accepts all arguments provided to the command, useage:
	/// <code>void MyCommand([AllArguments] string[] arguments)</code>
	/// or 
	/// <code>void MyCommand([AllArguments] string[] arguments, ICommandInterpreter ci)</code>
	/// </summary>
	[Serializable]
	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
	public class AllArgumentsAttribute : Attribute
	{
		/// <summary> Constructs an AllArgumentsAttribute </summary>
		public AllArgumentsAttribute()
		{ }
	}

	/// <summary>
	/// Provides all the display properties.
	/// </summary>
	public abstract class DisplayInfoAttribute : Attribute, IDisplayInfo
	{
		private string _displayName;
		private string[] _aliasNames;
		private string _category;
		private string _description;
		private bool _visible;

		/// <summary> Constructs the attribute </summary>
		protected DisplayInfoAttribute(string displayName, params string[] aliasNames)
		{
			_displayName = displayName;
			_aliasNames = aliasNames;
			_category = _description = null;
			_visible = true;
		}

		/// <summary> Returns the DisplayName </summary>
		public string DisplayName { get { return _displayName; } set { _displayName = value; } }
		/// <summary> Just the alias names </summary>
		public string[] AliasNames { get { return (string[])_aliasNames.Clone(); } set { _aliasNames = Check.NotNull(value); } }
		/// <summary> Returns the name list </summary>
		public string[] AllNames
		{
			get
			{
				List<string> names = new List<string>();
				if (_displayName != null) names.Add(_displayName);
				names.AddRange(_aliasNames);
				return names.ToArray();
			}
		}
		/// <summary> Returns the Category </summary>
		public string Category { get { return _category; } set { _category = value; } }
		/// <summary> Returns the Description </summary>
		public string Description { get { return _description; } set { _description = value; } }
		/// <summary> Returns the visibility of the command </summary>
		public virtual bool Visible { get { return _visible; } set { _visible = value; } }

        Type IDisplayInfo.ReflectedType { get { return null; } }
        void IDisplayInfo.AddAttribute<T>(T attribute) { }
	    bool IDisplayInfo.TryGetAttribute<T>(out T found)
	    {
	        found = null;
	        return false;
	    }

        void IDisplayInfo.Help() { }
	}

	/// <summary> Contains display info and a default value </summary>
	public abstract class DisplayInfoAndValueAttribute : DisplayInfoAttribute
	{
		private object _defaultValue;
		private bool _hasDefault;
		
		/// <summary> Constructs the attribute </summary>
		protected DisplayInfoAndValueAttribute(string displayName, params string[] aliasNames)
			: base(displayName, aliasNames)
		{ }

		/// <summary> Gets/sets the default value for the option </summary>
		public object DefaultValue { get { return _defaultValue; } set { _hasDefault = true; _defaultValue = value; } }

		/// <summary> Returns true if a default value was specified </summary>
		internal bool HasDefault { get { return _hasDefault; } }
	}

	/// <summary>
	/// Provides all the properties available for a command 'filter' that is
	/// called for every command invoked enabling custom processing of arguments
	/// and pre/post processing.  The attribute is optional, the format of the
	/// the method prototype is not and must be:
	/// <code>void (ICommandInterpreter interpreter, ICommandChain chain, string[] arguments);</code>
	/// </summary>
	[Serializable]
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class CommandFilterAttribute : DisplayInfoAttribute
	{
		char[] _keys;

		/// <summary> Constructs the attribute </summary>
		public CommandFilterAttribute(char key)
			: this(new char[] { key})
		{ }

		/// <summary> Constructs the attribute </summary>
		public CommandFilterAttribute( params char[] keys )
			: base(null, new string[0])
		{
			_keys = keys;
			base.Visible = false;
		}

		/// <summary> Returns the keys associated with this filter </summary>
		public Char[] Keys { get { return _keys; } }

		/// <summary> Ignored. </summary>
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public override bool Visible { get { return false; } set { } }
	}
	/// <summary>
	/// Provides all the properties available for a command.
	/// </summary>
	[Serializable]
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class CommandAttribute : DisplayInfoAttribute
	{
		/// <summary> Constructs the attribute </summary>
		public CommandAttribute()
			: base(null, new string[0])
		{ }
		/// <summary> Constructs the attribute </summary>
		public CommandAttribute(string displayName)
			: base(displayName, new string[0])
		{ }
		/// <summary> Constructs the attribute </summary>
		public CommandAttribute(string displayName, params string[] aliasNames)
			: base(displayName, aliasNames)
		{ }
	}
	/// <summary>
	/// Provides all the properties available for an argument.
	/// </summary>
	[Serializable]
	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
	public class ArgumentAttribute : DisplayInfoAndValueAttribute
	{
		/// <summary> Constructs the attribute </summary>
		public ArgumentAttribute()
			: base(null, new string[0])
		{ }
		/// <summary> Constructs the attribute </summary>
		public ArgumentAttribute(string displayName)
			: base(displayName, new string[0])
		{ }
		/// <summary> Constructs the attribute </summary>
		public ArgumentAttribute(string displayName, params string[] aliasNames)
			: base(displayName, aliasNames)
		{ }
	}
	/// <summary>
	/// Provides all the properties available for an argument.
	/// </summary>
	[Serializable]
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class OptionAttribute : DisplayInfoAndValueAttribute
	{
		/// <summary> Constructs the attribute </summary>
		public OptionAttribute()
			: base(null, new string[0])
		{ }
		/// <summary> Constructs the attribute </summary>
		public OptionAttribute(string displayName)
			: base(displayName, new string[0])
		{ }
		/// <summary> Constructs the attribute </summary>
		public OptionAttribute(string displayName, params string[] aliasNames)
			: base(displayName, aliasNames)
		{ }
	}
}
