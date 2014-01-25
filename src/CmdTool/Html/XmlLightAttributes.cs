#region Copyright 2010-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Web;

namespace CSharpTest.Net.Html
{
	/// <summary>
	/// A collection of attributes for an element
	/// </summary>
	public class XmlLightAttributes : IEnumerable<KeyValuePair<string, string>>
	{
		private readonly Dictionary<string, XmlLightAttribute> _attributes;
		
		internal XmlLightAttributes (IEnumerable<XmlLightAttribute> list)
		{
			_attributes = new Dictionary<string, XmlLightAttribute>(StringComparer.OrdinalIgnoreCase);
			int index = 0;
			foreach (XmlLightAttribute attribute in list)
			{
				XmlLightAttribute a = attribute;
				a.Ordinal = index ++;
				_attributes.Add(a.Name, a);
			}
		}
		/// <summary>
		/// Returns the number of items in the collection.
		/// </summary>
		public int Count { get { return _attributes.Count; } }
		/// <summary>
		/// Gets or Sets the attribute's unencoded text value
		/// </summary>
		public string this[string name]
		{
			get
			{
				return HttpUtility.HtmlDecode(_attributes[name].Value);
			}
			set
			{
				XmlLightAttribute a;
				if (!_attributes.TryGetValue(name, out a))
				{
					a = new XmlLightAttribute(name);
					a.Ordinal = _attributes.Count;
				}
				else if (a.Quote == XmlQuoteStyle.None || a.Quote == XmlQuoteStyle.Missing)
					a.Quote = XmlQuoteStyle.Double;
				a.Value = HttpUtility.HtmlAttributeEncode(value);
				_attributes[name] = a;
			}
		}

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="name">The name of the attribute to find</param>
        /// <param name="value">Set to the value of the attribute</param>
        /// <returns>Returns true if the attribute was defined</returns>
        public bool TryGetValue(string name, out string value)
        {
            XmlLightAttribute attr;
            if (_attributes.TryGetValue(name, out attr))
            {
                value = HttpUtility.HtmlDecode(attr.Value);
                return true;
            }
            value = null;
            return false;
        }

		/// <summary> Returns true if hte attribute is defined </summary>
		public bool ContainsKey(string name)
		{ return _attributes.ContainsKey(name); }

		/// <summary>
		/// Returns the names of the attributes in appearance order
		/// </summary>
		public IEnumerable<string> Keys
		{ get { foreach (XmlLightAttribute a in ByOrdinal) yield return a.Name; } }

		/// <summary>
		/// Adds a new attribute to the collection
		/// </summary>
        public void Add(string name, string value)
        {
        	Check.Assert<ArgumentOutOfRangeException>(_attributes.ContainsKey(name) == false);
        	this[name] = value;
        }

		/// <summary>
		/// Removes an item from the collection
		/// </summary>
		public bool Remove(string name)
		{
			if(_attributes.Remove(name))
			{
				int index = 0;
				foreach (XmlLightAttribute attr in ByOrdinal)
				{
					XmlLightAttribute a = attr;
					a.Ordinal = index++;
					_attributes[a.Name] = a;
				}
				return true;
			}
			return false;
		}

		private List<XmlLightAttribute> ByOrdinal
		{
			get
			{
				List<XmlLightAttribute> all = new List<XmlLightAttribute>(_attributes.Values);
				all.Sort(delegate(XmlLightAttribute a1, XmlLightAttribute a2) { return a1.Ordinal.CompareTo(a2.Ordinal); });
				return all;
			}
		}

		/// <summary>
		/// Returns the attributes as a collection
		/// </summary>
		/// <returns></returns>
		public XmlLightAttribute[] ToArray()
		{
			return ByOrdinal.ToArray();
		}

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			foreach (XmlLightAttribute a in ByOrdinal)
				yield return new KeyValuePair<string, string>(a.Name, HttpUtility.HtmlDecode(a.Value));
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }
	}
}
