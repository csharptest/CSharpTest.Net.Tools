#region Copyright 2008-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

/// <summary>
/// Quick and dirty logging for components that do not have dependencies.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
internal static partial class Log
{
    static int _init = 0;
    static TextWriter _textWriter = null;
    static TextWriterTraceListener _traceWriter = null;

    /// <summary>
    /// Allows you to close/open the writer
    /// </summary>
    public static void Open()
    {
        try
        {
            string fullName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), String.Format("{0}\\LogFile.txt", Process.GetCurrentProcess().ProcessName));
            Directory.CreateDirectory(Path.GetDirectoryName(fullName));

            string back = Path.ChangeExtension(fullName, ".bak");
            if (File.Exists(back))
                File.Delete(back);
            if (File.Exists(fullName))
                File.Move(fullName, back);

            FileStream fsStream = File.Open(fullName, FileMode.Append, FileAccess.Write, FileShare.Read | FileShare.Delete);
            StreamWriter sw = new StreamWriter(fsStream);
            sw.AutoFlush = true;
            Open(TextWriter.Synchronized(sw));
        }
        catch (Exception e)
        { Trace.WriteLine(e.ToString(), "CSharpTest.Net.QuickLog.Open()"); }
    }

    /// <summary>
    /// Redirects all log output to the provided text-writer
    /// </summary>
    public static void Open(TextWriter writer)
    {
        try
        {
            Close();
            _init = 1;
            _textWriter = writer;
            Trace.Listeners.Add(_traceWriter = new TextWriterTraceListener(_textWriter));
        }
        catch (Exception e)
        { Trace.WriteLine(e.ToString(), "CSharpTest.Net.QuickLog.Open()"); }
    }

    /// <summary>
    /// Returns the currently in-use text writer for log output
    /// </summary>
    public static TextWriter TextWriter { get { return _textWriter ?? TextWriter.Null; } }

    /// <summary>
    /// Allows you to close/open the writer
    /// </summary>
    public static void Close()
    {
        if (_traceWriter != null)
        {
            try
            {
                Trace.Listeners.Remove(_traceWriter);
                _traceWriter.Dispose();
            }
            catch { }
            finally
            {
                _textWriter = null;
                _traceWriter = null;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Error(Exception e) { InternalWrite(TraceLevel.Error, "{0}", e); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Warning(Exception e) { InternalWrite(TraceLevel.Warning, "{0}", e); }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Error(string format, params object[] args) { InternalWrite(TraceLevel.Error, format, args); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Warning(string format, params object[] args) { InternalWrite(TraceLevel.Warning, format, args); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Info(string format, params object[] args) { InternalWrite(TraceLevel.Info, format, args); }
    [MethodImpl(MethodImplOptions.NoInlining)]
	public static void Verbose(string format, params object[] args) { InternalWrite(TraceLevel.Verbose, format, args); }
	[MethodImpl(MethodImplOptions.NoInlining), Conditional("DEBUG")]
	public static void Debug(string format, params object[] args) { InternalWrite(TraceLevel.Verbose, format, args); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Write(string format, params object[] args) { InternalWrite(TraceLevel.Off, format, args); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Write(TraceLevel level, string format, params object[] args) { InternalWrite(level, format, args); }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IDisposable Start(string format, params object[] args)
    {
        try
        {
            if (args.Length > 0) format = String.Format(format, args);
            InternalWrite(TraceLevel.Verbose, "Start {0}", format);
        }
        catch (Exception e) { Trace.WriteLine(e.ToString(), "CSharpTest.Net.QuickLog.Write()"); }
        return new TaskInfo(format);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IDisposable AppStart(string format, params object[] args)
    {
        try
        {
            if (args.Length > 0) format = String.Format(format, args);
            InternalWrite(TraceLevel.Verbose, "Start {0}", format);
        }
        catch (Exception e) { Trace.WriteLine(e.ToString(), "CSharpTest.Net.QuickLog.Write()"); }
        return new TaskInfo(format);
    }

    [System.Diagnostics.DebuggerNonUserCode]
    private class TaskInfo : IDisposable
    {
        private readonly DateTime _start;
        private readonly string _task;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public TaskInfo(string task) { _task = task; _start = DateTime.Now; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        void IDisposable.Dispose()
        { InternalWrite(TraceLevel.Verbose, "End {0} ({1} ms)", _task, (DateTime.Now - _start).TotalMilliseconds); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InternalWrite(TraceLevel level, string format, params object[] args)
    {
        try
        {
            if (_init == 0)
            {
                if (0 == System.Threading.Interlocked.Exchange(ref _init, 1))
                { Open(); }
            }

            int depth = 2;
            if (args.Length > 0)
                format = String.Format(format, args);

            if (level <= _consoleLogLevel)
                Console_LogWrite(level, format);

            StackFrame frame;
            System.Reflection.MethodBase method;

            do
            {
                frame = new StackFrame(depth++);
                method = frame.GetMethod();
            }
            while (method.ReflectedType.FullName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                method.ReflectedType.GetCustomAttributes(typeof(System.Diagnostics.DebuggerNonUserCodeAttribute), true).Length > 0);

            string methodName, callingType;
            methodName = String.Format("{0}", method);
            callingType = String.Format("{0}", method.ReflectedType);

            string full = String.Format("{0:D2}{1,8} - {2}   at {3}",
                System.Threading.Thread.CurrentThread.ManagedThreadId,
                level == TraceLevel.Off ? "None" : level.ToString(),
                format, methodName);

            Trace.WriteLine(full, callingType);

            if (LogWrite != null)
                LogWrite(method, level, format);
        }
        catch (Exception e)
        { Trace.WriteLine(e.ToString(), "CSharpTest.Net.QuickLog.Write()"); }
    }

    public delegate void LogEventHandler(System.Reflection.MethodBase method, TraceLevel level, string message);
    public static event LogEventHandler LogWrite;

    private static TraceLevel _consoleLogLevel = TraceLevel.Off;
    public static TraceLevel ConsoleLevel { get { return _consoleLogLevel;} set { _consoleLogLevel = value; } }
    static void Console_LogWrite(System.Diagnostics.TraceLevel level, string message)
    {
        if (String.IsNullOrEmpty(message) || level > _consoleLogLevel)
            return;

        lock (typeof(Console))
        {
            if (level != TraceLevel.Warning && level != TraceLevel.Error)
            {
                Console.Out.WriteLine(message);
            }
            else
            {
                ConsoleColor clr = Console.ForegroundColor;
                if (level == System.Diagnostics.TraceLevel.Error)
                    Console.ForegroundColor = ConsoleColor.Red;
                else
                    Console.ForegroundColor = ConsoleColor.Yellow;
                
                Console.Error.WriteLine(message);
                Console.ForegroundColor = clr;
            }
        }
    }
}