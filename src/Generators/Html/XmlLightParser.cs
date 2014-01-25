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
using System.Text.RegularExpressions;
using System.Web;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Html
{
/*  # The following is the expanded html regex:

    \<(?:
    # match tag : 
    (?:(?<close>/)?(?<tag>
          [:_A-Za-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD]
          [:_A-Za-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD-\\.0-9\u00B7\u0300-\u036F\u203F-\u2040]*
        )
        # Attribute matching format:
        (?<attr>\s+(?<name>
          [:_A-Za-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD]
          [:_A-Za-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD-\\.0-9\u00B7\u0300-\u036F\u203F-\u2040]*
        )(?:\s*=\s*(?:(?:'(?<value>[^']*)')|(?:""(?<value>[^""]*)"")|(?:(?<value>[^\s>\/]*(?=[\s\/>])))))?)*
    (?<wsattrend>\s*)(?<closed>/)?)
    |# match special :
    (?:\!(?:
        # match comments :
        (?:--(?<comment>.*?)--)
        |# match cdata :
        (?:\[CDATA\[(?<cdata>.*?)]])
        |# other :
        (?:(?<special>[^><]*(?:<[^><]*(?:<[^>]*>[^><]*)*>[^><]*)*))
    ))
    |# match instruction :
    (?:\?(?<instruction>.*?)\?)
    )>
*/

    /// <summary>
	/// Provides a means by which you can cursur through xml/html documents and be notified for each tag/text/etc
	/// via implementing the IXmlLightReader interface.
	/// </summary>
	public static class XmlLightParser
	{
		private static readonly char[] EQ = new char[] { '=' };
		const string NameStart = ":_A-Za-z\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD";
		const string NameChar = NameStart + "-\\.0-9\u00B7\u0300-\u036F\u203F-\u2040";
		const string NameToken = "[" + NameStart + "][" + NameChar + "]*";

        const string XmlParseAttribute = @"(?<name>" + NameToken + @")\s*=\s*(?:(?:'(?<value>[^']*)')|(?:""(?<value>[^""]*)""))";
        const string HtmlParseAttribute = @"(?<name>" + NameToken + @")(?:\s*=\s*(?:(?:'(?<value>[^']*)')|(?:""(?<value>[^""]*)"")|(?:(?<value>[^\s>\/]*(?=[\s\/>])))))?";
		const string FormatParseExpression =
@"\<(?:
# match tag : 
(?:(?<close>/)?(?<tag>" + NameToken + @")
    # Attribute matching format:
    (?<attr>\s+{0})*
(?<wsattrend>\s*)(?<closed>/)?)
|# match special :
(?:\!(?:
    # match comments :
    (?:--(?<comment>.*?)--)
    |# match cdata :
    (?:\[CDATA\[(?<cdata>.*?)]])
    |# other :
    (?:(?<special>[^><]*(?:<[^><]*(?:<[^>]*>[^><]*)*>[^><]*)*))
))
|# match instruction :
(?:\?(?<instruction>.*?)\?)
)>";

        /// <summary>
        /// Provides a regular expression to match xml/html tags, comments, cdata, etc
        /// </summary>
        static readonly Regex XmlElementParsing = new Regex(
                        String.Format(FormatParseExpression, XmlParseAttribute),
                        RegexOptions.IgnorePatternWhitespace |
                        RegexOptions.IgnoreCase |
                        RegexOptions.Singleline |
                        RegexOptions.Compiled);

        /// <summary>
        /// Provides a regular expression to match xml/html tags, comments, cdata, etc
        /// </summary>
        static readonly Regex HtmlElementParsing = new Regex(
                        String.Format(FormatParseExpression, HtmlParseAttribute),
                        RegexOptions.IgnorePatternWhitespace |
                        RegexOptions.IgnoreCase |
                        RegexOptions.Singleline |
                        RegexOptions.Compiled);
        /// <summary>
		/// Provides a regular expression to match xml/html attribute name/value pairs
		/// </summary>
        static readonly Regex HtmlAttributeParsing = new Regex(@"(?<attr>\s+" + HtmlParseAttribute + ")*",
						RegexOptions.IgnorePatternWhitespace |
						RegexOptions.IgnoreCase |
						RegexOptions.Singleline |
						RegexOptions.Compiled);

        /// <summary> Determines how strict attributes are treated durring parsing </summary>
        public enum AttributeFormat 
        { 
            /// <summary> allows non-value and unquoted attributes </summary>
            Html, 
            /// <summary> requires attributes to have quoted values </summary>
            Xml
        }

        /// <summary>
        /// Parses the provided xml/html document into discrete components and provides the
        /// information to the provided reader, see XmlLightDocument
        /// </summary>
        public static void Parse(string content, IXmlLightReader reader)
        {
            Parse(content, HtmlElementParsing, reader);
        }

        /// <summary>
        /// Parses the provided xml/html document into discrete components and provides the
        /// information to the provided reader, see XmlLightDocument
        /// </summary>
        public static void Parse(string content, AttributeFormat format, IXmlLightReader reader)
        {
            Parse(content, format == AttributeFormat.Html ? HtmlElementParsing : XmlElementParsing, reader);
        }

        /// <summary>
        /// Parses the provided document into discrete components using the regex provided and 
        /// provides the information to the provided reader, see XmlLightDocument
        /// </summary>
        public static void Parse(string content, Regex parserExp, IXmlLightReader reader)
        {
        	XmlTagInfo tagInfo;

            int pos = 0;
			reader.StartDocument();

            foreach (Match element in parserExp.Matches(content))
			{
				Group tag = element.Groups["tag"];

				if (pos < element.Index)
					reader.AddText(content.Substring(pos, element.Index - pos));
				pos = element.Index + element.Length;

				if (tag.Success)
				{
					tagInfo.UnparsedTag = element.Value;
					tagInfo.FullName = tag.Value;
					tagInfo.SelfClosed = element.Groups["closed"].Success;
					tagInfo.EndingWhitespace = element.Groups["wsattrend"].Value;
					

					if (element.Groups["close"].Success)
					{
						tagInfo.Attributes = XmlLightAttribute.EmptyList;
						reader.EndTag(tagInfo);
					}
					else
					{
						tagInfo.Attributes = AttributeReader(element);
						reader.StartTag(tagInfo);
					}
				}
				else
				{
					Group comment = element.Groups["comment"];
					if (comment.Success)
						reader.AddComment(element.Value);
					else
					{
						Group cdata = element.Groups["cdata"];
						if (cdata.Success)
							reader.AddCData(element.Value);
						else
						{
							Group special = element.Groups["special"];
							if (special.Success)
								reader.AddControl(element.Value);
							else
							{
								Group instruction = element.Groups["instruction"];
								if (instruction.Success)
									reader.AddInstruction(element.Value);
							}
						}
					}
				}
			}

			if (pos < content.Length)
				reader.AddText(content.Substring(pos, content.Length - pos));

			reader.EndDocument();
		}

		private static IEnumerable<XmlLightAttribute> AttributeReader(Match element)
		{
			XmlLightAttribute attr;
			CaptureCollection attrs = element.Groups["attr"].Captures;
			CaptureCollection names = element.Groups["name"].Captures;
			CaptureCollection values = element.Groups["value"].Captures;

			string val = element.Value;

			if (element.Groups["name"].Success && attrs.Count == names.Count && names.Count == values.Count)//all attributes have values
			{
				for (int i = 0; i < names.Count; i++)
				{
					attr.Ordinal = i;
					attr.Name = names[i].Value;
					attr.Value = values[i].Value;
					attr.Before = attrs[i].Value.Substring(0, names[i].Index - attrs[i].Index);

					char chQuote = element.Value[values[i].Index - element.Index - 1];
					attr.Quote = chQuote == '"' ? XmlQuoteStyle.Double :
								 chQuote == '\'' ? XmlQuoteStyle.Single :
								 chQuote == '=' ? XmlQuoteStyle.Missing : XmlQuoteStyle.None;
					yield return attr;
				}
			}
			else //if some attributes are missing '=value', we have to reparse
			{
				for (int ix = 0; ix < attrs.Count; ix++)
				{
					attr.Ordinal = ix;

					string[] parts = attrs[ix].Value.Split(EQ, 2);
					string name = parts[0].Trim();
					string value = parts.Length == 1 ? (string)null : parts[1].Trim();
					attr.Quote = value == null ? XmlQuoteStyle.None : XmlQuoteStyle.Missing;

					if (value != null && value.Length > 0)
					{
						if (value[0] == '\'' || value[0] == '"')
						{
							attr.Quote = value[0] == '"' ? XmlQuoteStyle.Double : XmlQuoteStyle.Single;
							value = value.Substring(1, value.Length - 2);
						}
					}

					attr.Name = name;
					attr.Value = value;
					attr.Before = parts[0].Substring(0, parts[0].Length - name.Length);

					yield return attr;
				}
			}
		}

		/// <summary>
		/// Returns an enumeration of attribute name/value pairs from within an element:
		/// &lt;elem attr="value"&gt;
		/// </summary>
		public static IEnumerable<XmlLightAttribute> ParseAttributes(string tagXml)
		{
			foreach(Match m in HtmlAttributeParsing.Matches(tagXml))
				foreach(XmlLightAttribute a in AttributeReader(m))
					yield return a;
		}

		/// <summary>
		/// Returns an enumeration of attribute name/value pairs from within an element:
		/// &lt;elem attr="value"&gt;
		/// </summary>
		public static string ParseText(string content)
		{
			return HttpUtility.HtmlDecode(HtmlElementParsing.Replace(content, ParseText));
		}

		private static string ParseText(Match m)
		{
			Group cdata = m.Groups["cdata"];
			if (cdata.Success)
				return cdata.Value;
			return String.Empty;
		}
	}
}
