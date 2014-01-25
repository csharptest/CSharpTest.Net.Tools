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
using System.Xml.Schema;
using System.Text;
using CSharpTest.Net.IO;

namespace CSharpTest.Net.Html
{
    [AttributeUsage(AttributeTargets.Field)]
    class DOCTYPEAttribute : Attribute
    {
        public DOCTYPEAttribute(string resource) : this(resource, String.Empty, String.Empty) { }
        public DOCTYPEAttribute(string resource, string Public, string System)
        { RESOURCE = resource; PUBLIC = Public; SYSTEM = System; }
        public readonly String RESOURCE;
        public String PUBLIC;
        public String SYSTEM;
        public XhtmlDTDSpecification DTD;
    }

    /// <summary>
    /// Defines the required DTD specification
    /// </summary>
    public enum XhtmlDTDSpecification 
    {
        /// <summary> Use DTD only if defined </summary>
        None,
        /// <summary> 
        /// Use the XHTML 1.0 Transitional DTD 
        /// &lt;!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"&gt;
        /// </summary>
        [DOCTYPE("Xhtml1_0.xhtml1-strict.dtd", PUBLIC = "-/W3C/DTD XHTML 1.0 Strict/EN", SYSTEM = "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd")]
        XhtmlStrict_10,
        /// <summary> 
        /// Use the XHTML 1.0 Transitional DTD 
        /// &lt;!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"&gt;
        /// </summary>
        [DOCTYPE("Xhtml1_0.xhtml1-transitional.dtd", PUBLIC = "-/W3C/DTD XHTML 1.0 Transitional/EN", SYSTEM = "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd")]
        XhtmlTransitional_10,
        /// <summary> 
        /// Use the XHTML 1.0 Transitional DTD 
        /// &lt;!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Frameset//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-frameset.dtd"&gt;
        /// </summary>
        [DOCTYPE("Xhtml1_0.xhtml1-frameset.dtd", PUBLIC = "-/W3C/DTD XHTML 1.0 Frameset/EN", SYSTEM = "http://www.w3.org/TR/xhtml1/DTD/xhtml1-frameset.dtd")]
        XhtmlFrameset_10,

        /// <summary> 
        /// Allow any of the supported DTDs, but must be declared and compliant 
        /// </summary>
        Any
    }

    /// <summary>
    /// Provides validation of Xhtml documents based on w3c DTDs
    /// </summary>
    public class XhtmlValidation
    {
        readonly XmlNameTable _nameTable;
        readonly XmlNamespaceManager _namespaces;
        readonly XhtmlDTDSpecification _requiresDtd;

        /// <summary> Creates a validator that requires documents to use any of the three DTD specifications </summary>
        public XhtmlValidation()
            : this(XhtmlDTDSpecification.Any) { }

        /// <summary> Creates a validator that requires documents to use the specified DTD </summary>
        public XhtmlValidation(XhtmlDTDSpecification dtdRequired)
        {
            _requiresDtd = dtdRequired;
            _nameTable = new NameTable();
            _namespaces = new XmlNamespaceManager(_nameTable);
            _namespaces.AddNamespace("htm", "http://www.w3.org/1999/xhtml");
        }

        /// <summary> Validate the input textreader </summary>
        public void Validate(string originalFilename, TextReader reader)
        {
            ValidateDocument(_requiresDtd, originalFilename, reader.ReadToEnd());
        }

        /// <summary> Validate the input textreader </summary>
        public void Validate(TextReader reader)
        {
            using (TempFile temp = new TempFile())
            {
                string content = reader.ReadToEnd();
                temp.WriteAllText(content);
                ValidateDocument(_requiresDtd, temp.TempPath, content);
            }
        }

        /// <summary> Validate the input filename </summary>
        public void Validate(string filename)
        {
            ValidateDocument(_requiresDtd, filename, File.ReadAllText(filename));
        }

        XmlReaderSettings MakeReaderSettings(System.Xml.XmlResolver resolver, ValidationEventHandler errorHandler)
        {
            XmlReaderSettings settings = null;
            settings = new XmlReaderSettings();
            settings.CheckCharacters = true;
            settings.ConformanceLevel = ConformanceLevel.Document;
            settings.IgnoreComments = true;
            settings.NameTable = _nameTable;
            settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;
#pragma warning disable 612, 618
            settings.ValidationType = _requiresDtd == XhtmlDTDSpecification.None ? ValidationType.Auto : ValidationType.DTD;
#pragma warning restore 612, 618
            settings.ValidationEventHandler += errorHandler;
#if NET20 || NET35
            settings.ProhibitDtd = false;
#else
            settings.DtdProcessing = DtdProcessing.Parse;
#endif
            settings.XmlResolver = resolver;
            return settings;
        }

        void ValidateDocument(XhtmlDTDSpecification expect, string filename, string contents)
        {
            XmlResolver resolver = new XmlResolver();
            resolver.Credentials = null;
            ValidationErrorsList errors = new ValidationErrorsList(filename, contents);

            using (XmlReader reader = XmlReader.Create(new StringReader(contents), MakeReaderSettings(resolver, errors.OnValidationError)))
            {
                try
                {
                    reader.MoveToContent();

                    if (expect != XhtmlDTDSpecification.None)
                    {
                        if (expect != XhtmlDTDSpecification.Any && expect != resolver.DTDSpecification)
                            throw new XmlException("Missing required DTD specification.", null, 1, 1);

                        if (reader.LocalName != "html")
                            throw new XmlException(String.Format("Unexpected root element: {0}", reader.LocalName));
                        if (reader.NamespaceURI != _namespaces.LookupNamespace("htm"))
                            throw new XmlException(String.Format("Unexpected root namespace: {0}", reader.NamespaceURI));
                    }
                    while (reader.Read())
                    { }
                }
                catch (XmlException xmlEx)
                {
                    errors.OnXmlException(xmlEx);
                }
            }

            errors.Assert();
        }

