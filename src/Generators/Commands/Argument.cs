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
using System.Reflection;
using System.ComponentModel;
using CSharpTest.Net.Utils;
using System.Text;

namespace CSharpTest.Net.Commands
{
	[System.Diagnostics.DebuggerDisplay("{Parameter}")]
	partial class Argument : DisplayInfoBase, IArgument
	{
		readonly object _default;
		readonly bool _required;
		readonly bool _allArguments;

		public Argument(object target, ParameterInfo mi)
			: base(target, mi)
		{
			_default = null;
			_required = true;
			_allArguments = Parameter.IsDefined(typeof(AllArgumentsAttribute), true);

			foreach (DefaultValueAttribute a in mi.GetCustomAttributes(typeof(DefaultValueAttribute), true))
			{
				_default = a.Value;
				_required = false;
			}

			foreach (ArgumentAttribute a in mi.GetCustomAttributes(typeof(ArgumentAttribute), true))
			{
				if (a.HasDefault)
				{
					_required = false;
					_default = a.DefaultValue;
				}
			}
		}

		private ParameterInfo Parameter { get { return (ParameterInfo)base.Member; } }

		public Type Type { get { return Parameter.ParameterType; } }

		public override bool Visible { get { return base.Visible && !IsInterpreter && !IsAllArguments; } }
		public bool Required { get { return _required; } }
		public bool IsFlag { get { return Parameter.ParameterType == typeof(bool); } }
		public bool IsInterpreter { get { return Parameter.ParameterType == typeof(ICommandInterpreter); } }
		public bool IsAllArguments { get { return _allArguments; } }
		public Object DefaultValue { get { return _default; } }
        
	    internal Object GetArgumentValue(ICommandInterpreter interpreter, ArgumentList args, string[] allArguments)
		{
			object value = null;

			if (IsInterpreter)
				return interpreter;

			if (IsAllArguments)
			{
				args.Clear();
				args.Unnamed.Clear();
				return allArguments;
			}

			foreach (string name in AllNames)
			{
				ArgumentList.Item argitem;
				if (args.TryGetValue(name, out argitem))
				{
					if (Parameter.ParameterType == typeof(string[]))
						value = argitem.Values;
					else if (IsFlag)
					{
						bool result;
						value = (String.IsNullOrEmpty(argitem.Value) || (bool.TryParse(argitem.Value, out result) && result));
					}
					else
						value = argitem.Value;
					args.Remove(name);
				}
			}

			return base.ChangeType(value, Parameter.ParameterType, Required, DefaultValue); 
		}
	}
}
