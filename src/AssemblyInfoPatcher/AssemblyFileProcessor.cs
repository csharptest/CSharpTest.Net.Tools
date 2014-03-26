using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.AssemblyInfoPatcher
{
    class AssemblyFileProcessor
    {
        #region MatchAttribute Expression
        private static string MatchAttribute = @"
(?<=[^a-z,A-Z,0-9])
(?<FullName>(?<Attribute>[a-zA-Z][a-zA-Z0-9_]*?)(?:Attribute)?)
\s*\(\s*
(?<Content>
  (?<Value>[a-zA-Z](?:\.?[a-zA-Z0-9_]*)*)|
  (?<Quoted>""[^\""]*\"")
)
\s*\)";
        #endregion
        private readonly Regex _pattern;
        private readonly ArgumentList _args;
        private readonly bool _addMissing;
        private Dictionary<string, string> _variables;
        private Dictionary<string, string> _todo; 

        public AssemblyFileProcessor(ArgumentList args, bool addMissing)
        {
            _args = args;
            _addMissing = addMissing;
            _pattern = new Regex(MatchAttribute, RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);
            _variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _todo = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public void ProcessFile(FileInfo file)
        {
            _variables.Clear();
            _todo.Clear();

            if (_addMissing)
            {
                foreach (var item in _args)
                    _todo.Add(item.Name, item.Value);
            }

            var projFiles = new List<FileInfo>(file.Directory.GetFiles("*.csproj"));
            if (file.Directory.Parent != null)
                projFiles.AddRange(file.Directory.Parent.GetFiles("*.csproj"));
            if (projFiles.Count > 0)
                ReadProjectValues(projFiles[0]);

            bool detect;
            using (var io = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
            {
                int first = io.ReadByte();
                detect = first == 0 || first == 239 || first == 254 || first == 255 ||
                         (first == 43 && io.ReadByte() == 47 && io.ReadByte() == 118);
            }

            string text;
            Encoding encoding;
            using (var rdr = detect ? new StreamReader(file.FullName, true) : new StreamReader(file.FullName, Encoding.GetEncoding(CultureInfo.InstalledUICulture.TextInfo.OEMCodePage)))
            {
                encoding = rdr.CurrentEncoding;
                text = rdr.ReadToEnd();
            }

            string modified = _pattern.Replace(text, ReplaceValue);

            if (_todo.Count > 0)
            {
                string newText = AddMisingAttributes(modified.EndsWith(Environment.NewLine) ? modified.EndsWith(Environment.NewLine + Environment.NewLine) ? 0 : 1 : 2);
                modified += _pattern.Replace(newText, ReplaceValue);
            }

            if (text != modified)
            {
                if ((file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    file.Attributes = file.Attributes & ~FileAttributes.ReadOnly;

                using (var wtr = new StreamWriter(file.FullName, false, encoding))
                    wtr.Write(modified);
            }
        }

        private void ReadProjectValues(FileInfo projFile)
        {
            var doc = new XmlDocument();
            using(var rdr = new StreamReader(projFile.FullName))
                doc.Load(rdr);

            foreach (var node in doc.DocumentElement.ChildNodes)
            {
                var child = node as XmlElement;
                if (child != null && child.Name == "PropertyGroup" && !child.HasAttribute("Condition"))
                {
                    foreach (var pnode in child.ChildNodes)
                    {
                        var prop = pnode as XmlElement;
                        if (prop != null)
                            _variables[prop.Name] = prop.InnerText;
                    }
                }
            }
        }

        private string ReplaceValue(Match match)
        {
            string name = match.Groups["Attribute"].Value;
            string value;
            if (_args.TryGetValue(name, out value) || _args.TryGetValue(name + "Attribute", out value))
            {
                _todo.Remove(name);

                var quoted = match.Groups["Quoted"].Success;
                var contentGroup = match.Groups["Content"];

                var moffset = contentGroup.Index - match.Index;
                var replacement = match.Value.Substring(0, moffset);

                value = Environment.ExpandEnvironmentVariables(value);
                value = RegexPatterns.MakefileMacro.Replace(value, ReplaceVariable);

                // special case for GuidAttribute:
                if (StringComparer.Ordinal.Equals("Guid", name))
                {
                    value = new Guid(value).ToString("D");
                }

                if (quoted)
                    value = MakeString(value);
                replacement += value;
                
                replacement += match.Value.Substring(moffset + contentGroup.Length);
                return replacement;
            }

            return match.Value;
        }

        private string ReplaceVariable(Match m)
        {
            string value;
            string fld = m.Groups["field"].Value;
            if (!_args.TryGetValue(fld, out value))
            {
                if (!_variables.TryGetValue(fld, out value))
                {
                    if (null == (value = Environment.GetEnvironmentVariable(fld)))
                    {
                        Console.Error.WriteLine("Unknown variable {0}", m.Value);
                        value = m.Value;
                    }
                }
            }

            if (m.Groups["replace"].Success)
            {
                for (int i = 0; i < m.Groups["replace"].Captures.Count; i++)
                {
                    string replace = m.Groups["name"].Captures[i].Value;
                    string with = m.Groups["value"].Captures[i].Value;
                    value = value.Replace(replace, with);
                }
            }

            return value;
        }

        private static string MakeString(string data)
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
                sb.Append('x');
                sb.Append(((int)ch).ToString("x4"));
            }
            sb.Append('"');
            return sb.ToString();
        }

        private string AddMisingAttributes(int addnewline)
        {
            var sb = new StringBuilder();
            while (addnewline-- > 0)
                sb.AppendLine();

            sb.AppendLine("// Added by AssemblyInfoPatcher on " + DateTime.Now.ToShortDateString());
            foreach (var item in _todo)
            {
                string name = item.Key;
                string value = "xxx";

                bool typeFound = false;
                bool quote = false;
                if (name.EndsWith("Attribute") == false)
                    name += "Attribute";

                try
                {
                    var namespaces = new[]
                    {"System.Reflection", "System.Runtime.CompilerServices", "System.Runtime.InteropServices"};

                    foreach (var ns in namespaces)
                    {
                        Type t = Type.GetType(ns + '.' + name, false);
                        typeFound = (t != null);
                        if (typeFound)
                        {
                            name = t.FullName;
                            foreach (var ctr in t.GetConstructors())
                            {
                                var param = ctr.GetParameters();
                                if (param.Length == 1 && param[0].ParameterType == typeof (String))
                                    quote = true;
                            }
                            break;
                        }
                    }
                }
                catch { }

                if (!typeFound || quote)
                    value = "\"\"";

                sb.AppendFormat("[assembly: {0}({1})]", name, value);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}