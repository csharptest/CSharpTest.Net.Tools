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

namespace CSharpTest.Net.Processes
{
	/// <summary>
	/// Raised when a process started with the ProcessRunner exits
	/// </summary>
	public delegate void ProcessExitedEventHandler(object sender, ProcessExitedEventArgs args);

	/// <summary>
	/// Carries the exit code of the exited process.
	/// </summary>
	public class ProcessExitedEventArgs : EventArgs
	{
		readonly int _exitCode;
		internal ProcessExitedEventArgs(int exitCode)
		{
			_exitCode = exitCode;
		}

		/// <summary>
		/// Returns the environment exit code of the process
		/// </summary>
		public int ExitCode { get { return _exitCode; } }
	}
}
