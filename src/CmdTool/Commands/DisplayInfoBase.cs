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
	abstract class DisplayInfoBase
	{
		readonly string _name;
		readonly string[] _allNames;
		readonly Object _target;
		readonly ICustomAttributeProvider _member;
		private bool _visible;
        readonly string _category;
        object[] _attributes;
		protected string _description;
        readonly Type _reflectedType;

		public DisplayInfoBase(object target, ICustomAttributeProvider mi)
		{
			_target = target;
			_member = mi;

			if (mi is MethodInfo)
				_name = ((MethodInfo)mi).Name;
			else if (mi is ParameterInfo)
				_name = ((ParameterInfo)mi).Name;
			else if( mi is PropertyInfo)
				_name = ((PropertyInfo)mi).Name;
            
			InterpreterException.Assert(_name != null, "Unknown type " + mi.ToString());

            _reflectedType = target == null ? null : target is Type ? (Type)target : target.GetType();
			_description = _member.ToString();
			_category = _target.GetType().Name;
			_visible = true;

			foreach (DisplayNameAttribute a in _member.GetCustomAttributes(typeof(DisplayNameAttribute), true))
				_name = a.DisplayName;

			foreach (DescriptionAttribute a in _member.GetCustomAttributes(typeof(DescriptionAttribute), true))
				_description = String.Format("{0}", a.Description);

			foreach (CategoryAttribute a in _member.GetCustomAttributes(typeof(CategoryAttribute), true))
				_category = String.Format("{0}", a.Category);

			foreach (BrowsableAttribute a in _member.GetCustomAttributes(typeof(BrowsableAttribute), true))
				_visible = a.Browsable;

			List<string> names = new List<string>();

			foreach (DisplayInfoAttribute a in mi.GetCustomAttributes(typeof(DisplayInfoAttribute), true))
			{
				if (!String.IsNullOrEmpty(a.DisplayName))
					_name = a.DisplayName;
				names.AddRange(a.AliasNames);
				if (!String.IsNullOrEmpty(a.Description))
					_description = a.Description;
				if (!String.IsNullOrEmpty(a.Category))
					_category = a.Category;
				_visible &= a.Visible;
			}

			names.Insert(0, _name);
			foreach (AliasNameAttribute a in _member.GetCustomAttributes(typeof(AliasNameAttribute), true))
				names.Add(a.Name);
			_allNames = names.ToArray();

            try { _attributes = mi.GetCustomAttributes(true); }
            catch { _attributes = new object[0]; }
		}

		protected Object Target { get { return _target; } }
		protected ICustomAttributeProvider Member { get { return _member; } }

        public virtual Type ReflectedType { get { return _reflectedType; } }
        public virtual bool Visible { get { return _visible; } set { _visible = value; } }
		public virtual string DisplayName { get { return _name; } }
		public virtual string[] AllNames { get { return (string[])_allNames.Clone(); } }
		public virtual string Category { get { return _category; } }
		public virtual string Description { get { return _description; } }

	    public void AddAttribute<T>(T attribute) where T : Attribute
        {
            for (int i=0; i < _attributes.Length; i++)
            {
                if (_attributes[i] is T)
                {
                    _attributes[i] = attribute;
                    return;
                }
            }
            List<object> all = new List<object>(_attributes);
            all.Add(attribute);
	        _attributes = all.ToArray();
        }

	    public bool TryGetAttribute<T>(out T found) where T : Attribute
        {
            foreach (object attr in _attributes)
            {
                if (attr is T)
                {
                    found = (T)attr;
                    return true;
                }
            }
            found = null;
            return false;
        }

		/// <summary> Provides the standard type cohersion between types </summary>
		protected Object ChangeType(Object value, Type type, bool required, Object defaultValue)
		{
			if (value == null)
			{
				InterpreterException.Assert(required == false, "The value for {0} is required.", this.DisplayName);
				value = defaultValue;
			}
			InterpreterException.Assert(value != null || !type.IsValueType, "Can not set value of type {0} to null in {1}.", type, this.DisplayName);

		    if (value != null)
		    {
		        try
		        {
		            if (type.IsEnum && value is string)
		                value = Enum.Parse(type, value as string, true);
		            else if (!type.IsAssignableFrom(value.GetType()))
		                value = Convert.ChangeType(value, type);
		        }
		        catch (FormatException f)
		        {
		            throw new InterpreterException(f.Message, f);
		        }
		    }
		    return value;
		}
	}

}
