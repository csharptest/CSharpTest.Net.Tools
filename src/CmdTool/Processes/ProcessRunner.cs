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
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Text;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Processes
{
    /// <summary>
	/// Creates/Spawns a process with the standard error/out/in all mapped.  Subscribe to
	/// the OutputReceived event prior to start/run to listen to the program output, write
	/// to the StandardInput for input.
	/// </summary>
	public class ProcessRunner : IRunner
    {
		private static readonly string[] EmptyArgList = new string[0];

		private readonly ManualResetEvent _mreProcessExit = new ManualResetEvent(true);
		private readonly ManualResetEvent _mreOutputDone = new ManualResetEvent(true);
		private readonly ManualResetEvent _mreErrorDone = new ManualResetEvent(true);

		private readonly string _executable;
		private readonly string[] _arguments;

		private event ProcessOutputEventHandler _outputReceived;
		private event ProcessExitedEventHandler _processExited;

		private bool _isRunning;
		private string _workingDir;
		private volatile int _exitCode;
		private Process _running;
		private TextWriter _stdIn;

		/// <summary>Creates a ProcessRunner for the given executable </summary>
		public ProcessRunner(string executable)
			: this(executable, EmptyArgList)
		{ }
		/// <summary>Creates a ProcessRunner for the given executable and arguments </summary>
		public ProcessRunner(string executable, params string[] args)
		{
			_executable = Utils.FileUtils.FindFullPath(Check.NotEmpty(executable));
			_arguments = args == null ? EmptyArgList : args;

			_isRunning = false;
			_exitCode = 0;
			_running = null;
			_stdIn = null;
		}

		/// <summary> Detaches event handlers and closes input streams </summary>
		public void Dispose()
		{
			_outputReceived = null;
			_processExited = null;
			TextWriter w = _stdIn;
			if(w != null) w.Dispose();
		}

        /// <summary>
        /// Returns the remote process Id
        /// </summary>
        public int PID { get { return _running.Id; } }

    	/// <summary> Returns a debug-view string of process/arguments to execute </summary>
		public override string ToString()
		{
			return String.Format("{0} {1}", _executable, ArgumentList.EscapeArguments(_arguments));
		}

		/// <summary> Notifies caller of writes to the std::err or std::out </summary>
		public event ProcessOutputEventHandler OutputReceived
		{
			add { lock (this) _outputReceived += value; }
			remove { lock (this) _outputReceived -= value; }
		}

		/// <summary> Notifies caller when the process exits </summary>
		public event ProcessExitedEventHandler ProcessExited
		{
			add { lock (this) _processExited += value; }
			remove { lock (this) _processExited -= value; }
		}

		/// <summary> Allows writes to the std::in for the process </summary>
		public TextWriter StandardInput { get { return Check.NotNull(_stdIn); } }

		/// <summary> Gets or sets the initial working directory for the process. </summary>
		public string WorkingDirectory { get { return _workingDir ?? Environment.CurrentDirectory; } set { _workingDir = value; } }

			/// <summary> Waits for the process to exit and returns the exit code </summary>
		public int ExitCode { get { WaitForExit(); return _exitCode; } }

		/// <summary> Kills the process if it is still running </summary>
		public void Kill()
		{
			int attempt = 3;
            try
            {
                while (attempt-- >= 0 && _isRunning && !WaitForExit(TimeSpan.Zero, false))
                {
                    try 
                    { 
                        if (_running != null && !_running.HasExited)
                            _running.Kill();
                    }
                    catch (System.InvalidOperationException) { break; }
                }
            }
            catch (System.ComponentModel.Win32Exception)
            { }

		    TryRaiseExitedEvent(_mreErrorDone);
			TryRaiseExitedEvent(_mreOutputDone);
			TryRaiseExitedEvent(_mreProcessExit);
			_isRunning = false;
		}

		/// <summary> Closes std::in and waits for the process to exit </summary>
		public void WaitForExit()
		{
			WaitForExit(TimeSpan.MaxValue, true);
		}

		/// <summary> Closes std::in and waits for the process to exit, returns false if the process did not exit in the time given </summary>
		public bool WaitForExit(TimeSpan timeout) { return WaitForExit(timeout, true); }
		/// <summary> Waits for the process to exit, returns false if the process did not exit in the time given </summary>
		public bool WaitForExit(TimeSpan timeout, bool closeStdInput)
		{
			if (_stdIn != null && closeStdInput)
			{ _stdIn.Close(); _stdIn = null; }

            int waitTime = timeout.TotalMilliseconds >= int.MaxValue ? -1 : (int)timeout.TotalMilliseconds;
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                if (!_mreProcessExit.WaitOne(waitTime, false))
                    return false;
                if (!_mreErrorDone.WaitOne(waitTime, false))
                    return false;
                if (!_mreOutputDone.WaitOne(waitTime, false))
                    return false;
                return true;
            }

			bool exited = WaitHandle.WaitAll(new WaitHandle[] { _mreErrorDone, _mreOutputDone, _mreProcessExit }, waitTime, false);
			return exited;
		}

		/// <summary> Returns true if this instance is running a process </summary>
		public bool IsRunning { get { return _isRunning && !WaitForExit(TimeSpan.Zero, false); } }

		#region Run, Start, & Overloads
		/// <summary> Runs the process and returns the exit code. </summary>
		public int Run() { return Run(null, EmptyArgList); }

		/// <summary> Runs the process with additional arguments and returns the exit code. </summary>
		public int Run(params string[] moreArguments) { return Run(null, moreArguments); }

		/// <summary> Runs the process with additional arguments and returns the exit code. </summary>
		public int Run(TextReader input, params string[] arguments)
		{
			List<string> args = new List<string>(_arguments);
			args.AddRange(arguments ?? EmptyArgList);
			return InternalRun(input, args.ToArray());
		}

		/// <summary> 
		/// Calls String.Format() for each argument this runner was constructed with giving the object
		/// array as the arguments.  Once complete it runs the process with the new set of arguments and
		/// returns the exit code.
		/// </summary>
		public int RunFormatArgs(params object[] formatArgs)
		{
			Check.NotNull(formatArgs);
			List<string> args = new List<string>();
			foreach (string arg in _arguments)
				args.Add(String.Format(arg, formatArgs));
			return InternalRun(null, args.ToArray());
		}

		/// <summary> Starts the process and returns. </summary>
		public void Start() { Start(EmptyArgList); }

		/// <summary> Starts the process with additional arguments and returns. </summary>
		public void Start(params string[] moreArguments)
		{
			List<string> args = new List<string>(_arguments);
			args.AddRange(moreArguments ?? EmptyArgList);
			InternalStart(args.ToArray());
		}

		/// <summary> 
		/// Calls String.Format() for each argument this runner was constructed with giving the object
		/// array as the arguments.  Once complete it starts the process with the new set of arguments and
		/// returns.
		/// </summary>
		public void StartFormatArgs(params object[] formatArgs)
		{
			Check.NotNull(formatArgs);
			List<string> args = new List<string>();
			foreach (string arg in _arguments)
				args.Add(String.Format(arg, formatArgs));
			InternalStart(args.ToArray());
		}
		#endregion Run, Start, & Overloads
		
		private int InternalRun(TextReader input, string[] arguments)
		{
			InternalStart(arguments);
			if (input != null)
			{
				char[] buffer = new char[1024];
				int count;
				while (0 != (count = input.Read(buffer, 0, buffer.Length)))
					StandardInput.Write(buffer, 0, count);
			}
			WaitForExit();
			return ExitCode;
		}

		private void InternalStart(params string[] arguments)
		{
			if (IsRunning)
				throw new InvalidOperationException("The running process must first exit.");

			_isRunning = true;

			_mreProcessExit.Reset();
			_mreOutputDone.Reset();
			_mreErrorDone.Reset();
			_exitCode = 0;
			_stdIn = null;
			_running = new Process();

			string stringArgs = ArgumentList.EscapeArguments(arguments);
			ProcessStartInfo psi = new ProcessStartInfo(_executable, stringArgs);
			psi.WorkingDirectory = this.WorkingDirectory;

			psi.RedirectStandardInput = true;
			psi.RedirectStandardError = true;
			psi.RedirectStandardOutput = true;
			psi.CreateNoWindow = true;
			psi.UseShellExecute = false;
			psi.ErrorDialog = false;

			_running.StartInfo = psi;

			_running.Exited += process_Exited;
			_running.OutputDataReceived += process_OutputDataReceived;
			_running.ErrorDataReceived += process_ErrorDataReceived;

			_running.EnableRaisingEvents = true;
			Trace.TraceInformation("EXEC: {0} {1}", _running.StartInfo.FileName, _running.StartInfo.Arguments);
			_running.Start();

			_stdIn = _running.StandardInput;
			_running.BeginOutputReadLine();
			_running.BeginErrorReadLine();
		}

		private void OnOutputReceived(ProcessOutputEventArgs args)
		{
			lock (this)
			{
				if (_outputReceived != null)
					_outputReceived(this, args);
			}
		}

		void TryRaiseExitedEvent(ManualResetEvent completing)
		{
			if (completing == null || completing.WaitOne(0, false))
				return;//bad signal or already complete.

			try
			{
				if (_processExited != null)
				{
					bool isComplete = false;
					ProcessExitedEventHandler handler;

					lock (this)
					{
						if (null != (handler = _processExited))
						{
							if ( //determine if we are 'about' to complete with this signal
								(Object.ReferenceEquals(completing, _mreProcessExit) || _mreProcessExit.WaitOne(0, false)) &&
								(Object.ReferenceEquals(completing, _mreOutputDone) || _mreOutputDone.WaitOne(0, false)) &&
								(Object.ReferenceEquals(completing, _mreErrorDone) || _mreErrorDone.WaitOne(0, false))
								)
							{
								isComplete = true;
							}
						}
					}

					if (isComplete)
					{
						_isRunning = false;
						if (handler != null)
							handler(this, new ProcessExitedEventArgs(_exitCode));
					}
				}
			}
			finally
			{
				completing.Set();
			}
		}

		void process_Exited(object o, EventArgs e)
		{
			Trace.TraceInformation("EXIT: {0}", _running.StartInfo.FileName);
            try { _exitCode = _running.ExitCode; }
            catch (InvalidOperationException) { _exitCode = -1; }
			TryRaiseExitedEvent(_mreProcessExit);
		}

		void process_OutputDataReceived(object o, DataReceivedEventArgs e)
		{ InternalOutputDataReceived(e.Data); }

		void InternalOutputDataReceived(string data)
		{
			if (data != null)
				OnOutputReceived(new ProcessOutputEventArgs(data, false));
			else
				TryRaiseExitedEvent(_mreOutputDone);
		}

		void process_ErrorDataReceived(object o, DataReceivedEventArgs e)
		{ InternalErrorDataReceived(e.Data); }

		void InternalErrorDataReceived(string data)
		{
			if (data != null)
				OnOutputReceived(new ProcessOutputEventArgs(data, true));
			else
				TryRaiseExitedEvent(_mreErrorDone);
		}
	}
}
