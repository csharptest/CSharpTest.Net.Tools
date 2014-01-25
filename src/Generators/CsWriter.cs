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
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;
using System.Text;
using CSharpTest.Net.Collections;

namespace CSharpTest.Net.Generators
{
    public class CsWriter : IndentedTextWriter
    {
        private readonly DisposingList _open;

        public CsWriter() : this(new StringWriter()) { }
        public CsWriter(TextWriter writer) : base(writer)
        {
            _open = new DisposingList();
        }

        protected override void Dispose(bool disposing)
        {
            _open.Dispose();
            base.Dispose(disposing);
        }

        public override string ToString()
        {
            return InnerWriter.ToString();
        }

		public string MakeString(string data)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            foreach (char ch in data)
            {
                if (ch >= 32 && ch < 128)
                {
                    if (ch == '\\' || ch == '\'' || ch == '"')
                        sb.Append('\\');
                    sb.Append(ch);
                    continue;
                }
                if (ch == '\r') { sb.Append("\\r"); continue; }
                if (ch == '\n') { sb.Append("\\n"); continue; }
                if (ch == '\t') { sb.Append("\\t"); continue; }

                sb.Append('\\');
                sb.Append((char)('0' + ((ch >> 6) & 3)));
                sb.Append((char)('0' + ((ch >> 3) & 7)));
                sb.Append((char)('0' + (ch & 7)));
            }
            sb.Append('"');
            return sb.ToString();
		}

        public void AddNamespaces(params string[] namespaces)
        {
            foreach (string ns in namespaces)
                WriteLine("using {0};", ns);
        }

        public IDisposable WriteNamespace(string ns) 
        {
            if (String.IsNullOrEmpty(ns))
                return new DisposingList();//just some IDisposable... ignored.

            return WriteBlock("namespace {0}", ns);
        }

        public void WriteSummaryXml(string content, params object[] args)
        {
            if (args != null && args.Length > 0)
                content = String.Format(content, args);
            string line;
            WriteLine("/// <summary>");
            using (StringReader sr = new StringReader(content))
                while (null != (line = sr.ReadLine()))
                    WriteLine("/// {0}", System.Web.HttpUtility.HtmlEncode(line));
            WriteLine("/// </summary>");
        }

        public void WriteClassPreamble()
        {
            Assembly generator = Assembly.GetCallingAssembly() ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly() ?? GetType().Assembly;
            WriteLine("[global::System.Diagnostics.DebuggerStepThroughAttribute()]");
            WriteLine("[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]");
            WriteLine("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]");
            WriteLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"{0}\", \"{1}\")]", generator.GetName().Name, generator.GetName().Version);
        }

        public IDisposable WriteClass(string format, params object[] args)
        {
            WriteClassPreamble();
            return WriteBlock(format, args);
        }

        public IDisposable WriteBlock(string format, params object[] args)
        {
            if (args != null && args.Length > 0)
                format = String.Format(format, args);
            if (!String.IsNullOrEmpty(format))
            {
                string line;
                using (StringReader r = new StringReader(format))
                    while (null != (line = r.ReadLine()))
                        WriteLine(line);
            }
            return WriteBlock();
        }

		public IDisposable WriteBlock() { return new Braces(this); }

        private class Braces : IDisposable
        {
            private CsWriter _wtr;
            public Braces(CsWriter wtr)
            {
                _wtr = wtr;
                _wtr.WriteLine("{");
                _wtr.Indent++;
                _wtr._open.Add(this);
            }
            void IDisposable.Dispose()
            {
                if (_wtr != null && _wtr._open.Remove(this))
                {
                    _wtr.Indent--;
                    _wtr.WriteLine("}");
                    _wtr = null;
                }
            }
        }
    }
}
