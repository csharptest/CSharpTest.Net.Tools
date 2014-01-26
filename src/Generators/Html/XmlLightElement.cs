#region Copyright 2010-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Web;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Html
{
    /// <summary>
    /// Represents an html element
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{OriginalTag}")]
	public class XmlLightElement : IXPathNavigable
    {
        ///<summary>Provides tag name assigned to the ROOT node of the heirarchy</summary>
        public static readonly string ROOT = "";
        ///<summary>Provides tag name assigned to the TEXT nodes in the heirarchy</summary>
        public static readonly string TEXT = "!TEXT";
        ///<summary>Provides tag name assigned to the CDATA nodes in the heirarchy</summary>
        public static readonly string CDATA = "![CDATA[";
        ///<summary>Provides tag name assigned to comment nodes in the heirarchy</summary>
        public static readonly string COMMENT = "!--";
        ///<summary>Provides tag name assigned to the TEXT nodes in the heirarchy</summary>
        public static readonly string CONTROL = "!";
        ///<summary>Provides tag name assigned to processing instruction nodes in the heirarchy</summary>
        public static readonly string PROCESSING = "?";

    	/// <summary>
		/// Creates a new xml element
		/// </summary>
		public XmlLightElement(XmlLightElement parent, string tagName)
			: this(parent, true, tagName, String.Empty)
		{ }
		internal XmlLightElement(XmlLightElement parent, bool closed, string tagName, string tagContent)
			: this(parent, closed, tagName, String.Empty, tagContent, null)
		{ }
		internal XmlLightElement(XmlLightElement parent, XmlTagInfo tag)
			: this(parent, tag.SelfClosed, tag.FullName, tag.EndingWhitespace, tag.UnparsedTag, tag.Attributes)
		{ }
		internal XmlLightElement(XmlLightElement parent, bool closed, string tagName, string closingWs, string tagContent, IEnumerable<XmlLightAttribute> attrs)
		{
            _originalTag = tagContent;
			Parent = parent;
            _tagName = tagName;
			OpeningTagWhitespace = closingWs;
			ClosingTagWhitespace = String.Empty;
			IsEmpty = closed;

			if (parent != null)
				parent.Children.Add(this);

			Attributes = new XmlLightAttributes(attrs ?? new XmlLightAttribute[0]);
        }

        private XmlLightElement _parent;
        private readonly string _tagName;
        private bool _isEmpty;
        private string _originalTag;
        private string _openingTagWhitespace;
        private string _closingTagWhitespace;

        /// <summary> Returns the tag name of this html element </summary>
        public string TagName
        {
            get { return _tagName; }
        }

        /// <summary> Whitespace appearing before the close of the start tag (&lt;div   &gt;) </summary>
        public string OpeningTagWhitespace
        {
            get { return _openingTagWhitespace ?? String.Empty; }
            set { _openingTagWhitespace = value; }
        }

        /// <summary> Whitespace appearing before the close of the end tag (&lt;/div   &gt;) </summary>
        public string ClosingTagWhitespace
        {
            get { return _closingTagWhitespace ?? String.Empty; }
            set { _closingTagWhitespace = value; }
        }

        /// <summary> 
        /// Returns the text in it's original format. Where IsSpecial == true, this is used to rewrite
        /// the content.
        /// </summary>
        public string OriginalTag
        {
            get { return _originalTag; }
            set
            {
                Check.Assert<InvalidOperationException>(IsSpecialTag && TagName != ROOT);
                _originalTag = value;
            }
        }

        /// <summary> Returns the value (if any) of this html element </summary>
        public string Value
        {
            get
            {
                if (TagName == TEXT)
                {
                    return HttpUtility.HtmlDecode(OriginalTag);
                }
                else if (TagName == CDATA)
                {
                    return OriginalTag.Substring("<![CDATA[".Length, OriginalTag.Length - "<![CDATA[]]>".Length);
                }
                else
                    return String.Empty;
            }
            set
            {
                if (TagName == TEXT)
                    _originalTag = HttpUtility.HtmlEncode(value);
                else if (TagName == CDATA)
                    _originalTag = "<![CDATA[" + value + "]]>";
                else if (TagName == COMMENT)
                    _originalTag = "<!--" + value + "-->";
                else
                {
                    throw new NotSupportedException();
                }
            }
        }

        /// <summary> Returns the parent (if any) of this html element </summary>
        public XmlLightElement Parent
        {
            get { return _parent; }
            set
            {
                Check.Assert<InvalidOperationException>(_parent == null || value == null);
                _parent = value; 
            }
        }

        /// <summary>
		/// Returns the root-level node
		/// </summary>
		public XmlLightElement Document
		{
			get
			{
				XmlLightElement e = this;
				while (e.Parent != null)
					e = e.Parent;
				return e;
			}
		}

		/// <summary>
		/// Deep-scans heirarchy for the element with the provided id
		/// </summary>
		public XmlLightElement GetElementById(string id)
		{
			foreach (XmlLightElement found in FindElement(delegate(XmlLightElement e) { return e.Attributes.ContainsKey("id") && e.Attributes["id"] == id; }))
				return found;
			return null;
		}
		/// <summary>
		/// Finds the elements matching the provided criteria
		/// </summary>
		public IEnumerable<XmlLightElement> FindElement(Predicate<XmlLightElement> match)
		{
			List<XmlLightElement> todo = new List<XmlLightElement>();
			todo.Add(this);
			for( int ix = 0; ix < todo.Count; ix++ )
			{
				XmlLightElement test = todo[ix];
				if (match(test))
					yield return test;
				todo.AddRange(test.Children);
			}
		}

        /// <summary>
        /// Returns true if the node has a textual value, i.e. text or cdata
        /// </summary>
        public bool IsText { get { return TagName == TEXT || TagName == CDATA; } }
        /// <summary>
        /// Returns true if the node is a comment
        /// </summary>
        public bool IsComment { get { return TagName == COMMENT; } }

        /// <summary>
        /// Returns true if the node is self-closing (i.e. ends with '/>')
        /// </summary>
        public bool IsEmpty
        {
            get { return _isEmpty && Children.Count == 0; }
            set { _isEmpty = value; }
        }

        /// <summary>
        /// Returns true if the node is not a normal element
        /// </summary>
        public bool IsSpecialTag
        {
            get { return TagName == ROOT || TagName == TEXT || TagName == CDATA || TagName == COMMENT || TagName == CONTROL || TagName == PROCESSING; }
        }

        /// <summary>
		/// Returns the namespace or empty string
		/// </summary>
		public string Namespace
		{
			get
			{
				int ix = TagName.IndexOf(':');
				return ix < 0 ? String.Empty : TagName.Substring(0, ix);
			}
        }
        /// <summary>
        /// Returns the namespace or null
        /// </summary>
        public string NamespaceOrNull
        {
            get
            {
                int ix = TagName.IndexOf(':');
                return ix < 0 ? null : TagName.Substring(0, ix);
            }
        }
		/// <summary>
		/// Returns the name without the namespace prefix
		/// </summary>
		public string LocalName
		{
			get
			{
				int ix = TagName.IndexOf(':');
				return ix < 0 ? TagName : TagName.Substring(ix + 1);
			}
		}

		/// <summary> Returns the children of this html element </summary>
        public readonly List<XmlLightElement> Children = new List<XmlLightElement>();

    	/// <summary> Returns the attributes of this html element </summary>
    	public readonly XmlLightAttributes Attributes;

        /// <summary> Returns the inner text of this html element </summary>
        public string InnerText { get { return NormalizeText(GetInnerText()); } }

        /// <summary> Removes this node from it's parent element </summary>
        public void Remove()
        {
            Check.Assert<InvalidOperationException>(Parent != null);
            int ix = Parent.Children.IndexOf(this);
            Check.Assert<InvalidOperationException>(ix >= 0);
            Parent.Children.RemoveAt(ix);
            Parent = null;
        }

        /// <summary>
        /// Returns the next sibling element
        /// </summary>
		public XmlLightElement NextSibling
		{
			get
			{
                if (Parent == null) return null;
				int ix = Parent.Children.IndexOf(this) + 1;
				if (ix >= 0 && ix < Parent.Children.Count)
					return Parent.Children[ix];
				return null;
			}
		}
        /// <summary>
        /// Returns the previous sibling element
        /// </summary>
        public XmlLightElement PrevSibling
		{
			get
			{
                if (Parent == null) return null;
                int ix = Parent.Children.IndexOf(this) - 1;
				if (ix >= 0 && ix < Parent.Children.Count)
					return Parent.Children[ix];
				return null;
			}
		}
        static string NormalizeText(string text)
		{
			text = text.Replace('\r', ' ');
			text = text.Replace('\n', ' ');
			while (text.IndexOf("  ") >= 0)
				text = text.Replace("  ", " ");
			return text.Trim();
		}

		private string GetInnerText()
		{
			using (StringWriter sw = new StringWriter())
			{
				WriteText(sw);
				return sw.ToString();
			}
		}
		
		private void WriteText(TextWriter wtr)
		{
			if (IsText)
				wtr.Write(Value);
			else
			{
				foreach (XmlLightElement e in Children)
					e.WriteText(wtr);
			}
		}

        /// <summary>
        /// Returns the elements from the given xpath expression
        /// </summary>
		public IEnumerable<XmlLightElement> Select(string xpath)
		{
			foreach (XmlLightNavigator nav in CreateNavigator().Select(xpath))
				yield return nav.Element;
		}
		/// <summary>
		/// Returns the first element from the given xpath expression
		/// </summary>
		public XmlLightElement SelectSingleNode(string xpath)
		{
			foreach (XmlLightElement e in Select(xpath))
				return e;
			return null;
		}

        private string FindPrefixUri(string nsPrefix)
        {
            if (String.IsNullOrEmpty(nsPrefix))
                return null;

            string attr = String.Format("xmlns:{0}", nsPrefix);
            XmlLightElement e = this;
            while (e != null)
            {
                string value;
                if (e.Attributes.TryGetValue(attr, out value))
                    return value;
                e = e.Parent;
            }

            return null;
        }

        /// <summary>
        /// Writes the text to the xml writer while preserving entities and still ensuring 
        /// the remainder of the text is properly encoded.
        /// </summary>
        protected virtual void WriteText(XmlWriter wtr, string encodedValue)
        {
            int currIx = 0;
            //we want to 
            foreach (Match match in RegexPatterns.HtmlEntity.Matches(encodedValue))
            {
                wtr.WriteString(encodedValue.Substring(currIx, match.Index - currIx));
                wtr.WriteRaw(match.Value);
                currIx = match.Index + match.Length;
            }

            wtr.WriteString(encodedValue.Substring(currIx, encodedValue.Length - currIx));
        }

        /// <summary>
		/// Writes XML to an xml writer to ensure proper formatting
		/// </summary>
		public virtual void WriteXml(XmlWriter wtr)
		{
			if (TagName == TEXT)
			{
                if (OriginalTag.Trim().Length > 0)//non-whitespace?
                    WriteText(wtr, OriginalTag);
			    return;
			}
			if (TagName == CDATA)
			{
				wtr.WriteCData(Value);
				return;
			}
			if (TagName == COMMENT)
			{
				wtr.WriteComment(OriginalTag.Substring(4, OriginalTag.Length-7));
				return;
			}
			if (IsSpecialTag)
			{
				wtr.WriteRaw(OriginalTag);
				return;
			}
            
            wtr.WriteStartElement(NamespaceOrNull, LocalName, 
                Attributes.ContainsKey("xmlns") 
                ? Attributes["xmlns"] 
                : FindPrefixUri(NamespaceOrNull));

            foreach (XmlLightAttribute kv in Attributes.ToArray())
            {
                if (kv.Name == "xmlns") { }
                else if (kv.Namespace == "xmlns")
                {
                    wtr.WriteAttributeString(kv.Namespace, kv.LocalName, null, kv.Value);
                }
                else
                {
                    wtr.WriteAttributeString(kv.NamespaceOrNull, kv.LocalName, 
                        FindPrefixUri(kv.NamespaceOrNull), 
                        HttpUtility.HtmlDecode(kv.Value));
                }
            }

		    foreach (XmlLightElement e in Children)
				e.WriteXml(wtr);

            if (IsEmpty && Children.Count == 0)
                wtr.WriteEndElement();
            else
                wtr.WriteFullEndElement();
		}

        /// <summary>
		/// Writes the re-constructed innerHTML in a well-formed Xml format
		/// </summary>
		public void WriteXml(TextWriter wtr)
		{
			using (XmlTextWriter xw = new XmlTextWriter(wtr))
			{
				xw.Indentation = 2;
				xw.IndentChar = ' ';
				xw.Formatting = System.Xml.Formatting.Indented;
				WriteXml(xw);
				xw.Flush();
			}
		}

		/// <summary>
		/// Writes the modified document in it's original formatting
		/// </summary>
		public virtual void WriteUnformatted(TextWriter wtr)
		{
			if (IsSpecialTag)
			{
				wtr.Write(OriginalTag);
				return;
			}

			wtr.Write("<{0}", TagName);

			foreach (XmlLightAttribute kv in Attributes.ToArray())
			{
				string quote = kv.Quote == XmlQuoteStyle.Double ? "\"" :
					kv.Quote == XmlQuoteStyle.Single ? "'" :
					String.Empty;
				wtr.Write(kv.Before);
				wtr.Write(kv.Name);
				if (kv.Quote != XmlQuoteStyle.None || !String.IsNullOrEmpty(kv.Value))
				{
					wtr.Write('=');
					wtr.Write(quote);
					wtr.Write(kv.Value);
					wtr.Write(quote);
				}
			}
			wtr.Write(OpeningTagWhitespace);
			if (IsEmpty && Children.Count == 0)
				wtr.Write('/');
			wtr.Write('>');

			foreach (XmlLightElement e in Children)
				e.WriteUnformatted(wtr);

			if (!IsEmpty || Children.Count > 0)
				wtr.Write("</{0}{1}>", TagName, ClosingTagWhitespace);
		}

		/// <summary>
		/// Returns the re-constructed innerHTML in a well-formed Xml format
		/// </summary>
		public string InnerXml
		{
			get
			{
				using (StringWriter sw = new StringWriter())
				{
					WriteXml(sw);
					return sw.ToString();
				}
			}
		}

		/// <summary>
		/// Returns a new System.Xml.XPath.XPathNavigator object.
		/// </summary>
		public XPathNavigator CreateNavigator()
		{
			return new XmlLightNavigator(this);
		}
	}
}
