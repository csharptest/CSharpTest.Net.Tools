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
using System.Xml.Serialization;
using System.ComponentModel;

namespace CSharpTest.Net.CustomTool.XmlConfig
{
	/// <summary> A single file pattern match </summary>
	[XmlRoot("match")]
	public sealed class FileMatch
	{
	    private bool _stop;
	    private string _fileSpec;
		MatchAppliesTo[] _applyTo;
        GeneratorConfig[] _generators;

        /// <summary> stop crawling the directory tree for configuration files </summary>
        [XmlAttribute("stop"), DefaultValue(false)]
        public bool StopHere
        {
            get { return _stop; }
            set { _stop = value; }
        }

	    /// <summary> for any file that matches this filespec </summary>
	    [XmlAttribute("filespec")]
	    public string FileSpec
	    {
            get { return String.IsNullOrEmpty(_fileSpec) ? "*" : _fileSpec; }
            set { _fileSpec = value; }
	    }

		/// <summary> and is contained in any one of these directories, optional and defaults to '.' </summary>
		[XmlElement("applies-to")]
		public MatchAppliesTo[] AppliesTo
		{
			get { return _applyTo ?? new MatchAppliesTo[0]; }
			set { _applyTo = value; }
		}

		/// <summary> generator definitions </summary>
		[XmlElement("generator")]
		public GeneratorConfig[] Generators
		{
			get { return _generators ?? new GeneratorConfig[0]; }
			set { _generators = value; }
		}
	}
}
