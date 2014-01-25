#region Copyright 2010 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using CSharpTest.Net.CustomTool;
using CSharpTest.Net.Utils;
using CSharpTest.Net.CustomTool.Projects;

namespace CSharpTest.Net.StampCopyright
{
	static class Program
	{
		static int DoHelp()
		{
			Console.WriteLine("");
			Console.WriteLine("Usage:");
			Console.WriteLine("    StampCopyright.exe copyright.txt .\\*.csproj [/nologo] [/wait]");
			Console.WriteLine("");
			Console.WriteLine("        copyright.txt is the expected prefix for all .cs source files");
			Console.WriteLine("");
			Console.WriteLine("        .\\*.csproj is the inclusion list of projects to process");
			Console.WriteLine("");
			Console.WriteLine("        /nologo Hide the startup message");
			Console.WriteLine("");
			Console.WriteLine("        /wait after processing wait for user input");
			return 0;
		}

		static string _copyText;
        static string _subversion;
		static int _changes;

		[STAThread]
		static int Main(string[] raw)
		{
			ArgumentList args = new ArgumentList(raw);
			List<string> input = new List<string>(args.Unnamed);

			if (args.Contains("nologo") == false)
			{
				Console.WriteLine("StampCopyright.exe");
				Console.WriteLine("Copyright 2009-{0:yyyy} by Roger Knapp, Licensed under the Apache License, Version 2.0", DateTime.Now);
				Console.WriteLine("");
			}

			if ((args.Count == 0 && args.Unnamed.Count == 0) || args.Contains("?") || args.Contains("help"))
				return DoHelp();

			try
			{
                args.TryGetValue("svn", out _subversion);

				Log.ConsoleLevel = System.Diagnostics.TraceLevel.Warning;
                _changes = 0;
                _copyText = File.ReadAllText(input[0]).Trim();
                _copyText = _copyText.Replace("YEAR", DateTime.Now.Year.ToString());
                _copyText = _copyText.Replace("yyyy", DateTime.Now.Year.ToString());
				input.RemoveAt(0);
				ProjectVisitor visitor = new ProjectVisitor(false, input.ToArray());
				visitor.VisitProjectItems(VisitProjectItem);
			}
			catch (ApplicationException ae)
			{
				Log.Error(ae);
				Console.Error.WriteLine();
				Console.Error.WriteLine(ae.Message);
				Environment.ExitCode = -1;
			}
			catch (Exception e)
			{
				Log.Error(e);
				Console.Error.WriteLine();
				Console.Error.WriteLine(e.ToString());
				Environment.ExitCode = -1;
			}

			if (args.Contains("wait"))
			{
				Console.WriteLine();
				Console.WriteLine("Press [Enter] to continue...");
				Console.ReadLine();
			}

			return Environment.ExitCode;
		}

        static bool _ignoreConfirm = false;

        static bool Confirm(string format, params object[] args)
        {
            if (_ignoreConfirm)
            {
                Console.WriteLine(format, args);
                return true;
            }

            while (true)
            {
                Console.Write(format + " (y/n/a/q)?:", args);
                try
                {
                        switch (Console.ReadKey().Key)
                        {
                            case ConsoleKey.Y: return true;
                            case ConsoleKey.N: return false;
                            case ConsoleKey.A: return _ignoreConfirm = true;
                            case ConsoleKey.Q: throw new ApplicationException(new OperationCanceledException().Message);
                        }
                }
                finally
                {
                    Console.WriteLine();
                }
            }
        }

	    static void VisitProjectItem(IProjectInfo project, IProjectItem item)
		{
			if (item.BuildAction != "Compile")
				return;

			string fileName = item.FullFileName;
			if (!File.Exists(fileName))
				return;
			fileName = Path.GetFullPath(fileName);
			
			string ext = Path.GetExtension(fileName);
			if (!StringComparer.OrdinalIgnoreCase.Equals(".cs", ext))
				return;

			Encoding encoding;
			string content = ReadFileText(fileName, out encoding);
			string original = content;

			int ixGenerated = content.IndexOf("Generated", StringComparison.OrdinalIgnoreCase);
			if (ixGenerated >= 0)
				return;

			InsertFileHeader(fileName, ref content, _copyText);

			if (content != original)
			{
                lock (typeof(Program))
                {
                    if(Confirm("Update {0}", fileName))
                    {
                        _changes++;
                        using (StreamWriter wtr = new StreamWriter(fileName, false, encoding))
                        {
                            wtr.Write(content);
                            wtr.Flush();
                        }
                    }
                }
			}
		}

