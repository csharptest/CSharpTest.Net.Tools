#region Copyright 2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using CSharpTest.Net.Http;

namespace CSharpTest.Net.Commands
{
    #region Http Attributes
    /// <summary>
    /// Specifies that an argument should be ignored for HTTP requests.
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
    public class HttpIgnoreAttribute : Attribute
    {
        /// <summary>
        /// A default value that applies to an argument when invoked from the http service
        /// </summary>
        public object HttpDefaultValue { get; set; }
    }

    /// <summary>
    /// Specifies that an argument should be fill in from an HTTP header.
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class HttpHeaderBindingAttribute : Attribute
    {
        private string _header;
        private string[] _regexMatch;

        /// <summary> The HTTP header to bind the attribute to, i.e. "Accept" </summary>
        public string Header
        {
            get { return _header; }
        }

        /// <summary> One or More regular expressions that match a specific pattern in the HTTP header, i.e. "^(?&lt;=text/|application/)xml" </summary>
        public string[] RegexMatch
        {
            get { return _regexMatch; }
        }

        /// <summary> Constructs the attribute </summary>
        public HttpHeaderBindingAttribute(string header)
            : this(header, ".*")
        { }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="header">The HTTP header to bind the attribute to, i.e. "Accept" </param>
        /// <param name="regexMatch">One or More regular expressions that match a specific pattern in the HTTP header, i.e. "^(?&lt;=text/|application/)xml"</param>
        public HttpHeaderBindingAttribute(string header, params string[] regexMatch)
        {
            _header = header;
            _regexMatch = regexMatch;
        }
    }

    /// <summary>
    /// Indicates that the argument is a filename that should be uploaded from the client
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class HttpRequestFileAttribute : Attribute
    {
        private readonly string _mimeType;
        /// <summary> Constructs the attribute </summary>
        public HttpRequestFileAttribute() : this(null) { }
        /// <summary> Constructs the attribute </summary>
        public HttpRequestFileAttribute(string mimeType)
        {
            _mimeType = mimeType;
        }
        /// <summary> Returns the mime type given, or null</summary>
        public string MimeType { get { return _mimeType; } }
        /// <summary> True to allow multiple files to be uploaded, callee will recieve directory path if more than one file is uploaded. </summary>
        public bool AllowMultiple { get; set; }
    }

    /// <summary>
    /// Indicates that the argument is an output filename of the command
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class HttpResponseFileAttribute : Attribute
    {
        private readonly string _extension;
        private readonly string _mimeType;
        /// <summary> Constructs the attribute </summary>
        public HttpResponseFileAttribute(string extension) : this(extension, "application/binary") { }
        /// <summary> Constructs the attribute </summary>
        public HttpResponseFileAttribute(string extension, string mimeType)
        {
            _extension = extension;
            _mimeType = mimeType;
        }
        /// <summary> Returns the mime type given, or null</summary>
        public string Extension { get { return _extension; } }
        /// <summary> Returns the mime type given, or null</summary>
        public string MimeType { get { return _mimeType; } }
    }

    /// <summary>
    /// Sets the response type to a fixed mime-type on a command
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    public class HttpResponseTypeAttribute : Attribute
    {
        private readonly string _mimeType;
        /// <summary> Constructs the attribute </summary>
        public HttpResponseTypeAttribute(string mimeType) {_mimeType = mimeType; }
        /// <summary> Returns the mime type given</summary>
        public string MimeType { get { return _mimeType; } }
    }
    #endregion

    partial class CommandInterpreter
    {
        /// <summary>
        /// Returns the list of HTTP prefixes that will be used for servicing the hosthttp command.
        /// </summary>
        public static string[] GetHttpPrefixes(bool allowRemote, int port, string rootFolder)
        {
            rootFolder = String.IsNullOrEmpty(rootFolder) ? "/" : (rootFolder.Trim().Trim('/', '\\') + '/');
            rootFolder = '/' + rootFolder.TrimStart('/');
            return allowRemote
                ? new[]
                        {
                            String.Format(@"http://{0}:{1}{2}", Environment.MachineName, port, rootFolder),
                            String.Format(@"http://*:{0}{1}", port, rootFolder)
                        }
                : new[]
                        {
                            String.Format(@"http://localhost:{0}{1}", port, rootFolder),
                            String.Format(@"http://127.0.0.1:{0}{1}", port, rootFolder)
                        };
        }

