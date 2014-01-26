#region Copyright 2010-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Resources;
using System.Resources.Tools;
using System.Text;
using System.Text.RegularExpressions;
using CSharpTest.Net.Utils;
using Microsoft.CSharp;

namespace CSharpTest.Net.Generators.ResX
{
	class ResxGenItem
	{
        private static readonly CSharpCodeProvider Csharp = new CSharpCodeProvider();
        static readonly System.Reflection.AssemblyName[] AllowedNames = new System.Reflection.AssemblyName[] { };
		static readonly Regex ExceptionMatch = new Regex(@"Exception(\(|$)");
        static readonly Regex OptionMatch = new Regex(@"^#\s*(?:(?<name>[a-zA-Z_]\w*)\s*=\s*(?<value>\w+)\s*)(?:,\s*(?<name>[a-zA-Z_]\w*)\s*=\s*(?<value>\w+)\s*)*$");
		static readonly Regex FormatingMatch = RegexPatterns.FormatSpecifier;
        const uint HResultBitCustom = 0x20000000;
        const uint HResultBitError = 0x80000000;

		public readonly List<ResxGenArgument> Args;
        public readonly bool IsFormatter;
        public readonly bool HasArguments;
        public readonly bool IsException;

		public readonly string Identifier;

		public readonly string FullName;
        public readonly string ItemName;
        public readonly string MemberName;
        public readonly string Comments;
        public readonly Dictionary<string, string> Options;
		public readonly string Value;
		public readonly bool Ignored;
        public readonly ResXDataNode Node;

        public readonly bool AutoLog;
        public readonly int FacilityId;
        public readonly uint HResult;
        public readonly uint MessageId;

        public ResxGenItem(ResXOptions options, ResXDataNode node)
		{
            Node = node;
			Ignored = true;//and clear upon complete...
            
            Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Args = new List<ResxGenArgument>();
			try 
			{
				if (node.FileRef != null) return;
				Type type = Type.GetType(node.GetValueTypeName(AllowedNames));
				if (type == null || type != typeof(String))
					return;
				Value = (String)node.GetValue(AllowedNames);
			}
			catch { return; }

            MemberName = Identifier = StronglyTypedResourceBuilder.VerifyResourceName(node.Name, Csharp);
			FullName = ItemName = node.Name;

			Comments = node.Comment;
			string rawArgs = null;

			IsFormatter = FormatingMatch.IsMatch(Value);
			IsException = ExceptionMatch.IsMatch(node.Name);
            //if (!IsFormatter && !IsException)
            //    return;

			int pos;
			if ((pos = ItemName.IndexOf('(')) > 0)
			{
				rawArgs = ItemName.Substring(pos);
				ItemName = ItemName.Substring(0, pos);
                MemberName = StronglyTypedResourceBuilder.VerifyResourceName(ItemName, Csharp);
            }
			else if (Comments.StartsWith("(") && (pos = Comments.IndexOf(')')) > 0)
			{
				rawArgs = Comments.Substring(0, 1 + pos);
				Comments = Comments.Substring(pos + 1).Trim();
			}
			if (!String.IsNullOrEmpty(rawArgs))
				Args.AddRange(new ResxGenArgParser(rawArgs));

            //now thats out of the way... let's transform the format string into something usable:
            Value = FormatingMatch.Replace(Value,
                delegate(Match m)
                {
                    return "{" + GetArg(null, m.Groups["field"].Value) + m.Groups["suffix"].Value + "}";
                }
            );

            if (Comments.StartsWith(":") && Comments.IndexOf("Exception") > 0)
                IsException = true;

            bool parsedOptions = ParseOptions(ref Comments);

            FacilityId = options.FacilityId;
            bool hasId = GetMessageIdForItem(out MessageId);
            hasId |= GetHResultForItem(out HResult);

		    HasArguments = Args.Count > 0;
            if (HasArguments || IsFormatter || IsException || hasId)
            {
                Ignored = false;
                if (!parsedOptions)
                    throw new ApplicationException(String.Format("Unable to parse comment options: '{0}'", Comments));
            }
            AutoLog = hasId && MessageId != 0 && options.AutoLog && GetOption("log", true);
        }

