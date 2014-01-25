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
using System.IO;

namespace CSharpTest.Net.Processes
{
    /// <summary>
    /// The common interface between spawning processes, and spawning scripts.
    /// </summary>
    public interface IRunner : IDisposable
    {
        /// <summary> Notifies caller of writes to the std::err or std::out </summary>
        event ProcessOutputEventHandler OutputReceived;

        /// <summary> Notifies caller when the process exits </summary>
        event ProcessExitedEventHandler ProcessExited;

		/// <summary> Allows writes to the std::in for the process </summary>
		System.IO.TextWriter StandardInput { get; }

        /// <summary> Waits for the process to exit and returns the exit code </summary>
        int ExitCode { get; }

        /// <summary> Returns true if this instance is running a process </summary>
        bool IsRunning { get; }

        /// <summary> Kills the process if it is still running </summary>
        void Kill();

        /// <summary> Closes std::in and waits for the process to exit </summary>
        void WaitForExit();

        /// <summary> Closes std::in and waits for the process to exit, returns false if the process did not exit in the time given </summary>
        bool WaitForExit(TimeSpan timeout);

		/// <summary> Gets or sets the initial working directory for the process. </summary>
		string WorkingDirectory { get; set; }

        /// <summary> Runs the process and returns the exit code. </summary>
        int Run();

        /// <summary> Runs the process and returns the exit code. </summary>
		int Run(params string[] args);

		/// <summary> Runs the process and returns the exit code. </summary>
		int Run(TextReader input, params string[] arguments);

        /// <summary> Starts the process and returns. </summary>
        void Start();

        /// <summary> Starts the process and returns. </summary>
		void Start(params string[] args);
    }
}