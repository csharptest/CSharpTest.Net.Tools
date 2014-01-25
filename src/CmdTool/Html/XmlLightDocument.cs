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
using System.IO;
using System.Xml;

namespace CSharpTest.Net.Html
{
    /// <summary>
    /// Inteded to quickly read 'mostly' well-formed html text
    /// </summary>
	public class XmlLightDocument : XmlLightElement, IXmlLightReader
	{
		/// <summary>
		/// Stores the current node stack while parsing documents
		/// </summary>
		protected Stack<XmlLightElement> _parserStack;

        /// <summary>
        /// Returns the root element
        /// </summary>
        public XmlLightElement Root;

        /// <summary>
        /// Parses the document provided
        /// </summary>
		public XmlLightDocument()
            : base(null, false, ROOT, String.Empty)
		{
			_parserStack = new Stack<XmlLightElement>();
		}

        /// <summary>
        /// Parses the document provided
        /// </summary>
		public XmlLightDocument(string content)
            : this()
		{
			XmlLightParser.Parse(content, XmlLightParser.AttributeFormat.Xml, this);
		}

		/// <summary>
		/// Writes the re-constructed innerXML
		/// </summary>
		public override void WriteXml(XmlWriter wtr)
        {
            foreach (XmlLightElement e in Children)
                e.WriteXml(wtr);
        }

		/// <summary>
		/// Writes the re-constructed document while attempting to preserve formatting
		/// </summary>
		public override void WriteUnformatted(TextWriter wtr)
		{
			foreach (XmlLightElement e in Children)
				e.WriteUnformatted(wtr);
		}

		/// <summary> Begins the processing of an xml input </summary>
		public virtual void StartDocument()
		{
			Check.Assert<InvalidOperationException>(Children.Count == 0);
			_parserStack.Push(this);
		}

		/// <summary> Begins the processing of an xml tag </summary>
		public virtual void StartTag(XmlTagInfo tag)
		{
			XmlLightElement parent = _parserStack.Peek();
			XmlLightElement e = new XmlLightElement(parent, tag);

			if (Root == null && _parserStack.Count == 1)
				Root = e;
			if (tag.SelfClosed == false)
				_parserStack.Push(e);
		}

		/// <summary> Ends the processing of an xml tag </summary>
		public virtual void EndTag(XmlTagInfo tag)
		{
			XmlLightElement e = _parserStack.Pop();
			if (e.TagName != tag.FullName)
				throw new XmlException(String.Format("Incorrect tag closed '</{0}>', expected '</{1}>'", tag.FullName, e.TagName));
			e.ClosingTagWhitespace = tag.EndingWhitespace;
		}

		/// <summary> Encountered text or whitespace in the document </summary>
		public virtual void AddText(string content)
		{
			XmlLightElement parent = _parserStack.Peek();
			new XmlLightElement(parent, true, XmlLightElement.TEXT, content);
		}

		/// <summary> Encountered comment in the document </summary>
		public virtual void AddComment(string comment)
		{
			XmlLightElement parent = _parserStack.Peek();
			new XmlLightElement(parent, true, XmlLightElement.COMMENT, comment);
		}

		/// <summary> Encountered cdata section in the document </summary>
		public virtual void AddCData(string cdata)
		{
			XmlLightElement parent = _parserStack.Peek();
			new XmlLightElement(parent, true, XmlLightElement.CDATA, cdata);
		}

		/// <summary> Encountered control information &lt;! ... &gt; in the document </summary>
		public virtual void AddControl(string ctrl)
		{
			XmlLightElement parent = _parserStack.Peek();
			new XmlLightElement(parent, true, XmlLightElement.CONTROL, ctrl);
		}

		/// <summary> Encountered processing instruction &lt;? ... ?&gt; in the document </summary>
		public virtual void AddInstruction(string instruction)
		{
			XmlLightElement parent = _parserStack.Peek();
			new XmlLightElement(parent, true, XmlLightElement.PROCESSING, instruction);
		}

		/// <summary> Ends the processing of an xml input </summary>
        public virtual void EndDocument()
        {
            XmlLightElement e = _parserStack.Pop();
            if (e.TagName != this.TagName)
                throw new XmlException(String.Format("Tag was not closed, expected '</{0}>'", e.TagName));
            else if (this.Root == null)
                throw new XmlException(String.Format("Root element not found."));
        }
	}
}
