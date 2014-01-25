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
using System.Text.RegularExpressions;

namespace CSharpTest.Net.Generators.ResX
{
	class ResxGenArgParser : IEnumerable<ResxGenArgument>
	{
		//very incomplete; however, it should be close enough for 80%
		static readonly Regex ArgumentsMatch = new Regex(@"^\((\s*(?<type>(?:global::)?[^()<>\s,]+\s*(?:(?<targs><(?:[^<>]*<[^<>]*>)*[^<>]*>)|\s))\s*(?<name>[a-zA-Z_][\w_]*)\s*.*?[,)])*\)?$");

		private readonly List<ResxGenArgument> _args;

		public ResxGenArgParser(string rawArgs)
		{
			Match margs = ArgumentsMatch.Match(rawArgs);
			Group types = margs.Groups["type"];
			Group names = margs.Groups["name"];
			if(!margs.Success || types.Captures.Count != names.Captures.Count)
				throw new ApplicationException("Unable to parse argument list: " + rawArgs);

			_args = new List<ResxGenArgument>();
			for (int i = 0; i < types.Captures.Count; i++)
				_args.Add(new ResxGenArgument(types.Captures[i].Value.Trim(), names.Captures[i].Value.Trim()));
		}

		public IEnumerator<ResxGenArgument> GetEnumerator()
		{ return _args.GetEnumerator(); }

        [System.CodeDom.Compiler.GeneratedCodeAttribute("", "")]
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{ return GetEnumerator(); }
	}
}
