#region Copyright 2009-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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

namespace CSharpTest.Net.Utils
{
	/// <summary>
	/// A collection of common regular expression patterns
	/// </summary>
	public static class RegexPatterns
	{
		/// <summary>
		/// Matches a numeric version "1.2.3.4" up to 9 digits for a value
		/// </summary>
		public static readonly Regex FullVersion = new Regex(@"^[1-2]?[0-9]{1,9}\.[1-2]?[0-9]{1,9}\.[1-2]?[0-9]{1,9}\.[1-2]?[0-9]{1,9}$");

		/// <summary>
		/// Matches a numeric version with 2, 3, or 4 parts: "1.2", "1.2.3", or "1.2.3.4" up to 9 digits for a value
		/// </summary>
		public static readonly Regex Version = new Regex(@"^[1-2]?[0-9]{1,9}\.[1-2]?[0-9]{1,9}(\.[1-2]?[0-9]{1,9}(\.[1-2]?[0-9]{1,9})?)?$");

		/// <summary>
        /// Free-form matching of urls in plain text, from http://immike.net/blog/2007/04/06/5-regular-expressions-every-web-programmer-should-know/
		/// </summary>
		public static readonly Regex HttpUrl = new Regex(@"https?://[-\w]+(\.\w[-\w]*)+(:\d+)?(/[^.!,?;""\'<>()\[\]\{\}\s\x7F-\xFF]*([.!,?]+[^.!,?;""\'<>\(\)\[\]\{\}\s\x7F-\xFF]+)*)?");

        /// <summary>
        /// Finds html/xml entity references in text, test patterns: hex = #xae6f278 decimal = #1234567890 or named = lt
        /// </summary>
        public static readonly Regex HtmlEntity = new Regex(@"&(?<entity>#(?<number>x(?<hex>[\da-f]{1,8})|(?<decimal>\d{1,10}))|(?<name>[\w-[\d]]\w{1,10}));", RegexOptions.IgnoreCase);

        /// <summary>
        /// Matches a makefile macro name in text, i.e. "$(field:name=value)" where field is any alpha-numeric + ('_', '-', or '.') text identifier 
        /// returned from group "field".  the "replace" group contains all after the identifier and before the last ')'.  "name" and "value" groups
        /// match the name/value replacement pairs.
        /// </summary>
        public static readonly Regex MakefileMacro = new Regex(@"\$\((?<field>[\w-_\.]*)(?<replace>(?:\:(?<name>[^:=\)]+)=(?<value>[^:\)]*))+)?\)");

        /// <summary>
        /// Matches a c-sharp style format specifier in a string "{0,5:n}". The identifier may be any numeric set of characters.  The groups 
        /// returned will be "field", "suffix", "width", and "format".  Used with StringUtils.Transform() you can provide your own String.Format().
        /// </summary>
        public static readonly Regex FormatSpecifier = new Regex(@"(?<!{){(?<field>\d+)(?<suffix>(?:,(?<width>-?\d+))?(?:\:(?<format>[^}{]+))?)}");

        /// <summary>
        /// Matches a c-sharp style format specifier in a string "{Name-0,5:n}" with some additional changes. Used with StringUtils.Transform() you 
        /// can provide your own String.Format().  The groups returned will be the following:
        /// "field" - An identifier may contain any alpha-numeric or one of these special characters: ('_', '-', or '.')
        /// "suffix" - Everything after the identifer and before the closing brace '}'
        /// "width" - The width part of the format is a number after a ',' and before ':'
        /// "format" - Everything after the the ':' and before the closing '}', note: escapes }} are not supported.
        /// </summary>
        public static readonly Regex FormatNameSpecifier = new Regex(@"(?<!{){(?<field>[\w-_\.]+)(?<suffix>(?:,(?<width>-?\d+))?(?:\:(?<format>[^}{]+))?)}");

		/// <summary>
		/// Matches VisualStudio style error/warning format.  The groups returned are as follows:
		/// path = The file path (due caution should be taken to ensure this is a file path)
		/// line = The line number if any
		/// pos = The line position if any
		/// error = Was it tagged as an error?
		/// warning = Was it tagged as a warning?
		/// id = The error/warning id if provided
		/// message = The remainder of the text line
		/// </summary>
		public static Regex VSErrorMessage = new Regex(@"(?imx-:^(?<path>(?:[a-z]\:)?(?:[\\/][^\:\\/]*?)*)(?:\((?<line>\d{1,10})(?:,(?<pos>\d{1,10}))?\))?:(?:\s*(?:(?<error>error)|(?<warning>warning))\s*(?<id>[^:]*):)?\s*(?<message>.*?)\s*$)");

        /// <summary>
		/// Matches a guid in the common forms used with the string constructor
		/// of the System.Guid type:
		///  "ca761232ed4211cebacd00aa0057b223" 
		///  "ca761232-ed42-11ce-bacd-00aa0057b223" 
		///  "CA761232-ED42-11CE-BACD-00AA0057B223" 
		/// "{ca761232-ed42-11ce-bacd-00aa0057b223}" 
		/// "(CA761232-ED42-11CE-BACD-00AA0057B223)" 
		/// The following format is NOT support:
		/// "{0xCA761232, 0xED42, 0x11CE, {0xBA, 0xCD, 0x00, 0xAA, 0x00, 0x57, 0xB2, 0x23}}" 
		/// </summary>
		public static readonly Regex Guid = new Regex(@"^\{?[\da-fA-F]{8}\-?[\da-fA-F]{4}\-?[\da-fA-F]{4}\-?[\da-fA-F]{4}\-?[\da-fA-F]{12}\}?$");

		/// <summary>
		/// This is generally not enought to fully validate a card, there are other
		/// ways to validate by using the build-in checksums.
		/// </summary>
		public static readonly Regex CreditCard = new Regex(@"^(?:(?<Mastercard>5[1-5]\d{14})|(?<Visa>4(?:\d{15}|\d{12}))|(?<Amex>3[47]\d{13})|(?<DinersClub>3(?:0[0-5]|6[0-9]|8[0-9])\d{11})|(?<Discover>6011\d{12}))$");
    }
}