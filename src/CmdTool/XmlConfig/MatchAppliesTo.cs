#region Copyright 2009-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Xml.Serialization;

namespace CSharpTest.Net.CustomTool.XmlConfig
{
	/// <summary> Specifies one or more directies where this match applies </summary>
	[XmlRoot("applies-to")]
	public sealed class MatchAppliesTo
	{
		/// <summary> the relative (to config file) or abs path the file must be contained within </summary>
		[XmlAttribute("folder")]
		public string FolderPath;
	}
}
