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

namespace CSharpTest.Net.Html
{
	/// <summary> The quote used with an attribute value </summary>
	public enum XmlQuoteStyle : byte
	{
		/// <summary> The value was not defined, no '=' sign </summary>
		None,
		/// <summary> The value was not quoted, name=value </summary>
		Missing,
		/// <summary> The value was not quoted, name='value' </summary>
		Single,
		/// <summary> The value was not quoted, name="value" </summary>
		Double,
	}

	/// <summary>
	/// Represents a single attribute on an xml element
	/// </summary>
	public struct XmlLightAttribute
	{
		private const string Space = " ";
		/// <summary> A static empty list of attributes </summary>
		internal static readonly XmlLightAttribute[] EmptyList = new XmlLightAttribute[0];

		/// <summary> XmlLightAttribute </summary>
		public XmlLightAttribute(string name)
		{
			Quote = XmlQuoteStyle.Double;
			Name = name;
			Value = null;
			Before = Space;
			Ordinal = 0;
		}

		/// <summary> The offset of the attribute in the list </summary>
		public int Ordinal;
		/// <summary> The full name of the attribute </summary>
		public string Name;
		/// <summary> The original encoded text value of the attribute </summary>
		public string Value;
		/// <summary> The character used to quote the original value </summary>
		public XmlQuoteStyle Quote;
		/// <summary> The white-space characters preceeding the attribute name </summary>
		public string Before;

        /// <summary>
        /// Returns the namespace or empty string
        /// </summary>
        public string Namespace
        {
            get
            {
                int ix = Name.IndexOf(':');
                return ix < 0 ? String.Empty : Name.Substring(0, ix);
            }
        }
        /// <summary>
        /// Returns the namespace or null
        /// </summary>
        public string NamespaceOrNull
        {
            get
            {
                int ix = Name.IndexOf(':');
                return ix < 0 ? null : Name.Substring(0, ix);
            }
        }
        /// <summary>
        /// Returns the name without the namespace prefix
        /// </summary>
        public string LocalName
        {
            get
            {
                int ix = Name.IndexOf(':');
                return ix < 0 ? Name : Name.Substring(ix + 1);
            }
        }
	}

	/// <summary>
	/// Wraps up the information about a tag start while parsing
	/// </summary>
	public struct XmlTagInfo
	{
		/// <summary> XmlTagInfo </summary>
		public XmlTagInfo(string name, bool closed)
		{
			FullName = name;
			SelfClosed = closed;
			EndingWhitespace = String.Empty;
			UnparsedTag = null;
			Attributes = XmlLightAttribute.EmptyList;
		}
		/// <summary>The full name token of the element 'ns:name'</summary>
		public string FullName;
		/// <summary> True if the tag is self-closing/empty: &lt;empty/&gt; </summary>
		public bool SelfClosed;
		/// <summary> THe space preceeding the tag close '>'</summary>
		public string EndingWhitespace;
		/// <summary> The complete tag in raw/unparsed form </summary>
		public string UnparsedTag;
		/// <summary> The name/value pair attributes </summary>
		public IEnumerable<XmlLightAttribute> Attributes;
	}

	/// <summary>
	/// Provides a means by which the XmlLightParser can inform you of the document
	/// elements encountered.
	/// </summary>
	public interface IXmlLightReader
	{
		/// <summary> Begins the processing of an xml input </summary>
		void StartDocument();

		/// <summary> Begins the processing of an xml tag </summary>
		void StartTag(XmlTagInfo tag);

		/// <summary> Ends the processing of an xml tag </summary>
		void EndTag(XmlTagInfo tag);

		/// <summary> Encountered text or whitespace in the document </summary>
		void AddText(string content);

		/// <summary> Encountered comment in the document </summary>
		void AddComment(string comment);

		/// <summary> Encountered cdata section in the document </summary>
		void AddCData(string cdata);

		/// <summary> Encountered control information &lt;! ... &gt; in the document </summary>
		void AddControl(string cdata);

		/// <summary> Encountered processing instruction &lt;? ... ?&gt; in the document </summary>
		void AddInstruction(string instruction);

		/// <summary> Ends the processing of an xml input </summary>
		void EndDocument();
	}
}