        public bool Hidden { get { return !Ignored; } }

        private int GetArg(string type, string name)
        {
            type = String.IsNullOrEmpty(type) ? "object" : type;

            int ordinal;
            if (int.TryParse(name, out ordinal))
            {
                for (int add = Args.Count; add < ordinal; add++)
                    Args.Add(new ResxGenArgument("object", "_" + add));
                if (Args.Count == ordinal)
                    Args.Add(new ResxGenArgument(type, "_" + ordinal));
                return ordinal;
            }

            for (int i = 0; i < Args.Count; i++)
                if (Args[i].Name == name) return i;

            Args.Add(new ResxGenArgument(type, name));
            return Args.Count - 1;
        }

        //Comment options formed via "# Name1 = Value1 , name2 = value2" spacing optional but names and values must be alpha-numeric
        private bool ParseOptions(ref string rawComments)
        {
            int pos = Comments.IndexOf('#');
            if (pos < 0) return true;

            string optionText = rawComments.Substring(pos);
            rawComments = rawComments.Substring(0, pos).TrimEnd();
            Match optionMatch = OptionMatch.Match(optionText);
            if (!optionMatch.Success)
                return false;

            Group names = optionMatch.Groups["name"];
            Group values = optionMatch.Groups["value"];

            for (int i = 0; i < names.Captures.Count; i++)
                Options.Add(names.Captures[i].Value, values.Captures[i].Value);
            return true;
        }

        private bool GetHResultForItem(out uint hResult)
        {
            hResult = 0;
            string id;
            if (!TryGetOption("ErrorId", out id) && !TryGetOption("MessageId", out id))
                return false;

            hResult = StringIdToResult(id);
            hResult |= HResultBitCustom;
            if (IsException)
                hResult |= HResultBitError;
            return true;
        }

        private bool GetMessageIdForItem(out uint hResult)
        {
            hResult = 0;
            string id;
            if (!TryGetOption("MessageId", out id))
                return false;

            hResult = StringIdToResult(id);
            return true;
        }

        private uint StringIdToResult(string id)
        {
            //   3 3 2 2 2 2 2 2 2 2 2 2 1 1 1 1 1 1 1 1 1 1
            //   1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0
            //  +---+-+-+-+---------------------+-------------------------------+
            //  |Sev|C|N|R|      Facility       |               Code            |
            //  +---+-+-+-+---------------------+-------------------------------+
            uint errorNo = uint.Parse(id);
            uint severityLevel = 3;
            string severity = GetOption("Severity", GetOption("Level", IsException ? "Error" : "Info"));
            if (severity.StartsWith("warn", StringComparison.OrdinalIgnoreCase))
                severityLevel = 2;
            else if (severity.StartsWith("info", StringComparison.OrdinalIgnoreCase))
                severityLevel = 1;
            else if (!severity.StartsWith("err", StringComparison.OrdinalIgnoreCase))
                throw new ApplicationException(String.Format("Unknown severity: {0}", severity));

            uint hResult = (severityLevel << 30) & 0xC0000000;
            if (FacilityId > 0)
                hResult |= ((uint)(FacilityId & 0x07FF)) << 16;
            hResult |= errorNo & 0x0FFFF;
            return hResult;
        }

        public bool TryGetOption<T>(string name, out T value)
        {
            string text;
            if (Options.TryGetValue(name, out text))
            {
                value = (T)Convert.ChangeType(text, typeof(T));
                return true;
            }
            value = default(T);
            return false;
        }

        public T GetOption<T>(string name, T defaultValue)
        {
            T value;
            if (TryGetOption(name, out value))
                return value;
            return defaultValue;
        }

	    public string Parameters(bool includeTypes)
		{
			StringBuilder sbArgs = new StringBuilder();
			int count = 0;
			foreach (ResxGenArgument kv in Args)
			{
				if (count++ > 0) sbArgs.Append(", ");
				if (includeTypes) sbArgs.AppendFormat("{0} ", kv.Type);
				sbArgs.Append(kv.ParamName);
			}
			return sbArgs.ToString();
		}

	}
}