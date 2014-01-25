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

namespace CSharpTest.Net.Generators.ResX.Writers
{
    class ResxExceptionString : ResxString
    {
        public ResxExceptionString(ResxGenItem item)
            : base(item)
        {
            Check.Assert<ArgumentException>(item.IsException);
        }

        public IEnumerable<ResxGenArgument> PublicArgs
        {
            get
            {
                foreach (ResxGenArgument arg in Item.Args)
                    if (arg.IsPublic)
                        yield return arg;
            }
        }

        protected override string FormatterMethod(string className, string formatPrefix)
        {
            return String.Format("{0}.{1}SafeFormat", className, formatPrefix);
        }

        public void WriteCtor(CsWriter code, string fullClassName, string formatPrefix)
        {
            string formatNm = String.Format("{0}.{1}{2}", fullClassName, formatPrefix, Item.Identifier);
            string formatFn = FormatterMethod(fullClassName, formatPrefix);
            string baseArgs = Item.IsFormatter ? formatFn + "({0}, {1})" : "{0}";
            string argList = Item.HasArguments ? ", " + Item.Parameters(true) : "";
            string strHResult = Item.HResult != 0 ? String.Format("unchecked((int)0x{0:X8}U)", Item.HResult) : "-1";

            code.WriteSummaryXml(Item.Value);
            code.WriteLine("public {0}({1})", MemberName, Item.Parameters(true));
            using (code.WriteBlock("\t: this((System.Exception)null, {0}, {1})", strHResult, String.Format(baseArgs, formatNm, Item.Parameters(false))))
            {
                foreach (ResxGenArgument arg in Item.Args)
                    WriteSetProperty(code, arg.IsPublic, arg.Name, arg.ParamName);
                if (Item.AutoLog)
                    code.WriteLine("WriteEvent({0});", Item.Parameters(false));
            }
            code.WriteSummaryXml(Item.Value);
            code.WriteLine("public {0}({1}{2}System.Exception innerException)", MemberName, Item.Parameters(true), Item.HasArguments ? ", " : "");
            using (code.WriteBlock("\t: this(innerException, {0}, {1})", strHResult, String.Format(baseArgs, formatNm, Item.Parameters(false))))
            {
                foreach (ResxGenArgument arg in Item.Args)
                    WriteSetProperty(code, arg.IsPublic, arg.Name, arg.ParamName);
                if (Item.AutoLog)
                    code.WriteLine("WriteEvent({0});", Item.Parameters(false));
            }

            if (Item.AutoLog)
                WriteAutoLog(code, fullClassName, formatPrefix);

            code.WriteSummaryXml("if(condition == false) throws {0}", SummaryString);
            using (code.WriteBlock("public static void Assert(bool condition{0})", argList))
                code.WriteLine("if (!condition) throw new {0}({1});", MemberName, Item.Parameters(false));
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

        void WriteAutoLog(CsWriter code, string fullClassName, string formatPrefix)
        {
            code.WriteSummaryXml("Prepares the arguments as strings and writes the event");
            using (code.WriteBlock("private void WriteEvent({0})", Item.Parameters(true)))
            {
                base.WriteEvent(code, fullClassName, formatPrefix);
            }
        }
    }
}