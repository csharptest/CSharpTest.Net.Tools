#region Copyright 2010 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Resources.Tools;
using System.Text.RegularExpressions;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Generators.ResX
{
    /// <summary>
    /// Implements the Excpetion generation
    /// </summary>
    partial class ResxGenWriter
    {
        /// <summary>
        /// Appends static properties used for event logging
        /// </summary>
        void WriteProperties(CsWriter code)
        {
            if (_options.AutoLog || !String.IsNullOrEmpty(_options.EventSource))
            {
                code.WriteLine();
                code.WriteSummaryXml("The event source used to write events");
                code.WriteLine("internal static readonly string EventSourceName = @\"{0}\";", 
                    String.IsNullOrEmpty(_options.EventSource) ? _fullClassName : _options.EventSource);

                code.WriteLine();
                code.WriteSummaryXml("The category id used to write events");
                using (code.WriteBlock("public static bool TryCreateException(int hResult, string message, out System.Exception exception)"))
                using (code.WriteBlock("switch (unchecked((uint)hResult))"))
                {
                    Dictionary<uint, bool> visited = new Dictionary<uint, bool>();
                    foreach (List<ResxGenItem> lst in _items.Values)
                        foreach (ResxGenItem item in lst)
                        {
                            if (!item.IsException)
                                break;
                            if (item.HResult != 0 && !visited.ContainsKey(item.HResult))
                            {
                                visited.Add(item.HResult, true);
                                string exName = StronglyTypedResourceBuilder.VerifyResourceName(item.ItemName, Csharp);
                                code.WriteLine("case 0x{0:x8}U: exception = {1}.{2}.Create(hResult, message); return true;", item.HResult, _nameSpace, exName);
                            }
                        }
                    code.WriteLine("default: exception = null; return false;");
                }

            }
            if (_options.FacilityId > 0)
            {
                code.WriteLine();
                code.WriteSummaryXml("The the event log facility id of events defined in this resource file");
                code.WriteLine("internal static readonly int EventFacilityId = {0};", _options.FacilityId);
            }
            if (_options.AutoLog)
            {
                if (String.IsNullOrEmpty(_options.EventSource))
                    Console.Error.WriteLine("Warning: AutoLog == true, but no event source name was defined.");

                code.WriteLine();
                code.WriteSummaryXml("The the event log used to write events for this resource file");
                code.WriteLine("internal static readonly string EventLogName = @\"{0}\";", String.IsNullOrEmpty(_options.EventLog) ? "Application" : _options.EventLog);

                code.WriteLine();
                code.WriteSummaryXml("The category id used to write events for this resource file");
                code.WriteLine("internal static readonly int EventCategoryId = {0};", Math.Max(0, _options.EventCategoryId));

                if (String.IsNullOrEmpty(_eventLogger))
                {
                    code.WriteLine();
                    code.WriteSummaryXml("Writes an event log for the specified message id and arguments");
                    using (code.WriteBlock("internal static void WriteEvent(string eventLog, string eventSource, int category, System.Diagnostics.EventLogEntryType eventType, long eventId, object[] arguments, System.Exception error)"))
                    using (code.WriteBlock("using (System.Diagnostics.EventLog log = new System.Diagnostics.EventLog(eventLog, \".\", eventSource))"))
                        code.WriteLine("log.WriteEvent(new System.Diagnostics.EventInstance(eventId, category, eventType), null, arguments);");
                }
            }
        }

        void WriteException(CsWriter code, List<ResxGenItem> lst)
        {
            if (lst.Count == 0 || lst[0].IsException == false)
                return;

            ResxGenItem first = lst[0];
            string exName = StronglyTypedResourceBuilder.VerifyResourceName(first.ItemName, Csharp);

            string baseName = ": " + _baseException;
            foreach (ResxGenItem item in lst)
                if (item.Comments.StartsWith(":"))
                    baseName = item.Comments;

            code.WriteSummaryXml("Exception class: {0} {1}\r\n{2}", exName, baseName, first.Value);
            code.WriteLine("[System.SerializableAttribute()]");
            using (
                code.WriteClass("public {2}{3}class {0} {1}", exName, baseName, _sealed ? "sealed " : "",
                                _partial ? "partial " : ""))
            {
                code.WriteSummaryXml("Serialization constructor");
                code.WriteBlock(
                    "{1} {0}(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)",
                    exName, _sealed ? "internal" : "protected")
                    .Dispose();

                WriteStaticFactory(code, exName);

                Dictionary<string, ResxGenArgument> publicData = new Dictionary<string, ResxGenArgument>();
                foreach (ResxGenItem item in lst)
                    foreach (ResxGenArgument arg in item.Args)
                        if (arg.IsPublic)
                            publicData[arg.Name] = arg;

                foreach (ResxGenArgument pd in publicData.Values)
                {
                    if (pd.Name == "HResult" || pd.Name == "HelpLink" || pd.Name == "Source")
                        continue; //uses base properties

                    code.WriteLine();
                    code.WriteSummaryXml("The {0} parameter passed to the constructor", pd.ParamName);
                    code.WriteLine(
                        "public {1} {0} {{ get {{ if (Data[\"{0}\"] is {1}) return ({1})Data[\"{0}\"]; else return default({1}); }} }}",
                        pd.Name, pd.Type);
                }
                code.WriteLine();
                
                foreach (ResxGenItem item in lst)
                {
                    string formatNm = String.Format("{0}.ExceptionStrings.{1}", _fullClassName, item.Identifier);
                    string formatFn = _fullClassName + ".ExceptionStrings.SafeFormat";
                    string baseArgs = item.IsFormatter ? formatFn + "({0}, {1})" : "{0}";
                    string argList = item.HasArguments ? ", " + item.Parameters(true) : "";
                    string strHResult = item.HResult != 0 ? String.Format("unchecked((int)0x{0:X8}U)", item.HResult) : "-1";

                    code.WriteSummaryXml(item.Value);
                    code.WriteLine("public {0}({1})", exName, item.Parameters(true));
                    using (code.WriteBlock("\t: this((System.Exception)null, {0}, {1})", strHResult, String.Format(baseArgs, formatNm, item.Parameters(false))))
                    {
                        foreach (ResxGenArgument arg in item.Args)
                            WriteSetProperty(code, arg.IsPublic, arg.Name, arg.ParamName);
                        if (item.AutoLog)
                            code.WriteLine("WriteEvent({0});", item.Parameters(false));
                    }
                    code.WriteSummaryXml(item.Value);
                    code.WriteLine("public {0}({1}{2}System.Exception innerException)", exName, item.Parameters(true), item.HasArguments ? ", " : "");
                    using (code.WriteBlock("\t: this(innerException, {0}, {1})", strHResult, String.Format(baseArgs, formatNm, item.Parameters(false))))
                    {
                        foreach (ResxGenArgument arg in item.Args)
                            WriteSetProperty(code, arg.IsPublic, arg.Name, arg.ParamName);
                        if (item.AutoLog)
                            code.WriteLine("WriteEvent({0});", item.Parameters(false));
                    }

                    if (item.AutoLog)
                        WriteAutoLog(code, item);

                    code.WriteSummaryXml("if(condition == false) throws {0}", item.Value);
                    using (code.WriteBlock("public static void Assert(bool condition{0})", argList))
                        code.WriteLine("if (!condition) throw new {0}({1});", exName, item.Parameters(false));
                }
            }
        }

        void WriteStaticFactory(CsWriter code, string exceptionClass)
        {
            code.WriteSummaryXml("Used to recreate this exception from an hresult and message bypassing the message formatting");
            using (code.WriteBlock("internal static System.Exception Create(int hResult, string message)"))
                code.WriteLine("return new {0}.{1}((System.Exception)null, hResult, message);", _nameSpace, exceptionClass);

            code.WriteSummaryXml("Constructs the exception from an hresult and message bypassing the message formatting");
            using (code.WriteBlock("{0} {1}(System.Exception innerException, int hResult, string message) : base(message, innerException)",
                _sealed ? "private" : "protected", exceptionClass))
            {
                code.WriteLine("base.HResult = hResult;");
                if (!String.IsNullOrEmpty(_options.HelpLinkFormat))
                    code.WriteLine("base.HelpLink = {0}.ExceptionStrings.HelpLinkForHR(HResult);", _fullClassName);
                if (!String.IsNullOrEmpty(_options.EventSource))
                    code.WriteLine("base.Source = {0}.EventSourceName;", _fullClassName);
                if (_options.AutoLog && _options.EventCategoryId > 0)
                    WriteSetProperty(code, true, "CategoryId", _fullClassName + ".EventCategoryId");
            }
        }

        static void WriteSetProperty(CsWriter code, bool isPublic, string argName, string argValue)
        {
            if (isPublic)
            {
                if (argName == "HResult" || argName == "HelpLink" || argName == "Source")
                    code.WriteLine("base.{0} = {1};", argName, argValue);
                else
                    code.WriteLine("base.Data[\"{0}\"] = {1};", argName, argValue);
            }
        }

        void WriteAutoLog(CsWriter code, ResxGenItem item)
        {
            code.WriteSummaryXml("Prepares the arguments as strings and writes the event");
            using (code.WriteBlock("private void WriteEvent({0})", item.Parameters(true)))
            {
                WriteEventLogCall(code, item);
            }
        }

        void WriteEventLogCall(CsWriter code, ResxGenItem item)
        {
            Dictionary<int, string> formatting = new Dictionary<int, string>();
            for (int i = 0; i < item.Args.Count; i++)
                formatting[i] = "{0}";
            foreach (Match m in RegexPatterns.FormatSpecifier.Matches(item.Value))
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
            for (int i = 0; i < item.Args.Count; i++)
                code.WriteLine("{0}.ExceptionStrings.SafeFormat(@\"{1}\", {2}),", _fullClassName, formatting[i], item.Args[i].ParamName);
            code.WriteLine("};");

            using (code.WriteBlock("try"))
            {
                EventLogEntryType etype = item.MessageId < 0x80000000
                                              ? EventLogEntryType.Information
                                              : ((item.MessageId & 0xC0000000) == 0xC0000000)
                                                    ? EventLogEntryType.Error
                                                    : EventLogEntryType.Warning;
                string logMethod = String.IsNullOrEmpty(_eventLogger)
                                       ? String.Format("{0}.WriteEvent", _fullClassName)
                                       : _eventLogger;
                code.WriteLine("{0}(", logMethod);
                code.Indent++;
                code.WriteLine("{0}.EventLogName,", _fullClassName);
                code.WriteLine("{0}.EventSourceName,", _fullClassName);
                code.WriteLine("{0}.EventCategoryId,", _fullClassName);
                code.WriteLine("System.Diagnostics.EventLogEntryType.{0},", etype);
                code.WriteLine("0x0{0:X8}L, ctorEventArguments, {1});", item.MessageId, item.IsException ? "this" : "null");
                code.Indent--;
            }
            code.WriteLine("catch { }");
        }
    }
}
