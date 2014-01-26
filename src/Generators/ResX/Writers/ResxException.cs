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

namespace CSharpTest.Net.Generators.ResX.Writers
{
    class ResxException
    {
        List<ResxExceptionString> _items;
        readonly string _memberName;
        readonly string _baseException;
        readonly uint _hresult;

        public ResxException(ResxGenItem item)
        {
            _items = new List<ResxExceptionString>();
            _items.Add(new ResxExceptionString(item));
            _memberName = item.MemberName;
            _hresult = item.HResult;
            _baseException = item.Comments.StartsWith(":") ? item.Comments : null;
        }

        public uint HResult { get { return _hresult; } }
        public string MemberName { get { return _memberName; } }

        public void AddOverload(ResxGenItem item)
        {
            Check.Assert<InvalidOperationException>(_hresult == item.HResult, "The hresult must be the same for " + MemberName);
            Check.Assert<ArgumentException>(item.MemberName == _memberName);
            _items.Add(new ResxExceptionString(item));
        }

        public bool Test(Action<string> error)
        {
            bool success = true;
            foreach (ResxExceptionString exs in _items)
                success = exs.Test(error) && success;
            return success;
        }
        
        public void WriteFormatString(CsWriter code)
        {
            foreach (ResxExceptionString exs in _items)
                exs.WriteFormatString(code);
        }

        public void WriteException(CsWriter code, string fullClassName, string formatPrefix, string baseException, bool isSealed, bool isPartial)
        {
            string baseName = _baseException ?? ": " + baseException.TrimStart(':', ' ');

            code.WriteSummaryXml("Exception class: {0}\r\n{1}", MemberName, _items[0].SummaryString);
            code.WriteLine("[System.SerializableAttribute()]");
            using (code.WriteClass("public {2}{3}class {0} {1}", MemberName, baseName, 
                                isSealed ? "sealed " : "",
                                isPartial ? "partial " : ""))
            {
                code.WriteSummaryXml("Serialization constructor");
                code.WriteBlock(
                    "{1} {0}(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)",
                    MemberName, isSealed ? "internal" : "protected")
                    .Dispose();

                WriteDefaultCtor(code, fullClassName, formatPrefix, isSealed);
                WritePublicProperties(code);

                foreach (ResxExceptionString item in _items)
                    item.WriteCtor(code, fullClassName, formatPrefix);
            }
        }

        private void WritePublicProperties(CsWriter code)
        {
            Dictionary<string, ResxGenArgument> publicData = new Dictionary<string, ResxGenArgument>();
            foreach (ResxExceptionString item in _items)
                foreach (ResxGenArgument arg in item.PublicArgs)
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
            if(publicData.Count > 0)
                code.WriteLine();
        }

        void WriteDefaultCtor(CsWriter code, string fullClassName, string formatPrefix, bool isSealed)
        {
            code.WriteSummaryXml("Used to create this exception from an hresult and message bypassing the message formatting");
            using (code.WriteBlock("internal static System.Exception Create(int hResult, string message)"))
                code.WriteLine("return new {0}((System.Exception)null, hResult, message);", MemberName);

            code.WriteSummaryXml("Constructs the exception from an hresult and message bypassing the message formatting");
            using (code.WriteBlock("{0} {1}(System.Exception innerException, int hResult, string message) : base(message, innerException)",
                isSealed ? "private" : "protected", MemberName))
            {
                code.WriteLine("base.HResult = hResult;");
                code.WriteLine("base.HelpLink = {0}.{1}HelpLinkFormat(HResult, GetType().FullName);", fullClassName, formatPrefix);
            }
        }
    }
}
