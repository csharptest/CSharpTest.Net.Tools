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
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Resources;
using System.Resources.Tools;
using CSharpTest.Net.Generators.ResX.Writers;
using Microsoft.CSharp;

namespace CSharpTest.Net.Generators.ResX
{
	partial class ResxGenWriter
	{
		private static readonly CSharpCodeProvider Csharp = new CSharpCodeProvider();
		private readonly string _fileName;
		private readonly string _nameSpace;
		private readonly string _className;
        private readonly string _fullClassName;
		private readonly string _resxNameSpace;
        private readonly string _baseException;
        private readonly string _eventLogger;
		private readonly bool _public, _partial, _sealed;
        private readonly ResXOptions _options;

		private readonly List<ResXDataNode> _xnodes;
        private readonly List<ResxString> _xstrings;
        private readonly Dictionary<string, ResxException> _xexceptions;

        public ResxGenWriter(string filename, string nameSpace, string resxNameSpace, bool asPublic, bool asPartial, bool asSealed, 
            string className, string baseException)
		{
            _options = new ResXOptions();
			_fileName = filename;
			_nameSpace = nameSpace;
			_resxNameSpace = resxNameSpace;
			_public = asPublic;
		    _partial = asPartial;
			_sealed = asSealed;
			_className = className;
            _baseException = baseException;
            _fullClassName = String.Format("{0}.{1}", _nameSpace, _className);
            _eventLogger = null;

			//Environment.CurrentDirectory = Path.GetDirectoryName(_fileName);
            Parse(_xstrings = new List<ResxString>(), _xexceptions = new Dictionary<string, ResxException>(StringComparer.Ordinal), _xnodes = new List<ResXDataNode>());
            _eventLogger = _eventLogger ?? _options.AutoLogMethod;
		}

        void Parse(List<ResxString> xstrings, Dictionary<string, ResxException> xexceptions, List<ResXDataNode> xnodes)
		{
            foreach (ResxGenItem item in _options.ReadFile(_fileName))
            {
                if (!item.Hidden)
                    xnodes.Add(item.Node);
                if (!item.Ignored)
                {
                    if (!item.IsException)
                        xstrings.Add(new ResxString(item));
                    else
                    {
                        ResxException ex;
                        if (xexceptions.TryGetValue(item.MemberName, out ex))
                            ex.AddOverload(item);
                        else
                            xexceptions.Add(item.MemberName, new ResxException(item));
                    }
                }
            }
		}

		public void Write(TextWriter output)
        {
			string resxCode = CreateResources();
			int lastBrace = resxCode.LastIndexOf('}') - 1;
			int nextLastBrace = resxCode.LastIndexOf('}', lastBrace) - 1;
            if(String.IsNullOrEmpty(_nameSpace))
                nextLastBrace = lastBrace;

			output.WriteLine(resxCode.Substring(0, nextLastBrace - 1));

			using (CsWriter code = new CsWriter())
			{
				//Add formatting methods
				code.Indent = 2;
				code.WriteLine();

                WriteProperties(code);
				WriteFormatters(code);
				code.Indent--;
				code.WriteLine();
				code.WriteLine(resxCode.Substring(nextLastBrace, lastBrace - nextLastBrace));

                foreach (ResxException e in _xexceptions.Values)
                    e.WriteException(code, _fullClassName, "ExceptionStrings.", _baseException, _sealed, _partial);

				output.WriteLine(code.ToString());
			}

			output.WriteLine(resxCode.Substring(lastBrace));
		}

        public bool Test(TextWriter errors)
        {
            bool success = true;
            Action<string> report = delegate(string message)
            { errors.WriteLine("{0}({1}): error: {2}", _fileName, 0, message); };

            foreach (ResxString xs in _xstrings)
                success = xs.Test(report) && success;
            foreach (ResxException xs in _xexceptions.Values)
                success = xs.Test(report) && success;
            return success;
        }

		string CreateResources()
		{
			//Now we've loaded our own type data, we need to generate the resource accessors:
			string[] errors;

			Hashtable all = new Hashtable();
			foreach (ResXDataNode node in _xnodes)
				all[node.Name] = node;

			CodeCompileUnit unit = StronglyTypedResourceBuilder.Create(all,
				_className, _nameSpace, _resxNameSpace, Csharp, !_public, out errors);

			foreach (string error in errors)
				Console.Error.WriteLine("Warning: {0}", error);

			CodeGeneratorOptions options = new CodeGeneratorOptions();
			options.BlankLinesBetweenMembers = false;
			options.BracingStyle = "C";
			options.IndentString = "    ";

			using (StringWriter swCode = new StringWriter())
			{
				Csharp.GenerateCodeFromCompileUnit(unit, swCode, options);
				string result = swCode.ToString();

                if (_partial)
                    result = result.Replace(" class ", " partial class ");
			    return result;
			}
		}

