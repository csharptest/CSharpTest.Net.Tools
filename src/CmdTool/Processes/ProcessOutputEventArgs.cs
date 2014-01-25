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

namespace CSharpTest.Net.Processes
{
	/// <summary> A delegate that handles the write to either std::out or std::in for a process </summary>
	public delegate void ProcessOutputEventHandler(object sender, ProcessOutputEventArgs args);
	/// <summary> 
	/// The event args that contains information about the line of text written to either
	/// std::out or std::in on the created process. 
	/// </summary>
	public sealed class ProcessOutputEventArgs : EventArgs
	{
		private readonly bool _isError;
		private readonly string _data;

		internal ProcessOutputEventArgs(string output, bool iserror)
		{
			_data = output;
			_isError = iserror;
		}

		/// <summary> Returns the line of text written to standard out/error  </summary>
		public String Data { get { return _data; } }
		/// <summary> Returns true if the line of text was written to std::error </summary>
		public bool Error { get { return _isError; } }
	}
}
