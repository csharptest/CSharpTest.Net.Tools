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
using System.ComponentModel;
using System.Xml.Serialization;
using CSharpTest.Net.Processes;

namespace CSharpTest.Net.CustomTool.XmlConfig
{
    /// <summary> Defines an executable script </summary>
    public class GeneratorScript
    {
		protected bool _invoke = false;
        private string _text;

		[XmlIgnore]
		public bool InvokeAssembly { get { return _invoke; } }

			/// <summary> The type of the script content </summary>
        [XmlAttribute("type")] 
        public ScriptEngine.Language Type;

		/// <summary> Includes a script file by prepending it's contents to the enclosed script block </summary>
        [XmlAttribute("src")]
    	public string Include;

        /// <summary> The script content </summary>
        [XmlText]
        public string Text
        {
            get { return _text ?? string.Empty; }
            set { _text = value; }
        }
    }

	/// <summary> Defines an executable script </summary>
	public class GeneratorExecute : GeneratorScript
	{
		public GeneratorExecute()
		{
			Type = ScriptEngine.Language.Exe;
		}

		/// <summary> The type of the script content </summary>
		[XmlAttribute("exe")]
		public string Exe
		{
			get { return Text; }
			set { Text = (value ?? string.Empty).Trim(); }
		}
	}

	/// <summary> Defines an executable script </summary>
	public class AssemblyExecute : GeneratorScript
	{
		public AssemblyExecute()
		{
			Type = ScriptEngine.Language.Exe;
			_invoke = true;
		}

		/// <summary> The type of the script content </summary>
		[XmlAttribute("exe")]
		public string Exe
		{
			get { return Text; }
			set { Text = (value ?? string.Empty).Trim(); }
		}
	}
}