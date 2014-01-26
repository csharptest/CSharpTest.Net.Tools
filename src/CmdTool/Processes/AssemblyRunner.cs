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
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Processes
{
    /// <summary>
    /// Create an AppDomain configured to run the .Net Assembly provided and marshalls Console input/output to and
    /// from the app domain when run.  This allow a more performant execution of .Net command-line tools while
    /// keeping with *most* of the behavior of running out-of-process.  Some serious side effects can occur when
    /// using Environment.* settings like CurrentDirectory and ExitCode since these are shared with the appdomain.
    /// </summary>
	public class AssemblyRunner : IRunner
	{
		private static readonly string[] EmptyArgList = new string[0];

		private readonly string _executable;
		private readonly AppDomain _workerDomain;

		private event ProcessOutputEventHandler _outputReceived;
		private event ProcessExitedEventHandler _processExited;

		private bool _disposed;
		private bool _isRunning;
		private string _workingDir;
		private volatile int _exitCode;
		private Thread _running;
		private Exception _exception;

        /// <summary>
        /// Constructs the AppDomain for the given executable by using it's path for the base directory and configuraiton file.
        /// </summary>
		public AssemblyRunner(string executable)
		{
			_executable = FileUtils.FindFullPath(Check.NotEmpty(executable));
			AppDomainSetup setup = new AppDomainSetup();

			setup.ApplicationBase = Path.GetDirectoryName(_executable);
			setup.ApplicationName = Path.GetFileNameWithoutExtension(_executable);
			setup.ConfigurationFile = _executable + ".config";
			_workerDomain = AppDomain.CreateDomain(setup.ApplicationName, AppDomain.CurrentDomain.Evidence, setup);
		}

        /// <summary>
        /// Ensures clean-up of the app domain... This has to be pushed off of the GC Cleanup thread as AppDoamin.Unload will
        /// fail on GC thread.
        /// </summary>
		~AssemblyRunner()
		{
			if(!_disposed)
			{
                try { new Action<AppDomain>(UnloadDomain).BeginInvoke(_workerDomain, null, null); }
                catch { }
			}
		}
        /// <summary> Ignores errors from the AppDomain.Unload since exceptions would be unhandled. </summary>
		static void UnloadDomain(AppDomain domain)
		{
			try { AppDomain.Unload(domain); }
            catch(Exception e) { System.Diagnostics.Trace.WriteLine(e.ToString(), typeof(AssemblyRunner).FullName); }
		}
        /// <summary>
        /// Returns true if this object's worker domain has been unloaded.
        /// </summary>
		public bool IsDisposed { get { return _disposed; } }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
		{
			if (!_disposed)
			{
				_disposed = true;
				Kill();
				AppDomain.Unload(_workerDomain);
				GC.SuppressFinalize(this);
			}
		}

		/// <summary> Returns a debug-view string of process/arguments to execute </summary>
		public override string ToString()
		{
			return _executable;
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

		[Obsolete("This is not implemented for domains", true)]
		TextWriter IRunner.StandardInput { get { throw new NotSupportedException(); } }

		/// <summary> Gets or sets the initial working directory for the process. </summary>
		public string WorkingDirectory { get { return _workingDir ?? Environment.CurrentDirectory; } set { _workingDir = value; } }

			/// <summary> Waits for the process to exit and returns the exit code </summary>
		public int ExitCode { get { WaitForExit(); return _exitCode; } }

		/// <summary> Returns true if this instance is running a process </summary>
		public bool IsRunning { get { return _isRunning && !WaitForExit(TimeSpan.Zero, false); } }

		/// <summary> Kills the process if it is still running </summary>
		public void Kill()
		{
			Thread t = _running;
			if (t != null && t.IsAlive)
			{
				t.Abort();
				if (!WaitForExit(TimeSpan.FromMinutes(1)))
					throw new TimeoutException();
			}
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
			int waitTime = timeout.TotalMilliseconds >= int.MaxValue ? -1 : (int)timeout.TotalMilliseconds;
			Thread runner = _running;
			if (runner == null || !runner.IsAlive || runner.Join(waitTime))
			{
				if (_exception != null)
					throw _exception;
				return true;
			}
			return false;
		}

		/// <summary> Runs the process and returns the exit code. </summary>
		public int Run()
		{ return Run(EmptyArgList); }

		/// <summary> Runs the process with additional arguments and returns the exit code. </summary>
		public int Run(params string[] arguments)
		{ return Run(null, arguments); }

		/// <summary> Runs the process with additional arguments and returns the exit code. </summary>
		public int Run(TextReader input, params string[] arguments)
		{
			Check.Assert<InvalidOperationException>(_isRunning == false);
			Execute(new StartArguments(input, arguments));
			if (_exception != null)
				throw _exception;
			return ExitCode;
		}

		/// <summary> Starts the process and returns. </summary>
		public void Start()
		{ Start(EmptyArgList); }

		/// <summary> Starts the process with additional arguments and returns. </summary>
		public void Start(params string[] arguments)
		{ Start(null, arguments); }

		/// <summary> Starts the process with additional arguments and returns. </summary>
		public void Start(TextReader input, params string[] arguments)
		{
			Check.Assert<InvalidOperationException>(_isRunning == false);
			Thread t = new Thread(Execute);
			t.SetApartmentState(ApartmentState.MTA);
			t.Name = Path.GetFileName(_executable);
            try
            {
                _isRunning = true;
                t.Start(new StartArguments(input, arguments));
                _running = t;
            }
            catch
            {
                _isRunning = false;
                throw;
            }
		}

		[Serializable]
		class DomainHook : MarshalByRefObject, IDisposable
		{
			public override object InitializeLifetimeService()
			{ return null; }
			TextWriter _out, _err;
			TextReader _in;

			StringWriter _captureout, _captureerr;

			public void Dispose()
			{ }

			public void Capture(string stdInput)
			{
				_out = Console.Out;
				_err = Console.Error;
				_in = Console.In;

				Console.SetOut(_captureout = new StringWriter());
				Console.SetError(_captureerr = new StringWriter());
				Console.SetIn(new StringReader(stdInput ?? String.Empty));
			}

			public void GetOutput(out string stdout, out string stderr)
			{
				Console.SetIn(_in);
				_in = null;
				Console.SetOut(_out);
				Console.SetError(_err);
				_out = _err = null;
				stdout = _captureout.ToString();
				_captureout.Dispose();
				_captureout = null;
				stderr = _captureerr.ToString();
				_captureerr.Dispose();
				_captureerr = null;
			}
		}

		private class StartArguments
		{
			public StartArguments(TextReader input, string[] args)
			{
				StdInput = input;
				Arguments = args;
			}

			public readonly TextReader StdInput;
			public readonly string[] Arguments;
		}

		private void Execute(object objArgs)
		{
            string cwd = null;
            try
            {
                _exception = null;
                cwd = Environment.CurrentDirectory;
                StartArguments args = (StartArguments)objArgs;

                ObjectHandle obj = _workerDomain.CreateInstanceFrom(GetType().Assembly.Location, typeof(DomainHook).FullName);
                if (obj == null) throw new ApplicationException("Unable to hook child application domain.");
                using (DomainHook hook = (DomainHook)obj.Unwrap())
                {
                    hook.Capture(args.StdInput == null ? String.Empty : args.StdInput.ReadToEnd());
                    try
                    {
                        string[] arguments = args.Arguments;
                        Environment.CurrentDirectory = WorkingDirectory;
#if NET20 || NET35
                        _exitCode = _workerDomain.ExecuteAssembly(_executable, AppDomain.CurrentDomain.Evidence, arguments);
#else
                        _exitCode = _workerDomain.ExecuteAssembly(_executable, arguments);
#endif
                    }
                    finally
                    {
                        string line, stdout, stderr;
                        hook.GetOutput(out stdout, out stderr);
                        if (_outputReceived != null)
                        {
                            using (StringReader r = new StringReader(stdout))
                                while (null != (line = r.ReadLine()))
                                    _outputReceived(this, new ProcessOutputEventArgs(line, false));
                            using (StringReader r = new StringReader(stderr))
                                while (null != (line = r.ReadLine()))
                                    _outputReceived(this, new ProcessOutputEventArgs(line, true));
                        }
                    }
                }
            }
            catch (ThreadAbortException) { return; }
            catch (Exception e)
            {
                _exitCode = -1;
                try
                {
                    ThreadStart savestack = Delegate.CreateDelegate(typeof(ThreadStart), e, "InternalPreserveStackTrace", false, false) as ThreadStart;
                    if (savestack != null) savestack();
                    _exception = e;
                }
                catch { _exception = new TargetInvocationException(e); }
            }
			finally
			{
				_isRunning = false;
				_running = null;
				if(cwd != null) Environment.CurrentDirectory = cwd;

				if (_processExited != null)
					_processExited(this, new ProcessExitedEventArgs(_exitCode));
			}
		}
	}
}
