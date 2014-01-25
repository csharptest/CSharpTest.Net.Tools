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
using System.Xml;
using System.Xml.XPath;

namespace CSharpTest.Net.Html
{
	class XmlLightNavigator : XPathNavigator
	{
		XmlNameTable _names;
		XmlLightElement _current;
		List<string> _attrNames;
		int _attribute;

		public XmlLightNavigator(XmlLightElement current)
			: this(new NameTable(), current, null, -1) { }
		private XmlLightNavigator(XmlNameTable names, XmlLightElement current, List<string> attrNames, int attribute)
		{
			_names = names;
			_current = current;
			_attrNames = attrNames;
			_attribute = attribute;
		}

		public XmlLightElement Element { get { return _current; } }

		public override string BaseURI
		{
			get { return "uri://xpath.navigator"; }
		}

		public override string NamespaceURI
		{
			get { return String.Empty; }
		}

		public override XPathNavigator Clone()
		{
			return new XmlLightNavigator(_names, _current, _attrNames, _attribute);
		}

		public override bool IsEmptyElement
		{
			get { return _current.IsEmpty; }
		}

		public override bool IsSamePosition(XPathNavigator other)
		{
			return Object.ReferenceEquals(_current, ((XmlLightNavigator)other)._current);
		}

		public override string Prefix
		{
			get
			{
				int ix = Name.IndexOf(':');
				return ix < 0 ? String.Empty : _names.Add(Name.Substring(0, ix));
			}
		}

		public override string LocalName
		{
			get
			{
				int ix = Name.IndexOf(':');
				return ix < 0 ? Name : _names.Add(Name.Substring(ix + 1));
			}
		}

		public override string Name
		{
			get 
			{
				if(_attrNames != null && _attribute >= 0 && _attribute < _attrNames.Count)
					return _names.Add(_attrNames[_attribute]); 
				return _names.Add(_current.TagName); 
			}
		}

		public override string Value
		{
			get
			{
				if (_attrNames != null && _attribute >= 0 && _attribute < _attrNames.Count)
					return _current.Attributes[_attrNames[_attribute]]; 
				return _current.Value;
			}
		}

		public override XmlNameTable NameTable
		{
			get { return _names; }
		}

		public override XPathNodeType NodeType
		{
			get 
			{
				if (_attribute != -1)
					return XPathNodeType.Attribute;
				if (_current.IsText)
				{
					if (String.IsNullOrEmpty(_current.Value) || _current.Value.Trim().Length == 0)
						return XPathNodeType.Whitespace;
					return XPathNodeType.Text;
				}
				if (_current.TagName == XmlLightElement.ROOT)
					return XPathNodeType.Root;
				if (_current.TagName == XmlLightElement.COMMENT)
					return XPathNodeType.Comment;
				if (_current.IsSpecialTag)
					return XPathNodeType.Whitespace;
				return XPathNodeType.Element;
			}
		}

		public override bool MoveTo(XPathNavigator other)
		{
			_current = ((XmlLightNavigator)other)._current;
			_attrNames = ((XmlLightNavigator)other)._attrNames;
			_attribute = ((XmlLightNavigator)other)._attribute;
			return true;
		}

		public override bool MoveToFirstNamespace(XPathNamespaceScope namespaceScope)
		{
			return false;
		}

		public override bool MoveToNextNamespace(XPathNamespaceScope namespaceScope)
		{
			return false;
		}

		public override bool MoveToId(string id)
		{
			XmlLightElement item = _current.Document.GetElementById(id);
			if (item != null)
			{
				_current = item;
				_attribute = -1;
				return true;
			}
			return false;
		}

		public override bool MoveToFirstChild()
		{
			if (_current.Children.Count > 0)
			{
				_current = _current.Children[0];
				_attribute = -1;
				return true;
			}
			return false;
		}

		public override bool MoveToParent()
		{
			if (_attribute != -1)
			{
				_attribute = -1;
				_attrNames = null;
				return true;
			}
			XmlLightElement e = _current.Parent;
			if (e != null)
			{
				_current = e;
				_attribute = -1;
				return true;
			}
			return false;
		}

		public override bool MoveToPrevious()
		{
			XmlLightElement e = _current.PrevSibling;
			if (e != null)
			{
				_current = e;
				_attribute = -1;
				return true;
			}
			return false;
		}

		public override bool MoveToNext()
		{
			XmlLightElement e = _current.NextSibling;
			if (e != null)
			{
				_current = e;
				_attribute = -1;
				return true;
			}
			return false;
		}

		public override bool MoveToFirstAttribute()
		{
			_attrNames = new List<string>(_current.Attributes.Keys);
			if (_attrNames.Count > 0)
			{
				_attribute = 0;
				return true;
			}
			return false;
		}

		public override bool MoveToNextAttribute()
		{
			if (_attribute >= 0 && _attrNames != null && (_attribute + 1) < _attrNames.Count)
			{
				_attribute++;
				return true;
			}
			return false;
		}
	}
}