		void WriteFormatters(CsWriter code)
		{
            if (_xstrings.Count > 0)
            {
                foreach (ResxString s in _xstrings)
                    s.WriteAccessor(code, _fullClassName, "FormatStrings.");

                code.WriteLine();
                code.WriteSummaryXml("Returns the raw format strings.");
                using (code.WriteClass("public static {0}class FormatStrings", _partial ? "partial " : ""))
                {
                    foreach (ResxString s in _xstrings)
                        s.WriteFormatString(code);
                }
            }
		    if (_xexceptions.Count > 0)
			{
				code.WriteLine();
				code.WriteSummaryXml("Returns the raw exception strings.");
                using (code.WriteClass("public static {0}class ExceptionStrings", _partial ? "partial " : ""))
				{
                    code.WriteSummaryXml("Formats a message for an exception");
                    using (code.WriteBlock("internal static string SafeFormat(string message, params object[] args)"))
                    {
                        using(code.WriteBlock("try"))
                            code.WriteLine("return string.Format(resourceCulture, message, args);");
                        using(code.WriteBlock("catch"))
                            code.WriteLine("return message ?? string.Empty;");
                    }

                    string helpLinkFormat = _options.HelpLinkFormat ?? String.Empty;
                    String.Format(helpLinkFormat, 5, String.Empty);//just to make sure it's a valid format

                    code.WriteSummaryXml("{0}", _options.HelpLinkFormat);
                    using (code.WriteBlock("internal static string HelpLinkFormat(int hResult, string typeName)"))
                        code.WriteLine("return SafeFormat({0}, hResult, typeName);", code.MakeString(helpLinkFormat));

                    foreach (ResxException ex in _xexceptions.Values)
                        ex.WriteFormatString(code);
				}
			}
		}

        /// <summary>
        /// Appends static properties used for event logging
        /// </summary>
        void WriteProperties(CsWriter code)
        {
            if (_options.AutoLog)
            {
                code.WriteLine();
                code.WriteSummaryXml("Create the appropriate type of exception from an hresult using the specified message");
                using (code.WriteBlock("public static bool TryCreateException(int hResult, string message, out System.Exception exception)"))
                using (code.WriteBlock("switch (unchecked((uint)hResult))"))
                {
                    Dictionary<uint, bool> visited = new Dictionary<uint, bool>();
                    foreach (ResxException ex in _xexceptions.Values)
                    {
                        if (ex.HResult != 0 && !visited.ContainsKey(ex.HResult))
                        {
                            visited.Add(ex.HResult, true);
                            code.WriteLine(
                                "case 0x{0:x8}U: exception = {1}.{2}.Create(hResult, message); return true;",
                                ex.HResult, _nameSpace, ex.MemberName);
                        }
                    }
                    code.WriteLine("default: exception = null; return false;");
                }

                code.WriteLine();
                code.WriteSummaryXml("The the event log facility id of events defined in this resource file");
                code.WriteLine("internal static readonly int EventFacilityId = {0};", Math.Max(0, _options.FacilityId));

                code.WriteLine();
                code.WriteSummaryXml("The category id used to write events for this resource file");
                code.WriteLine("internal static readonly int EventCategoryId = {0};", Math.Max(0, _options.EventCategoryId));

                if (String.IsNullOrEmpty(_options.EventSource))
                    Console.Error.WriteLine("Warning: AutoLog == true, but no event source name was defined.");

                code.WriteLine();
                code.WriteSummaryXml("The the event log used to write events for this resource file");
                code.WriteLine("internal static readonly string EventLogName = {0};", 
                    code.MakeString(String.IsNullOrEmpty(_options.EventLog) ? "Application" : _options.EventLog));

                code.WriteLine();
                code.WriteSummaryXml("The event source used to write events");
                code.WriteLine("internal static readonly string EventSourceName = {0};",
                    code.MakeString(String.IsNullOrEmpty(_options.EventSource) ? "" : _options.EventSource));

                code.WriteLine();
                code.WriteSummaryXml("Writes an event log for the specified message id and arguments");
                using (code.WriteBlock("internal static void WriteEvent(string eventLog, string eventSource, int category, System.Diagnostics.EventLogEntryType eventType, long eventId, object[] arguments, System.Exception error)"))
                {
                    using (code.WriteBlock("try"))
                    {
                        if (String.IsNullOrEmpty(_eventLogger))
                        {
                            using (code.WriteBlock("using (System.Diagnostics.EventLog log = new System.Diagnostics.EventLog(eventLog, \".\", eventSource))"))
                                code.WriteLine("log.WriteEvent(new System.Diagnostics.EventInstance(eventId, category, eventType), null, arguments);");
                        }
                        else
                        {
                            code.WriteLine("{0}(eventLog, eventSource, category, eventType, eventId, arguments, error);",_eventLogger);
                        }
                    }
                    code.WriteLine("catch { }");
                }
            }
        }
	}
}
