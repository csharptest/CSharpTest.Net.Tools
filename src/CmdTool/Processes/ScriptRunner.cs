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
using System.IO;
using CSharpTest.Net.Commands;
using CSharpTest.Net.IO;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Processes
{
    /// <summary>
    /// Defines a way to run scripts as an external process and capture their output.
    /// </summary>
    public class ScriptRunner : IRunner, IDisposable
    {
        private readonly ProcessRunner _runner;
        private readonly ScriptEngine _engine;
        private readonly TempFile _scriptFile;
		private readonly string[] _arguments;

        /// <summary>
        /// Creates a runnable script with the specified language
        /// </summary>
        public ScriptRunner(ScriptEngine.Language language, string script)
            : this(ScriptEngine.GetDefaults(language), script)
        { }

        /// <summary>
        /// Creates a runnable script with the specified engine parameters
        /// </summary>
        public ScriptRunner(ScriptEngine engine, string script)
        {
			_engine = engine;
			_scriptFile = _engine.Compile(script);

			_arguments = ArgumentList.Parse(engine.ArgumentFormat.Replace("{SCRIPT}", _scriptFile.TempPath));
			_runner = new ProcessRunner(engine.Executable, _arguments);
        }

		/// <summary> Return teh script engine being used </summary>
		public ScriptEngine ScriptEngine { get { return _engine; } }
		/// <summary> Return teh arguments to pass to script engine exe </summary>
		public string[] ScriptArguments { get { return _arguments; } }
		/// <summary> Returns the temp file of the script </summary>
		public string ScriptFile { get { return _scriptFile.TempPath; } }

			/// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() 
        {
            _scriptFile.Dispose();
        }

    	/// <summary>
    	/// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
    	/// </summary>
    	public override string ToString()
		{
    		return _runner.ToString();
		}

        /// <summary> Notifies caller of writes to the std::err or std::out </summary>
        public event ProcessOutputEventHandler OutputReceived
        {
            add { _runner.OutputReceived += value; }
            remove { _runner.OutputReceived -= value; }
        }

        /// <summary> Notifies caller when the process exits </summary>
        public event ProcessExitedEventHandler ProcessExited
        {
            add { _runner.ProcessExited += value; }
            remove { _runner.ProcessExited -= value; }
        }

		/// <summary> Allows writes to the std::in for the process </summary>
		public System.IO.TextWriter StandardInput { get { return _runner.StandardInput; } }

        /// <summary> Waits for the process to exit and returns the exit code </summary>
        public int ExitCode
        {
            get { return _runner.ExitCode; }
        }

		/// <summary> Gets or sets the initial working directory for the process. </summary>
		public string WorkingDirectory { get { return _runner.WorkingDirectory; } set { _runner.WorkingDirectory = value; } }

        /// <summary> Returns true if this instance is running a process </summary>
        public bool IsRunning
        {
            get { return _runner.IsRunning; }
        }

        /// <summary> Kills the process if it is still running </summary>
        public void Kill() { _runner.Kill(); }

        /// <summary> Closes std::in and waits for the process to exit </summary>
        public void WaitForExit() { _runner.WaitForExit(); }

        /// <summary> Closes std::in and waits for the process to exit, returns false if the process did not exit in the time given </summary>
        public bool WaitForExit(TimeSpan timeout)
        { return _runner.WaitForExit(timeout); }

		/// <summary> Runs the process and returns the exit code. </summary>
		public int Run() { return Run(new string[0]); }
        /// <summary> Runs the process and returns the exit code. </summary>
		public int Run(params string[] args) { return Run(null, args); }
		/// <summary> Runs the process and returns the exit code. </summary>
		public int Run(TextReader input, params string[] arguments)
		{
			if (input == null && _engine.UsesStandardInputScript)
				input = new StringReader(_scriptFile.ReadAllText());

			return _runner.Run(input, arguments);
		}

    	/// <summary> Runs the process and returns the exit code. </summary>
		public void Start() { Start(new string[0]); }
        /// <summary> Starts the process and returns. </summary>
		public void Start(params string[] args)
        {
            _runner.Start(args);
            if (_engine.UsesStandardInputScript)
            {
            	_runner.StandardInput.Write(_scriptFile.ReadAllText());
            	_runner.StandardInput.Close();
            }
        }
    }
}