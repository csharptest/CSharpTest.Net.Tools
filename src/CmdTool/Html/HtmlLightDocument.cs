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
using System.Text;
using CSharpTest.Net.Collections;

namespace CSharpTest.Net.Html
{
	using TagPair = System.Collections.Generic.KeyValuePair<string, string[]>;

	/// <summary>
	/// Represents a loosly parsed html document
	/// </summary>
	public class HtmlLightDocument : XmlLightDocument
	{
		/// <summary>
		/// According to the Xhtml DTD these tags do not cotain anything
		/// </summary>
		SetList<string> _nonClosedTags = new SetList<string>(new string[]
        {
            "br",
            "base",
            "meta",
            "link",
            "hr",
            "basefont",
            "param",
            "img",
            "area",
            "input",
            "col",
        },
		StringComparer.OrdinalIgnoreCase);
		/// <summary>
		/// These tags automatically close a containing tag of the same type, 
		/// i.e. &lt;p>&lt;p>&lt;/p> is the same as &lt;p>&lt;/p>&lt;p>&lt;/p>
		/// </summary>
		SetList<string> _nonNestingTags = new SetList<string>(new string[]
        {
            "p",
            "tr",
            "td",
            "li",
        },
		StringComparer.OrdinalIgnoreCase);

		class TagLookup : Dictionary<string, List<string>>
		{
			public TagLookup (IEnumerable<KeyValuePair<string, string[]>> values)
				: base(StringComparer.OrdinalIgnoreCase)
			{
				foreach (KeyValuePair<string, string[]> kv in values)
					base.Add(kv.Key, new List<string>(kv.Value));
			}
		}
		/// <summary>
		/// Strict-Heirarchy elements are elements that have a required parent type(s)
		/// </summary>
		TagLookup _htmlHeirarchy = new TagLookup(
			new TagPair[] {
				new TagPair("head", new string[] { "html" }),
				new TagPair("body", new string[] { "html" }),
				new TagPair("li", new string[] { "ol", "ul" }),
				new TagPair("dt", new string[] { "dl" }),
				new TagPair("dd", new string[] { "dl" }),
				new TagPair("caption", new string[] { "table" }),
				new TagPair("colgroup", new string[] { "table" }),
				new TagPair("col", new string[] { "colgroup", "table" }),
				new TagPair("thead", new string[] { "table" }),
				new TagPair("tfoot", new string[] { "table" }),
				new TagPair("tbody", new string[] { "table" }),
				new TagPair("tr", new string[] { "table", "tbody", "tfoot", "thead" }),
				new TagPair("th", new string[] { "tr" }),
				new TagPair("td", new string[] { "tr" }),
			}
		);

		/// <summary>
		/// Represents a loosly parsed html document
		/// </summary>
		public HtmlLightDocument() : base() { }
		/// <summary>
		/// Represents a loosly parsed html document
		/// </summary>
		public HtmlLightDocument(string content) : base() 
        {
            XmlLightParser.Parse(content, XmlLightParser.AttributeFormat.Html, this);
        }

		/// <summary> </summary>
		public override void StartTag(XmlTagInfo tag)
		{
			if (_nonClosedTags.Contains(tag.FullName))
				tag.SelfClosed = true;

			XmlLightElement parent = _parserStack.Peek();
			List<string> allowedParents;

			if (_nonNestingTags.Contains(tag.FullName) && StringComparer.OrdinalIgnoreCase.Equals(parent.TagName, tag.FullName))
				_parserStack.Pop();
			else if (_htmlHeirarchy.TryGetValue(tag.FullName, out allowedParents))
			{
				int depth = 0;
				XmlLightElement[] stack = _parserStack.ToArray();
				while (depth < stack.Length && allowedParents.BinarySearch(stack[depth].TagName, StringComparer.OrdinalIgnoreCase) < 0)
					depth++;

				if (depth < stack.Length)
					for (; depth > 0; depth--)
						_parserStack.Pop();
				else
					StartTag(new XmlTagInfo(allowedParents[0], false));
			}

			base.StartTag(tag);
		}

		/// <summary> </summary>
		public override void EndTag(XmlTagInfo tag)
		{
			if (_nonClosedTags.Contains(tag.FullName))
				return;

			XmlLightElement closed = null;
			try
			{
				XmlLightElement[] stack = _parserStack.ToArray();
				if (stack[0].TagName == tag.FullName)
				{
					closed = _parserStack.Pop();
					return;
				}

				//closes any tags left open in these elements
				bool found = false;
				for (int i = 0; !found && i < stack.Length; i++)
					found = found || StringComparer.OrdinalIgnoreCase.Equals(stack[i].TagName, tag.FullName);

				while (found &&
				       StringComparer.OrdinalIgnoreCase.Equals((closed = _parserStack.Pop()).TagName, tag.FullName) == false)
				{
				}
			}
			finally
			{
				if(closed != null && StringComparer.OrdinalIgnoreCase.Equals(closed.TagName, tag.FullName))
					closed.ClosingTagWhitespace = tag.EndingWhitespace;
			}
		}

		/// <summary> Ends the processing of an xml input </summary>
		public override void EndDocument()
		{
			_parserStack.Clear();
			if (Root == null || !StringComparer.OrdinalIgnoreCase.Equals("html", Root.TagName))
				throw new ApplicationException("Invalid HTML document.");
		}
	}
}
