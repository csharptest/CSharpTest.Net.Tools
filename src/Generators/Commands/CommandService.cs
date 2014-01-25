#region Copyright 2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using CSharpTest.Net.Services;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Commands
{
    /// <summary>
    /// Provide this type to the CommandInterpreter to enable running as a service.
    /// </summary>
    public static class ServiceCommands
    {
        /// <summary>
        /// Provide this type to the CommandInterpreter to enable installing any command as a service.
        /// </summary>
        public static class Installation
        {
            /// <summary>
            /// Installs a Win32 service that runs a single command on this executable when started.  While
            /// typically used for the HostHTTP command, it will operate for any command.  Commands hosted as a
            /// service may use the Console.ReadLine() to wait for the service-stop command.
            /// </summary>
            /// <param name="svcName">The name of the Win32 Service to install.</param>
            /// <param name="displayName">The description of the Win32 Service.</param>
            /// <param name="startupType">The service startup type: Automatic, Manual, or Disabled.</param>
            /// <param name="serviceAccount">The service account: LocalService, NetworkService, or LocalSystem.</param>
            /// <param name="commandName">The command to run as a service.</param>
            /// <param name="arguments">The arguments required to run the command.</param>
            /// <param name="ci">The current command interpreter, used to verify the command 'RunAsService' and the commandName provided.</param>
            /// <param name="executable">Optional, The path to the executable that is running.</param>
            [Command("InstallService", Description = "Installs the specified command to run as a service."), HttpIgnore]
            public static void InstallService(
                [Argument("serviceName", Description = "The service name to install as.")] string svcName,
                [Argument("displayName", Description = "The display name of the service.")] string displayName,
                [Argument("startupType", Description = "The service startup type: Automatic, Manual, or Disabled.")] ServiceStartMode startupType,
                [Argument("serviceAccount", Description = "The service account: LocalService, NetworkService, or LocalSystem.")] ServiceAccount serviceAccount,
                [Argument("command", Description = "The command to run as a service.")] string commandName,
                [Argument("arguments", DefaultValue = null, Description = "The arguments required to run the command.")] string[] arguments,
                ICommandInterpreter ci,
                [Argument("executable", Visible = false), System.ComponentModel.DefaultValue(null)] 
                string executable
                )
            {
                ICommand cmd;
                if (!ci.TryGetCommand("RunAsService", out cmd))
                    throw new ApplicationException(
                        "You must add typeof(ServiceCommands) to the commands or provide your own RunAsService command.");
                if (!ci.TryGetCommand(commandName, out cmd))
                    throw new ApplicationException("The command name '" + commandName + "' was not found.");
                if (String.IsNullOrEmpty(svcName))
                    throw new ArgumentNullException("svcName");

                string exe = Path.GetFullPath(executable ?? new Uri(Constants.EntryAssembly.Location).AbsolutePath);
                List<string> args = new List<string>();

                args.Add("RunAsService");
                args.Add("/serviceName=" + svcName);
                args.Add(commandName);
                args.AddRange(arguments ?? new string[0]);

                using (
                    SvcControlManager scm = SvcControlManager.Create(svcName, displayName, false, startupType, exe,
                                                                     args.ToArray(),
                                                                     SvcControlManager.NT_AUTHORITY.Account(
                                                                         serviceAccount), null))
                {
                    args.RemoveRange(0, 2);
                    args.Insert(0, Path.GetFileName(exe));
                    scm.SetDescription(ArgumentList.EscapeArguments(args.ToArray()));
                }
            }

            /// <summary>
            /// Removes a Win32 Service that was previously installed.
            /// </summary>
            /// <param name="svcName">The name of the service to remove.</param>
            [Command("UninstallService", Description = "Uninstalls the specified service."), HttpIgnore]
            public static void UninstallService(
                [Argument("serviceName", Description = "The service name to uninstall.")] string svcName
                )
            {
                using (SvcControlManager scm = new SvcControlManager(svcName))
                {
                    scm.Delete();
                }
            }
        }

        /// <summary>
        /// Used to host a command-interpreter's command as a Win32 service.  Use ServiceCommands.Installation.InstallService
        /// to install the service.
        /// </summary>
        [Command("RunAsService", Visible = false), HttpIgnore]
        public static void RunAsService(
            ICommandInterpreter ci,
            [Argument("serviceName")] string name,
            [AllArguments] string[] rawArgs
            )
        {
            string svcName;
            ArgumentList.Remove(ref rawArgs, "serviceName", out svcName);
            Console.SetIn(TextReader.Null);

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ServiceBase.Run(new ServiceProcess(ci, svcName, rawArgs));
        }

        class ServiceProcess : ServiceBase
        {
            private readonly ICommandInterpreter _ci;
            private readonly string[] _arguments;
            private Thread _worker;
            private ManualResetEvent _shutdown;

            public ServiceProcess(ICommandInterpreter ci, string svcName, string[] arguments)
            {
                _ci = ci;
                _arguments = arguments;
                AutoLog = true;
                CanStop = true;
                ServiceName = svcName;
                _shutdown = new ManualResetEvent(false);
            }

            protected override void OnStart(string[] args)
            {
                _worker = new Thread(Run);
                _worker.IsBackground = true;
                _worker.Name = ServiceName;
                _worker.Start();
            }

            private void Run()
            {
                //bool debugging = false;
                //while (!debugging)
                //    Thread.Sleep(100);

                var wtr = new EventLogWriter(ServiceName);
                try
                {
                    wtr.WriteLine("Service initializing...");
                    _ci.Run(_arguments, wtr, wtr, new BlockingReader(_shutdown));
                }
                catch (Exception e)
                {
                    try
                    {
                        wtr.WriteLine(e.ToString());
                    }
                    catch
                    {
                    }
                }
                finally
                {
                    if (_shutdown.WaitOne(0, false) == false)
                    {
                        wtr.WriteLine("The command unexpectedly terminated.");
                        Environment.Exit(1);
                    }
                }
            }

            protected override void OnStop()
            {
                try
                {
                    _shutdown.Set();
                    if (_worker != null)
                    {
                        RequestAdditionalTime(30000);
                        if (!_worker.Join(15000))
                        {
                            RequestAdditionalTime(30000);
                            if (!_worker.Join(15000))
                            {
                                _worker.Abort();
                                _worker.Join();
                            }
                        }
                    }
                }
                finally
                {
                    _worker = null;
                }
            }
        }

        private class EventLogWriter : TextWriter
        {
            private readonly string _name;
            EventLog _log;
            private StringBuilder _sb;

            public EventLogWriter(string name)
            {
                _name = name;
                _sb = new StringBuilder(1024);
                try { _log = new EventLog("Application", ".", name); }
                catch { _log = null; } 
            }

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }

            public override void Write(char ch)
            {
                if (ch == '\n' || ch == '\r')
                {
                    if (_sb.Length > 0)
                    {
                        if (_log != null)
                        {
                                try
                            {
                                var msg = _sb.ToString().Trim();
                                if (msg.Length > 0)
                                {
                                    lock (_log)
                                        _log.WriteEntry(String.Format("[{0}]: {1}", _name, msg));
                                }
                            }
                            catch
                            {
                                _log = null;
                            }
                        }
                        _sb.Length = 0;
                    }
                }
                else
                {
                    _sb.Append(ch);
                }
            }
            public override void Write(char[] buffer, int index, int count)
            {
                _sb.Append(buffer, index, count);
                if (count > 0 && buffer[index + count-1] == '\n' || buffer[index + count-1] == '\r')
                    Write('\n');
            }
        }

        private class BlockingReader : TextReader
        {
            private readonly WaitHandle _stop;

            public BlockingReader(WaitHandle stop)
            {
                _stop = stop;
            }

            public override int Peek()
            {
                _stop.WaitOne();
                return -1;
            }
            public override int Read()
            {
                return Peek();
            }
        }
    }
}