        /// <summary>
        /// Starts a HttpListener for hosting the commands available on this instance over http/REST
        /// </summary>
        [Command("HostHttp", Category = "Built-in", Description = "Hosts a local http listener for RESTful access.", Visible = true), HttpIgnore]
        public void HostHttp(
            [Argument("port", DefaultValue = 8080, Description = "The port number to listen on.")] int port,
            [Argument("root", DefaultValue = "", Description = "Requests constrained within the URI folder.")] string rootFolder,
            [Argument("remote", DefaultValue = false, Description = "Allow requests from other machines.")] bool allowRemote,
            [Argument("browse", DefaultValue = false, Description = "Allow requests from other machines.")] bool startBrowser)
        {
            string[] prefixes = GetHttpPrefixes(allowRemote, port, rootFolder);
            using (HttpServer server = new CSharpTest.Net.Http.HttpServer(5))
            {
                server.ProcessRequest += ServerOnProcessRequest;
                server.Start(prefixes);

                Console.WriteLine("Listening on {0}", prefixes[0]);
                if (startBrowser)
                    System.Diagnostics.Process.Start(prefixes[0]);
                Console.WriteLine("Press [Enter] to quit");
                Console.ReadLine();

                server.Stop();
            }
        }

        private void ServerOnProcessRequest(object sender, HttpContextEventArgs eventArg)
        {
            HttpListenerContext context = eventArg.Context;
            try
            {
                context.Response.Headers["Server"] = "C0D3";
                context.Response.Headers["Expires"] = "0";
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";

                string applicationPath = context.Request.Url.AbsolutePath.TrimEnd('/', '\\') + '/';
                if (applicationPath.StartsWith(eventArg.Host.ApplicationPath, StringComparison.OrdinalIgnoreCase))
                    applicationPath = applicationPath.Substring(eventArg.Host.ApplicationPath.Length - 1);

                string[] segments = applicationPath.TrimEnd('/', '\\').Split('/', '\\');
                
                HttpIgnoreAttribute ignore;
                ICommand cmd;
                bool execute = _commands.TryGetValue(context.Request.Url.AbsolutePath, out cmd);
                if (cmd == null && segments.Length == 2)
                {
                    if (_commands.TryGetValue(segments[1], out cmd))
                        execute = (!String.IsNullOrEmpty(context.Request.Url.Query) ||
                                   context.Request.HttpMethod.ToUpperInvariant() != "GET");
                }

                if (cmd != null && cmd.TryGetAttribute(out ignore))
                {
                    throw new UnauthorizedAccessException("The command is not available.");
                }
                else if (cmd != null && execute)
                {
                    ExecCommand(context, cmd);
                    return;
                }
                else if (cmd != null || (cmd == null && segments.Length == 1))
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    using (SwitchedOutputStream output = new SwitchedOutputStream(context.Response.OutputStream, ushort.MaxValue))
                    using (StreamWriter wtr = new StreamWriter(output))
                    {
                        GenerateHtmlPage(wtr, eventArg.Host.ApplicationPath, cmd != null ? cmd.DisplayName : null);
                        if (!output.OutputSent)
                            context.Response.ContentLength64 = output.BufferPosition;
                        output.Commit();
                    }
                    return;
                }

                WriteErrorPage(context, 404, "Not Found", "The url is malformed or the command name is incorrect.");
            }
            catch (InterpreterException e)
            {
                try { WriteErrorPage(context, 400, "Bad Request", e.Message); }
                catch { }
            }
            catch (UnauthorizedAccessException e)
            {
                try { WriteErrorPage(context, 403, "Forbidden", e.Message); }
                catch { }
            }
            catch (Exception e)
            {
                try { WriteErrorPage(context, 500, "Internal Server Error", e.Message); }
                catch { }
            }
        }

