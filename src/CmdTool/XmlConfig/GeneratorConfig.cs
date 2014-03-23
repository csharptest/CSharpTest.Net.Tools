#region Copyright 2009 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.ComponentModel;
using System.Xml.Serialization;

namespace CSharpTest.Net.CustomTool.XmlConfig
{
	/// <summary> Defines a generator exe file to run with the input source file </summary>
	[XmlRoot("generator")]
	public sealed class GeneratorConfig
	{
        private bool _debug;
		private string _basePath;
		GeneratorInput _standardOut;
        GeneratorOutput[] _output;
		GeneratorArgument[] _arguments;

		[XmlIgnore]
		internal string BaseDirectory { get { return _basePath; } set { _basePath = value; } }

        [XmlAttribute("debug")]
        public bool Debug { get { return Config.VERBOSE || _debug; } set { _debug = value; } }

	    [XmlAttribute("input-encoding"), DefaultValue(FileEncoding.Default)]
	    public FileEncoding InputEncoding { get; set; }

        [XmlAttribute("output-encoding"), DefaultValue(FileEncoding.Default)]
        public FileEncoding OutputEncoding { get; set; }

	    /// <summary> The format of the execute command-line, expanding environment variable with %var% </summary>
		[XmlElement("script", typeof(GeneratorScript))]
		[XmlElement("execute", typeof(GeneratorExecute))]
		[XmlElement("assembly", typeof(AssemblyExecute))]
		public GeneratorScript Script;

	    /// <summary> Additional arguments for the process </summary>
	    [XmlElement("arg")]
		public GeneratorArgument[] Arguments
	    {
			get { return _arguments ?? new GeneratorArgument[0]; }
            set { _arguments = value; }
	    }

		/// <summary> Saves the program's standard output to a file with the provided extension </summary>
		[XmlElement("std-input")]
		public GeneratorInput StandardInput
		{
			get { return _standardOut ?? new GeneratorInput(); }
			set { _standardOut = value; }
		}

		/// <summary> Saves the program's standard output to a file with the provided extension </summary>
		[XmlElement("std-output")]
		public GeneratorOutput StandardOut;

	    /// <summary> Optionally look to see if a file with this extension was generated and add it to the project </summary>
	    [XmlElement("output")]
	    public GeneratorOutput[] Output
	    {
            get { return _output ?? new GeneratorOutput[0]; }
            set { _output = value; }
	    }
	}

    public enum FileEncoding
    {
        [XmlEnum("default")]
        Default,
        [XmlEnum("ascii")]
        Ascii,
        [XmlEnum("utf-8")]
        Utf8,
    }

    public class GeneratorArgument
	{
		[XmlAttribute("value")]
		public string Text;
	}

	public class GeneratorInput
	{
		[XmlAttribute("redirect")]
		public bool Redirect;
	}
}
