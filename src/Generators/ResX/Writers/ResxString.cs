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
using System.Diagnostics;
using System.Text.RegularExpressions;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Generators.ResX.Writers
{
    class ResxString
    {
        private readonly ResxGenItem _item;

        public ResxString(ResxGenItem item)
        {
            _item = Check.NotNull(item);
        }

        protected ResxGenItem Item { get { return _item; } }
        public string MemberName { get { return Item.MemberName; } }

        public virtual bool Test(Action<string> error)
        {
            try { String.Format(Item.Value, new object[Item.Args.Count]); return true; }
            catch (Exception err)
            { error(String.Format("{0} - {1}", Item.ItemName, err.Message)); return false; }
        }

        protected virtual string FormatterMethod(string className, string formatPrefix) { return "String.Format"; }
        protected virtual string CultureMember { get { return "resourceCulture"; } }
        public virtual string SummaryString
        {
            get
            {
                string summary = Item.Value;
                if (summary.Length > 255)
                {
                    int stop = summary.LastIndexOf(' ', 255);
                    summary = summary.Substring(0, stop < 0 ? 255 : stop);
                }
                return summary;
            }
        }

        public virtual void WriteAccessor(CsWriter code, string fullClassName, string formatPrefix)
        {
            code.WriteSummaryXml(SummaryString);
            using (code.WriteBlock("public static string {0}({1})", Item.MemberName, Item.Parameters(true)))
            {
                if(Item.AutoLog)
                    WriteEvent(code, fullClassName, formatPrefix);
                string args = Item.Parameters(false);
                if( args.Length > 0 )
                    args = ", " + args;

                code.WriteLine("return {0}({1}, {2}.{3}{4}{5});",
                    FormatterMethod(fullClassName, formatPrefix), CultureMember, fullClassName, formatPrefix, Item.Identifier, args);
            }
        }

        public virtual void WriteFormatString(CsWriter code)
        {
            code.WriteSummaryXml(SummaryString);
            code.WriteLine(
                "public static string {0} {{ get {{ return ResourceManager.GetString({1}, {2}); }} }}",
                Item.Identifier, code.MakeString(Item.FullName), CultureMember);
        }

        public virtual void WriteEvent(CsWriter code, string fullClassName, string formatPrefix)
        {
            Dictionary<int, string> formatting = new Dictionary<int, string>();
            for (int i = 0; i < Item.Args.Count; i++)
                formatting[i] = "{0}";
            foreach (Match m in RegexPatterns.FormatSpecifier.Matches(Item.Value))
            {
                Group field = m.Groups["field"];
                int ix;
                if (int.TryParse(field.Value, out ix))
                {
                    formatting[ix] = String.Format("{0}0{1}",
                        m.Value.Substring(0, field.Index - m.Index),
                        m.Value.Substring(field.Index + field.Length - m.Index));
                }
            }

            code.WriteLine("string[] ctorEventArguments = new string[] {");
            for (int i = 0; i < Item.Args.Count; i++)
                code.WriteLine("{0}(@\"{1}\", {2}),", FormatterMethod(fullClassName, formatPrefix), formatting[i], Item.Args[i].ParamName);
            code.WriteLine("};");

            EventLogEntryType etype = Item.MessageId < 0x80000000
                                          ? EventLogEntryType.Information
                                          : ((Item.MessageId & 0xC0000000) == 0xC0000000)
                                                ? EventLogEntryType.Error
                                                : EventLogEntryType.Warning;

            string logMethod = String.Format("{0}.WriteEvent", fullClassName);
            code.WriteLine("{0}(", logMethod);
            code.Indent++;
            code.WriteLine("{0}.EventLogName,", fullClassName);
            code.WriteLine("{0}.EventSourceName,", fullClassName);
            code.WriteLine("{0}.EventCategoryId,", fullClassName);
            code.WriteLine("System.Diagnostics.EventLogEntryType.{0},", etype);
            code.WriteLine("0x0{0:X8}L, ctorEventArguments, {1});", Item.MessageId, Item.IsException ? "this" : "null");
            code.Indent--;
        }
    }
}