        private void ExecCommand(HttpListenerContext context, ICommand cmd)
        {
            TextWriter stdOut;
            TextWriter stdErr = new StringWriter();

            string tempdir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").Substring(0, 16));
            SwitchedOutputStream output = new SwitchedOutputStream(context.Response.OutputStream, ushort.MaxValue);
            try
            {
                string contentType = "text/plain";
                IArgument reqFile = null;
                IArgument resFile = null;

                foreach (IArgument argument in cmd.Arguments)
                {
                    HttpRequestFileAttribute reqFileAttr = null;
                    HttpResponseFileAttribute resFileAttr = null;
                    if (argument.TryGetAttribute(out reqFileAttr))
                        Check.Assert<ArgumentException>(null == Interlocked.Exchange(ref reqFile, argument));
                    if (argument.TryGetAttribute(out resFileAttr))
                        Check.Assert<ArgumentException>(null == Interlocked.Exchange(ref resFile, argument));
                }

                if (reqFile != null && reqFile.Required && (
                                                               context.Request.HttpMethod.ToUpperInvariant() != "POST" ||
                                                               !context.Request.ContentType.StartsWith(
                                                                   "multipart/form-data",
                                                                   StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException();

                List<string> args = new List<string>();
                args.Add(cmd.DisplayName);
                HttpResponseTypeAttribute ctypeAttr;

                if (cmd.TryGetAttribute(out ctypeAttr) && !String.IsNullOrEmpty(ctypeAttr.MimeType))
                    contentType = ctypeAttr.MimeType;

                GetArguments(context, cmd, tempdir, ref contentType, args);

                if (resFile != null)
                {
                    HttpResponseFileAttribute fattr;
                    resFile.TryGetAttribute(out fattr);

                    Directory.CreateDirectory(tempdir);
                    string tempPath = Path.Combine(tempdir, cmd.DisplayName + fattr.Extension);
                    args.Add(String.Format("/{0}={1}", resFile.DisplayName, tempPath));

                    using (stdOut = new StreamWriter(output, Encoding.UTF8))
                        Run(args.ToArray(), stdOut, stdErr, TextReader.Null);

                    if (output.OutputSent)
                        throw new ApplicationException("Headers already sent.");

                    context.Response.ContentType = fattr.MimeType ?? "application/binary";
                    context.Response.Headers.Add("Content-Disposition",
                                                 String.Format("attachment; filename=\"{0}{1}\"", cmd.DisplayName,
                                                               fattr.Extension));
                    using (Stream ostream = context.Response.OutputStream)
                    using (Stream istream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        int len;
                        byte[] buffer = new byte[ushort.MaxValue];
                        while (0 != (len = istream.Read(buffer, 0, buffer.Length)))
                            ostream.Write(buffer, 0, len);
                    }
                }
                else
                {
                    context.Response.ContentType = contentType +
                                                   (contentType.Contains("text") || contentType.Contains("xml") ||
                                                    contentType.Contains("json")
                                                        ? "; charset=utf-8"
                                                        : "");

                    using (stdOut = new StreamWriter(output, Encoding.UTF8))
                        Run(args.ToArray(), stdOut, stdErr, TextReader.Null);
                }

                if (!output.OutputSent)
                    context.Response.ContentLength64 = output.BufferPosition;

                output.Commit();
            }
            catch (InterpreterException) { throw; }
            catch (Exception e)
            {
                if (output.OutputSent)
                {
                    using (stdOut = new StreamWriter(output, Encoding.UTF8))
                    {
                        stdOut.Write("EXCEPTION: ");
                        stdOut.WriteLine(e.Message);
                        stdOut.WriteLine(stdErr.ToString());
                    }
                    output.Commit();
                }
                else
                    WriteErrorPage(context, 500, "Internal Server Error", e.Message, stdErr.ToString(), output.ToString());
            }
            finally
            {
                if (Directory.Exists(tempdir))
                {
                    try
                    {
                        Directory.Delete(tempdir, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void GetArguments(HttpListenerContext context, ICommand cmd, string tempdir, ref string contentType, List<string> args)
        {
            string acceptArgument = null;
            string originalType = contentType;
            HttpIgnoreAttribute ignored;
            Dictionary<string, string> exists = new Dictionary<string, string>();

            foreach (IArgument arg in cmd.Arguments)
            {
                string value = null;
                HttpHeaderBindingAttribute hdr;
                if (arg.TryGetAttribute(out hdr))
                {
                    string text = context.Request.Headers[hdr.Header];
                    if (text == null)
                        value = null;
                    else if (StringComparer.OrdinalIgnoreCase.Equals(hdr.Header, "Accept"))
                    {
                        acceptArgument = arg.DisplayName;
                        foreach (string x in text.Split(','))
                        {
                            string part = x.Trim();
                            if (part.IndexOf(';') > 0)
                                part = part.Substring(0, part.IndexOf(';')).Trim();

                            foreach (string exp in hdr.RegexMatch)
                            {
                                Match m = Regex.Match(part, exp);
                                if (m.Success)
                                {
                                    contentType = part;
                                    value = m.Value;
                                    break;
                                }
                            }
                            if (value != null)
                                break;
                        }
                    }
                    else
                    {
                        foreach (string exp in hdr.RegexMatch)
                        {
                            Match m = Regex.Match(text, exp);
                            if (m.Success)
                            {
                                value = m.Value;
                                break;
                            }
                        }
                    }
                }
                exists.Add(arg.DisplayName, value);
            }

            string query = context.Request.Url.Query;
            //application/x-www-form-urlencoded
            if (context.Request.HttpMethod.ToUpperInvariant() == "POST" && context.Request.ContentType != null &&
                context.Request.ContentType.Contains("application/x-www-form-urlencoded"))
            {
                using (TextReader r = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
                    query += "&" + r.ReadToEnd();
            }
            //multipart/form-data
            if (context.Request.HttpMethod.ToUpperInvariant() == "POST" && context.Request.ContentType != null &&
                context.Request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                MimeMultiPartData form = new MimeMultiPartData(context.Request.InputStream, context.Request.Headers);
                foreach (KeyValuePair<string, string> set in form.ToDictionary())
                {
                    if (exists.ContainsKey(set.Key) && !String.IsNullOrEmpty(set.Value))
                        exists[set.Key] = set.Value;
                }
                foreach (IArgument arg in cmd.Arguments)
                {
                    HttpRequestFileAttribute a;
                    if (arg.TryGetAttribute(out a))
                    {
                        List<MimeMessagePart> parts = new List<MimeMessagePart>(form.GetAllPartsByName(arg.DisplayName));
                        Directory.CreateDirectory(tempdir);
                        foreach (MimeMessagePart part in parts)
                            File.WriteAllBytes(Path.Combine(tempdir, part.FileName), part.Body);
                        
                        if (parts.Count == 1)
                            exists[arg.DisplayName] = Path.Combine(tempdir, parts[0].FileName);
                        else if (parts.Count > 1)
                            exists[arg.DisplayName] = tempdir;
                    }
                }
            }

            foreach (string arg in query.TrimStart('?').Split('&'))
            {
                string[] parts = arg.Split(new char[] { '=' }, 2);
                if (parts.Length == 2 && parts[1].Length > 0)
                {
                    parts[0] = Uri.UnescapeDataString(parts[0]);
                    parts[1] = Uri.UnescapeDataString(parts[1]);
                    if (exists.ContainsKey(parts[0]))
                    {
                        exists[parts[0]] = parts[1];
                        if (StringComparer.OrdinalIgnoreCase.Equals(acceptArgument, parts[0]))
                            contentType = originalType;
                    }
                }
            }

            foreach (IArgument arg in cmd.Arguments)
                if (arg.TryGetAttribute(out ignored))
                    exists[arg.DisplayName] = ignored.HttpDefaultValue == null ? null : ignored.HttpDefaultValue.ToString();

            foreach (KeyValuePair<string, string> set in exists)
            {
                if (set.Value != null)
                    args.Add(String.Format("/{0}={1}", set.Key, set.Value));
            }
        }

        private void WriteErrorPage(HttpListenerContext context, int code, string status, string errorDesc, params string[] messages)
        {
            context.Response.StatusCode = code;
            context.Response.StatusDescription = status;
            context.Response.ContentType = "text/html; charset=utf-8";

            using (XmlTextWriter w = new XmlTextWriter(context.Response.OutputStream, Encoding.UTF8))
            {
                w.Formatting = System.Xml.Formatting.Indented;
                w.WriteStartElement("html");
                w.WriteStartElement("head");
                w.WriteElementString("title", code + " - " + status);
                w.WriteEndElement();
                w.WriteStartElement("body");
                {
                    w.WriteElementString("h1", code + " - " + status);
                    w.WriteStartElement("p");
                    w.WriteElementString("strong", errorDesc);
                    w.WriteEndElement();
                    foreach (string message in messages)
                        w.WriteElementString("p", message);
                }
                w.WriteEndElement();
                w.WriteEndElement();
                w.Flush();
            }
        }

        private void GenerateHtmlPage(TextWriter sw, string appRoot, string itemName)
        {
            List<IDisplayInfo> items = new List<IDisplayInfo>();
            if (itemName != null)
                items.Add(_commands[itemName]);
            else
                items.AddRange(Commands);

            using (XmlTextWriter w = new XmlTextWriter(sw))
            {
                w.Formatting = System.Xml.Formatting.Indented;
                w.WriteStartElement("html");
                w.WriteStartElement("head");
                w.WriteElementString("title", Constants.ProcessName + " Help");
                w.WriteEndElement();
                w.WriteStartElement("body");
                {
                    HttpIgnoreAttribute ignored;
                    if (items.Count == 1)
                    {
                        Command command = (Command)items[0];
                        
                        HttpResponseFileAttribute resFileAttr = null;
                        HttpRequestFileAttribute reqFileAttr = null;
                        foreach (IArgument arg in command.Arguments)
                        {
                            if (reqFileAttr == null)
                                arg.TryGetAttribute(out reqFileAttr);
                            if (resFileAttr == null)
                                arg.TryGetAttribute(out resFileAttr);
                        }
                        
                        w.WriteElementString("h1", command.DisplayName);
                        w.WriteElementString("p", command.Description);

                        w.WriteStartElement("a");
                        w.WriteAttributeString("href", appRoot);
                        w.WriteString("<< Back to Top");
                        w.WriteEndElement();

                        w.WriteStartElement("form");
                        {
                            w.WriteAttributeString("name", command.DisplayName.ToLowerInvariant() + "Form");
                            if (resFileAttr == null)
                                w.WriteAttributeString("onsubmit", "document.getElementById('" + command.DisplayName.ToLowerInvariant() + "Submit').disabled = true;");

                            w.WriteAttributeString("action", appRoot + command.DisplayName + "/");
                            if (reqFileAttr != null)
                            {
                                w.WriteAttributeString("method", "post");
                                w.WriteAttributeString("enctype", "multipart/form-data");
                            }
                            else
                            {
                                w.WriteAttributeString("method", "get");
                            }

                            w.WriteStartElement("ul");
                            w.WriteAttributeString("style", "list-style-type: none;");

                            w.WriteStartElement("li");
                            w.WriteElementString("strong", "Arguments:");
                            w.WriteEndElement();

                            foreach (Argument arg in command.Arguments)
                            {
                                if (arg.Visible == false || arg.Type == typeof(ICommandInterpreter))
                                    continue;
                                if (arg.IsAllArguments)
                                    continue;

                                HttpHeaderBindingAttribute hdr;
                                if (arg.TryGetAttribute(out resFileAttr) || arg.TryGetAttribute(out hdr) || arg.TryGetAttribute(out ignored))
                                    continue;

                                w.WriteStartElement("li");

                                w.WriteStartElement("strong");
                                w.WriteAttributeString("style", "display:inline-block; width: 100px; text-align: right;");
                                w.WriteString(arg.DisplayName);
                                w.WriteEndElement();

                                w.WriteStartElement("input");
                                w.WriteAttributeString("name", arg.DisplayName);
                                w.WriteAttributeString("style", "width: 300px;");
                                if (arg.Required)
                                    w.WriteAttributeString("required", "required");

                                if (arg.TryGetAttribute(out reqFileAttr))
                                {
                                    w.WriteAttributeString("type", "file");
                                    if (reqFileAttr.AllowMultiple)
                                        w.WriteAttributeString("multiple", "multiple");
                                    if (!String.IsNullOrEmpty(reqFileAttr.MimeType))
                                        w.WriteAttributeString("accept", reqFileAttr.MimeType);

                                }
                                else
                                {
                                    w.WriteAttributeString("type", "text");
                                    w.WriteAttributeString("value", String.Format("{0}", arg.DefaultValue));
                                }
                                w.WriteEndElement();

                                w.WriteString(arg.Required ? " * " : " - ");
                                w.WriteString(arg.Description.TrimEnd('.') + ".");

                                w.WriteEndElement();
                            }

                            w.WriteStartElement("li");
                            w.WriteAttributeString("style", "padding-top:10px;");
                            {
                                w.WriteStartElement("strong");
                                w.WriteAttributeString("style", "display:inline-block; width: 100px; text-align: right;");
                                w.WriteString(" ");
                                w.WriteEndElement();

                                w.WriteStartElement("input");
                                w.WriteAttributeString("id", command.DisplayName.ToLowerInvariant() + "Submit");
                                w.WriteAttributeString("name", command.DisplayName.ToLowerInvariant() + "Submit");
                                w.WriteAttributeString("type", "submit");
                                w.WriteAttributeString("value", command.DisplayName);
                                w.WriteEndElement();
                            }
                            w.WriteEndElement();
                            w.WriteEndElement();
                        }
                        w.WriteEndElement();
                    }
                    else
                    {
                        w.WriteElementString("h1", Constants.ProductName + " " + Constants.ProductVersion);
                        w.WriteElementString("p", System.Diagnostics.FileVersionInfo.GetVersionInfo(Constants.ProcessFile).Comments);

                        Dictionary<string, List<IDisplayInfo>> all = new Dictionary<string, List<IDisplayInfo>>(StringComparer.OrdinalIgnoreCase);
                        foreach (IDisplayInfo item in items)
                        {
                            if (item.ReflectedType == typeof(CommandInterpreter) || item.ReflectedType == typeof(BuiltInCommands.BuiltIn))
                                continue;
                            if (item.TryGetAttribute(out ignored))
                                continue;

                            List<IDisplayInfo> l;
                            if (!all.TryGetValue(item.Category, out l))
                                all.Add(item.Category, l = new List<IDisplayInfo>());
                            l.Add(item);
                        }

                        List<string> keys = new List<string>(all.Keys);
                        keys.Sort();
                        foreach (string key in keys)
                        {
                            List<IDisplayInfo> list = all[key];

                            w.WriteElementString("h2", key + ":");
                            foreach (IDisplayInfo info in list)
                            {
                                ICommand command = info as ICommand;
                                if (command == null || !info.Visible)
                                    continue;

                                w.WriteStartElement("li");
                                w.WriteStartElement("a");
                                w.WriteAttributeString("href", appRoot + command.DisplayName + '/');
                                w.WriteElementString("b", command.DisplayName);
                                w.WriteEndElement();
                                w.WriteString(" - ");
                                w.WriteString(info.Description.TrimEnd('.') + ".");
                                w.WriteEndElement();
                            }
                        }
                    }
                }
                foreach (AssemblyCopyrightAttribute copy in Constants.EntryAssembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), true))
                    w.WriteElementString("p", copy.Copyright);
                w.WriteEndElement();
                w.WriteEndElement();
                w.Flush();
            }
        }

        #region SwitchedOutputStream
        private class SwitchedOutputStream : Stream
        {
            private int _pos;
            private bool _outputSent;
            private byte[] _bytes;
            private Stream _output;
            public SwitchedOutputStream(Stream outputStream, int bufferSize)
            {
                _pos = 0;
                _bytes = new byte[bufferSize];
                _output = outputStream;
            }

            public bool OutputSent { get { return _outputSent; } }
            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override void Flush() { }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
            public int BufferPosition { get { return _pos; } }
            public override int Read(byte[] buffer, int offset, int count) { throw new InvalidOperationException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (count + _pos >= _bytes.Length)
                {
                    _outputSent = true;
                    _output.Write(_bytes, 0, Interlocked.Exchange(ref _pos, 0));
                }
                if (count + _pos >= _bytes.Length)
                {
                    _outputSent = true;
                    _output.Write(buffer, offset, count);
                }
                else
                {
                    Buffer.BlockCopy(buffer, offset, _bytes, _pos, count);
                    _pos += count;
                }
            }
            public override string ToString()
            {
                return Encoding.UTF8.GetString(_bytes, 0, _pos);
            }

            public void Commit()
            {
                _output.Write(_bytes, 0, _pos);
                _bytes = null;
                _output.Flush();
                _output.Dispose();
            }
        }
        #endregion
    }
}