        // <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Frameset//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-frameset.dtd">
        // <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
        // <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd">
        class XmlResolver : System.Xml.XmlResolver
        {
            static DOCTYPEAttribute[] _docTypes;
            static XmlResolver()
            {
                List<DOCTYPEAttribute> docTypes = new List<DOCTYPEAttribute>();
                foreach (System.Reflection.FieldInfo f in typeof(XhtmlDTDSpecification).GetFields())
                foreach (DOCTYPEAttribute attr in f.GetCustomAttributes(typeof(DOCTYPEAttribute), false))
                {
                    attr.DTD = (XhtmlDTDSpecification)Enum.Parse(typeof(XhtmlDTDSpecification), f.Name);
                    docTypes.Add(attr);
                }
                _docTypes = docTypes.ToArray();
            }

            public XhtmlDTDSpecification DTDSpecification = XhtmlDTDSpecification.None;

            public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
            {
                Uri cwd = new Uri(Environment.CurrentDirectory);
                string loc = absoluteUri.ToString();
                if (loc.StartsWith(cwd.AbsoluteUri))
                    loc = loc.Substring(cwd.AbsoluteUri.Length).TrimStart('/');

                foreach (DOCTYPEAttribute attr in _docTypes)
                {
                    if (attr.PUBLIC == loc || attr.SYSTEM == loc)
                    {
                        this.DTDSpecification = attr.DTD;
                        return Check.NotNull(GetType().Assembly.GetManifestResourceStream(GetType(), attr.RESOURCE));
                    }
                }

                if (this.DTDSpecification == XhtmlDTDSpecification.XhtmlStrict_10 ||
                    this.DTDSpecification == XhtmlDTDSpecification.XhtmlFrameset_10 ||
                    this.DTDSpecification == XhtmlDTDSpecification.XhtmlTransitional_10)
                {
                    if (loc.EndsWith("/xhtml-lat1.ent"))
                        return Check.NotNull(GetType().Assembly.GetManifestResourceStream(GetType(), "Xhtml1_0.xhtml-lat1.ent"));
                    else if (loc.EndsWith("/xhtml-special.ent"))
                        return Check.NotNull(GetType().Assembly.GetManifestResourceStream(GetType(), "Xhtml1_0.xhtml-special.ent"));
                    else if (loc.EndsWith("/xhtml-symbol.ent"))
                        return Check.NotNull(GetType().Assembly.GetManifestResourceStream(GetType(), "Xhtml1_0.xhtml-symbol.ent"));
                }

                return null;
            }
            public override System.Net.ICredentials Credentials
            { set { } }
        }

        class ValidationErrorsList : List<String>
        {
            readonly string _filename;
            readonly string[] _lines;
            XmlException _first;

            public ValidationErrorsList(string filename, string contents)
            {
                _first = null;
                _filename = filename;
                _lines = contents.Split('\n');
            }

            public void Assert()
            {
                if (_first != null)
                {
                    string msg = String.Join(Environment.NewLine, this.ToArray());
                    throw new XmlException(msg, _first, _first.LineNumber, _first.LinePosition);
                }
            }

            public void OnXmlException(XmlException e)
            {
                _first = _first ?? e;
                HandleError(e.LineNumber, e.LinePosition, XmlSeverityType.Error, e.Message);
            }

            public void OnValidationError(object sender, ValidationEventArgs e)
            {
                if (e.Exception != null)
                {
                    _first = _first ?? new XmlException(e.Exception.Message, e.Exception, e.Exception.LineNumber, e.Exception.LinePosition);
                    HandleError(e.Exception.LineNumber, e.Exception.LinePosition, e.Severity, e.Message);
                }
            }

            private void HandleError(int line, int pos, XmlSeverityType severity, string message)
            {
                StringBuilder errorText = new StringBuilder();
                errorText.AppendFormat("{0}({1},{2}): {3}: {4}", _filename, line, pos, severity.ToString().ToLower(), message);
                System.Diagnostics.Trace.WriteLine(errorText.ToString());

                if (line > 0 && line <= _lines.Length)
                {
                    for (int ix = Math.Max(0, line - 3); ix < Math.Min(_lines.Length, line + 2); ix++)
                        System.Diagnostics.Trace.WriteLine((ix == line ? "! " : "  ") + _lines[ix]);

                    string lineText = _lines[line - 1].TrimEnd();
                    if (pos > 0 && pos < lineText.Length)
                    {
                        if (pos > 1 && lineText[pos - 2] == '<')
                            pos--;
                        lineText = lineText.Substring(pos - 1);
                    }
                    if (lineText.Length > 0 && lineText[0] == '<')
                    {
                        int ixEnd = lineText.IndexOf('>');
                        if (ixEnd > 1 && ixEnd < lineText.Length)
                            lineText = lineText.Substring(0, ixEnd + 1);
                    }
                    else
                        lineText = lineText.Substring(0, Math.Min(lineText.Length, 120));
                    if (!String.IsNullOrEmpty(lineText))
                    {
                        errorText.AppendLine();
                        errorText.Append('\t');
                        errorText.AppendLine(lineText);
                    }
                }
                base.Add(errorText.ToString());
            }
        }
    }
}