		static string ReadFileText(string fileName, out Encoding encoding)
		{
			encoding = Encoding.ASCII;
			string content = String.Empty;
			if (new FileInfo(fileName).Length > 0)
			{
				using (Stream io = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
				{
					int tmpCh = io.ReadByte();
					io.Position = 0;
					using (StreamReader rdr = new StreamReader(io, true))
					{
						content = rdr.ReadToEnd().TrimStart();
						if (tmpCh >= 0x0e0)
							encoding = rdr.CurrentEncoding;
					}
				}
			}
			return content;
		}

		static Regex FindYear = new Regex(@"(?<=[^\d])(?:19|20)\d{2}(?=[^\d])");

		static void InsertFileHeader(string fileName, ref string content, string insert)
		{
			string[] lines = insert.Replace("\r\n", "\n").Split('\n');
			string last = lines[lines.Length - 1];
			Check.Assert<ArgumentException>(lines[0].Contains("Copyright"));

			int ixCopyright = content.IndexOf("Copyright", StringComparison.OrdinalIgnoreCase);
			int ixFirstLine = content.IndexOf('\n');
			Match ixEnding = new Regex(String.Format(@"^{0}\s*$", last), RegexOptions.Multiline).Match(content);

			if (ixCopyright > 0 && ixCopyright > ixFirstLine)
			{
				Console.Error.WriteLine("Found Copyright? {0}", fileName);
				ixCopyright = -1;
			}
			if (ixCopyright > 0 && !ixEnding.Success)
				throw new ApplicationException("Unable to locate the end of Copyright notice.");

			if (ixCopyright > 0)
			{
				string temp = content.Substring(0, ixEnding.Index + ixEnding.Length).Trim();
				if (temp == insert)
					return; //already has the same copyright

				int ixYear = insert.IndexOf(DateTime.Now.Year.ToString());
				if (ixYear >= 0)
				{
					int fromYear = 0, minYearFrom = int.MaxValue;
					foreach (Match m in FindYear.Matches(temp))
						if (int.TryParse(m.Value, out fromYear) && fromYear > 1900)
							minYearFrom = Math.Min(minYearFrom, fromYear);

                    if (!String.IsNullOrEmpty(_subversion))
                        YearFromSubversion(fileName, ref minYearFrom);

				    if (minYearFrom < DateTime.Now.Year)
						insert = insert.Insert(ixYear, minYearFrom + "-");
				}
				if (temp == insert)
					return; //already has the same copyright

				content = content.Substring(ixEnding.Index + ixEnding.Length).TrimStart();
			}

			content = String.Format("{0}{1}{2}", insert, Environment.NewLine, content.TrimStart());
		}

        class OutputCapture
        {
            ManualResetEvent _outputComplete = new ManualResetEvent(false);
            StringWriter _output = new StringWriter();

            public OutputCapture(Process p )
            {
                p.OutputDataReceived += new DataReceivedEventHandler(OutputDataReceived);
                p.BeginOutputReadLine();
            }

            void OutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null)
                    _outputComplete.Set();
                else
                    _output.WriteLine(e.Data);
            }

            public string Output
            {
                get { _outputComplete.WaitOne(30000, false); return _output.ToString(); }
            }
        }

        static void YearFromSubversion(string fileName, ref int minYearFrom)
	    {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(_subversion);
                psi.Arguments = String.Format("log --xml --non-interactive \"{0}\"", fileName);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardInput = true;
                Trace.WriteLine(_subversion + " " + psi.Arguments);
                Process p = Process.Start(psi);
                p.StandardInput.Close();
                OutputCapture capture = new OutputCapture(p);
                p.WaitForExit(60000);
                if (p.ExitCode == 0)
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(capture.Output);
                    foreach (XmlElement e in doc.SelectNodes("/log/logentry/date"))
                    {
                        int tmp;
                        if (int.TryParse(e.InnerText.Substring(0, 4), out tmp) && tmp > 1900 && tmp <= DateTime.Now.Year)
                            minYearFrom = Math.Min(minYearFrom, tmp);
                    }
                }
                else
                    Trace.WriteLine(_subversion + " failed: " + p.ExitCode);
            }
            catch { return; }
	    }
	}
}
