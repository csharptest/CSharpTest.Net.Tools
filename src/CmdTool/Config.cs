#region Copyright 2009-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using CSharpTest.Net.Utils;
using CSharpTest.Net.CustomTool.XmlConfig;

namespace CSharpTest.Net.CustomTool
{
	class Config : XmlConfiguration<CmdToolConfig>
	{
		public const string SCHEMA_NAME = "CmdTool.xsd";
		public Config() : base(SCHEMA_NAME) { }

        public static bool VERBOSE = false;
	}
}
