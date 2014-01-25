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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Resources.Tools;
using System.Text;
using CSharpTest.Net.Generators.ResX;
using CSharpTest.Net.Html;
using CSharpTest.Net.Utils;
using Microsoft.CSharp;

namespace CSharpTest.Net.Generators.ResXtoMc
{
    class McFileGenerator
    {
        private static readonly CSharpCodeProvider Csharp = new CSharpCodeProvider();
        private readonly List<string> _resxFiles;

        private readonly Dictionary<uint, ResxGenItem> _itemsByHResult;
        private readonly Dictionary<string, List<ResxGenItem>> _itemsByName;
        private readonly Dictionary<int, string> _facilities;
        private readonly Dictionary<int, string> _categories;
        private readonly Dictionary<string, string> _eventsource;

        public McFileGenerator(IEnumerable<string> files)
        {
            _resxFiles = new List<string>();
            foreach (string file in files)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(file), ".csproj"))
                    _resxFiles.AddRange(ProjectToResXFiles(file));
                else if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(file), ".resx"))
                    _resxFiles.Add(file);
                else
                    throw new ApplicationException(String.Format("Unknown file type: {0}", file));
            }

            _itemsByHResult = new Dictionary<uint, ResxGenItem>();
            _itemsByName = new Dictionary<string, List<ResxGenItem>>(StringComparer.OrdinalIgnoreCase);
            _facilities = new Dictionary<int, string>();
            _categories = new Dictionary<int, string>();
            _eventsource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string filename in _resxFiles)
            {
                Dictionary<string, bool> thisFile = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                ResXOptions options = new ResXOptions();

                foreach (ResxGenItem item in options.ReadFile(filename))
                {
                    ResxGenItem collision;

                    item.Options["EventMessageFormat"] = options.EventMessageFormat;

                    if (item.Ignored || item.MessageId == 0)
                        continue;

                    if (_itemsByHResult.TryGetValue(item.MessageId, out collision))
                        throw new ApplicationException(String.Format("The item {0} has a duplicate id {2:x8} with {3} in file {1}.", item.Node.Name, filename, item.MessageId, collision.Node.Name));
                    _itemsByHResult.Add(item.MessageId, item);

                    if(!thisFile.ContainsKey(item.ItemName) && _itemsByName.ContainsKey(item.ItemName))
                        throw new ApplicationException(String.Format("Duplicate item name {0} found in file {1}.", item.ItemName, filename));

                    List<ResxGenItem> list;
                    if (!_itemsByName.TryGetValue(item.ItemName, out list))
                    {
                        thisFile.Add(item.ItemName, true);
                        _itemsByName.Add(item.ItemName, list = new List<ResxGenItem>());
                    }
                    list.Add(item);
                }

                if(options.FacilityId > 0)
                    AddKeyValue("Facility", filename, _facilities, options.FacilityId, options.FacilityName);
                if(options.EventCategoryId > 0)
                    AddKeyValue("Category", filename, _categories, options.EventCategoryId, options.EventCategoryName);
                if(!String.IsNullOrEmpty(options.EventSource))
                    AddKeyValue("Event Source", filename, _eventsource, options.EventSource, options.EventLog);
            }
        }

        static IEnumerable<string> ProjectToResXFiles(string file)
        {
            string dir = Path.GetDirectoryName(file);
            XmlLightDocument proj = new XmlLightDocument(File.ReadAllText(file));
            foreach (XmlLightElement xref in proj.Select("/Project/ItemGroup/EmbeddedResource"))
            {
                if (!xref.Attributes.ContainsKey("Include") || !xref.Attributes["Include"].EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
                    continue;
                string include = xref.Attributes["Include"];
                yield return Path.Combine(dir, include);
            }
        }

        public Dictionary<int, string> Facilities { get { return new Dictionary<int, string>(_facilities); } }
        public Dictionary<int, string> Categories { get { return new Dictionary<int, string>(_categories); } }
        public Dictionary<string, string> EventSources { get { return new Dictionary<string, string>(_eventsource); } }

        private void AddKeyValue<K, V>(string name, string file, Dictionary<K, V> d, K key, V value) where V : IEquatable<V>
        {
            V copy;
            if (!d.TryGetValue(key, out copy))
                d.Add(key, value);
            else if (!copy.Equals(value))
                throw new ApplicationException(String.Format("Duplicate {0} found, {1} == {2}, and {3} == {2}, defined in {4}", name, copy, key, value, file));
        }

        public void Write(TextWriter writerIn)
        {
            Dictionary<int, string> catId = new Dictionary<int, string>();
            Dictionary<int, string> facId = new Dictionary<int, string>();

            IndentedTextWriter writer = new IndentedTextWriter(writerIn);
            writer.WriteLine("MessageIdTypedef=long");
            writer.WriteLine("LanguageNames=(English=0x409:MSG00409)");//need to discover language from resx?
            writer.WriteLine();
            
            writer.WriteLine("SeverityNames=(");
            writer.Indent++;
            writer.WriteLine("Success=0x0");
            writer.WriteLine("Information=0x1");
            writer.WriteLine("Warning=0x2");
            writer.WriteLine("Error=0x3");
            writer.Indent--;
            writer.WriteLine(")");
            writer.WriteLine();

            if (_facilities.Count > 0)
            {
                List<int> keys = new List<int>(_facilities.Keys);
                keys.Sort();
                writer.WriteLine("FacilityNames=(");
                writer.Indent++;
                foreach (int key in keys)
                {
                    facId[key] = "FACILITY_" + StronglyTypedResourceBuilder.VerifyResourceName(_facilities[key], Csharp).ToUpper();
                    writer.WriteLine("{0}=0x{1:x}", facId[key], key);
                }
                writer.Indent--;
                writer.WriteLine(")");
                writer.WriteLine();
            }
            if(_categories.Count > 0)
            {
                List<int> keys = new List<int>(_categories.Keys);
                keys.Sort();
                writer.WriteLine(";// CATEGORIES");
                writer.WriteLine();
                foreach (int key in keys)
                {
                    catId[key] = "CATEGORY_" + StronglyTypedResourceBuilder.VerifyResourceName(_categories[key], Csharp).ToUpper();
                    writer.WriteLine("MessageId       = 0x{0:x}", key);
                    writer.WriteLine("SymbolicName    = {0}", catId[key]);
                    writer.WriteLine("Language        = English");
                    writer.WriteLine(_categories[key]);
                    writer.WriteLine(".");
                    writer.WriteLine();
                }
            }

            writer.WriteLine(";// MESSAGES");
            writer.WriteLine();

            foreach (KeyValuePair<uint, ResxGenItem> pair in _itemsByHResult)
            {
                ResxGenItem item = pair.Value;
                uint hr = pair.Key;
                writer.WriteLine("MessageId       = 0x{0:x}", hr & 0x0FFFF);
                writer.WriteLine("Severity        = {0}", (hr & 0x80000000) == 0 ? "Information" : (hr & 0x40000000) == 0 ? "Warning" : "Error");
                if(0 != (int)((hr >> 16) & 0x3FF))
                    writer.WriteLine("Facility        = {0}", facId[(int)((hr >> 16) & 0x3FF)]);
                writer.WriteLine("SymbolicName    = {0}", item.Identifier.ToUpper());
                writer.WriteLine("Language        = English");

                int ordinal = 1;
                string messageFormat = null;
                string messageText = String.Empty;

                if (item.Options.ContainsKey("EventMessageFormat") && !String.IsNullOrEmpty(item.Options["EventMessageFormat"]))
                {
                    FormatNumbering numbering = new FormatNumbering(ordinal);
                    messageFormat = RegexPatterns.FormatSpecifier.Replace(
                       item.Options["EventMessageFormat"].Replace("%", "%%"),
                       numbering.Transform);
                    ordinal = 1 + numbering.MaxIdentifier;
                }

                messageText = RegexPatterns.FormatSpecifier.Replace(
                    item.Value.Replace("%", "%%"), 
                    new FormatNumbering(ordinal).Transform);

                if (messageFormat != null)
                    messageText = String.Format("{0}\r\n{1}", messageText, messageFormat);

                writer.WriteLine(
                    messageText
                    .Replace("{{", "{")
                    .Replace("}}", "}")
                    .Replace("\r\n", "\n")
                    .Replace("\n", "%n\r\n")
                    .Replace("\r\n.", "\r\n%.")
                    .Replace("!", "%!")
                    );

                writer.WriteLine(".");
                writer.WriteLine();
            }
        }

        class FormatNumbering
        {
            readonly int _baseNumber;
            int _maxIdentifier;

            public FormatNumbering(int baseNumber)
            {
                _maxIdentifier = _baseNumber = baseNumber;
            }

            public int MaxIdentifier { get { return _maxIdentifier; } }

            public string Transform(System.Text.RegularExpressions.Match m)
            {
                int ordinal = int.Parse(m.Groups["field"].Value);
                _maxIdentifier = Math.Max(_maxIdentifier, ordinal + _baseNumber);
                return String.Format("%{0}", ordinal + _baseNumber);
            }
        }
    }
}
