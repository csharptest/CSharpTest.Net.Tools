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

namespace CSharpTest.Net.Generators.ResX
{
	class ResxGenArgument
	{
		public ResxGenArgument(string type, string name)
		{
			Type = String.IsNullOrEmpty(type) ? "object" : type.Trim();
			Name = name.Trim();
		}

		public string Type;
		public string Name;

		public bool IsPublic { get { return Char.IsUpper(Name[0]); } }
		public string ParamName { get { return String.Format("{0}{1}", Char.ToLower(Name[0]), Name.Substring(1)); } }
		
        // no longer used... Exception fields are placed inside of Exception.Data[] dictionary
        //public string FieldName { get { return String.Format("_{0}{1}", Char.ToLower(Name[0]), Name.Substring(1)); } }
	}
}