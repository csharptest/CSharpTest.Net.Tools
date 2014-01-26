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

namespace CSharpTest.Net.Commands
{
	[System.Diagnostics.DebuggerDisplay("{Property}")]
	partial class Option : DisplayInfoBase, IOption
	{
		readonly bool _required;
		readonly object _default;

		public static IOption Make(object target, PropertyInfo mi)
		{ return new Option(target, mi); }

		Option(object target, PropertyInfo mi)
			: base(target, mi)
		{
			_default = null;
			_required = true;

			foreach (DefaultValueAttribute a in mi.GetCustomAttributes(typeof(DefaultValueAttribute), true))
			{
				_required = false;
				this.Value = _default = a.Value;
			}

			foreach (OptionAttribute a in mi.GetCustomAttributes(typeof(OptionAttribute), true))
			{
				if (a.HasDefault)
				{
					_required = false;
					_default = a.DefaultValue;
				}
			}
		}

		private PropertyInfo Property { get { return (PropertyInfo)base.Member; } }

		public Type Type { get { return Property.PropertyType; } }

		public bool Required { get { return _required; } }
		public Object DefaultValue { get { return _default; } }

		public Object Value
		{
			get { return Property.GetValue(base.Target, null); }
			set
			{
				Property.SetValue(base.Target, ChangeType(value, Property.PropertyType, Required, DefaultValue), null);
			}
		}
	}
}
