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
using CSharpTest.Net.Utils;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;

namespace CSharpTest.Net.StampVersion
{
	static class Program
	{
		static int DoHelp()
		{
			Console.WriteLine("");
			Console.WriteLine("Usage:");
			Console.WriteLine("    StampVersion.exe [/nologo] [/wait] /build:{Number}|{File Path} [/revision={Number}|{File Path}]");
            Console.WriteLine("");
            Console.WriteLine("        /version:{Version} - defines the entire version string, i.e. 1.0.0.0");
            Console.WriteLine("        /version:{File Path} - file path of a text file contain a line with:");
            Console.WriteLine("                       Version: {Version}");
            Console.WriteLine("");
            Console.WriteLine("        /fileversion:{Version} - defines the file version string if different");
            Console.WriteLine("        /fileversion:{File Path} - file path of a text file contain a line with:");
            Console.WriteLine("                       FileVersion: {Version}");
            Console.WriteLine("");
			Console.WriteLine("        /major:{Number} - defines the major (2th part) of the version");
			Console.WriteLine("        /major:{File Path} - file path of a text file contain a line with:");
			Console.WriteLine("                       Major: {Number}");
			Console.WriteLine("");
			Console.WriteLine("        /minor:{Number} - defines the minor (2th part) of the version");
			Console.WriteLine("        /minor:{File Path} - file path of a text file contain a line with:");
			Console.WriteLine("                       Minor: {Number}");
			Console.WriteLine("");
			Console.WriteLine("        /build:{Number} - defines the build (3th part) of the version");
			Console.WriteLine("        /build:{File Path} - file path of a text file contain a line with:");
			Console.WriteLine("                       Build: {Number}");
			Console.WriteLine("");
			Console.WriteLine("        /revision:{Number} - defines the revision (4th part) of the version");
			Console.WriteLine("        /revision:{File Path} - file path of a text file contain a line with:");
			Console.WriteLine("                       Revision: {Number}");
			Console.WriteLine("");
			Console.WriteLine("        /nologo Hide the startup message");
			Console.WriteLine("");
			Console.WriteLine("        /wait after processing wait for user input");
			return 0;
		}

		[STAThread]
		static int Main(string[] raw)
		{
			ArgumentList args = new ArgumentList(raw);

			using (Log.AppStart(Environment.CommandLine))
			{
				if (args.Contains("nologo") == false)
				{
					Console.WriteLine("StampVersion.exe");
					Console.WriteLine("Copyright 2009 by Roger Knapp, Licensed under the Apache License, Version 2.0");
					Console.WriteLine("");
				}

				if ((args.Unnamed.Count == 0 && args.Count == 0) || args.Contains("?") || args.Contains("help"))
					return DoHelp();

				try
				{
                    string major = null, minor = null, build = null, revision = null;
				    string version;
                    if(args.TryGetValue("version", out version))
                    {
                        string[] dotted = version.Split('.');
                        major = dotted[0];
                        minor = dotted.Length >= 1 ? dotted[1] : null;
                        build = dotted.Length >= 2 ? dotted[2] : null;
                        revision = dotted.Length >= 3 ? dotted[3] : null;
                    }

				    major = GetNumber("Major", args, major);
					minor = GetNumber("Minor", args, minor);
					build = GetNumber("Build", args, build);
					revision = GetNumber("Revision", args, revision);

					if (major == null && minor == null && build == null && revision == null)
						return DoHelp();

				    string fileversion = args.SafeGet("fileversion");

					FileList files = new FileList(@"AssemblyInfo.cs");

					Regex versionPattern = new Regex(@"[^a-z,A-Z,0-9](?<Type>AssemblyVersion|AssemblyFileVersion)\s*\(\s*\" + '\"' +
						@"(?<Version>[0-2]?[0-9]{1,9}\.[0-2]?[0-9]{1,9}(?:(?:\.[0-2]?[0-9]{1,9}(?:(?:\.[0-2]?[0-9]{1,9})|(?:\.\*))?)|(?:\.\*))?)\" + '\"' +
						@"\s*\)");

					foreach (FileInfo file in files.ToArray())
					{
						StringBuilder content = new StringBuilder();
						int lastpos = 0;
						string text = File.ReadAllText(file.FullName);
						foreach (Match match in versionPattern.Matches(text))
						{
							Group verMatch = match.Groups["Version"];
							content.Append(text, lastpos, verMatch.Index - lastpos);
							lastpos = verMatch.Index + verMatch.Length;

							string[] parts = verMatch.Value.Split('.');
							if( parts.Length < 2 )//regex should prevent us getting here
								throw new ApplicationException(String.Format("Bad version string: {0} on line {1}", verMatch.Value, content.ToString().Split('\n').Length));
							if (parts.Length < 3)
								parts = new string[] { parts[0], parts[1], "0" };
							else if( parts.Length == 3 && parts[2] == "*" )
								parts = new string[] { parts[0], parts[1], "0", "*" };
							if (parts.Length == 3 && revision != null)
								parts = new string[] { parts[0], parts[1], parts[2], "0" };
							if( build != null && build == "*" )
								parts = new string[] { parts[0], parts[1], "*" };

							if (major != null && parts.Length > 0)
								parts[0] = major;
							if (minor != null && parts.Length > 1)
								parts[1] = minor;
							if (build != null && parts.Length > 2)
								parts[2] = build;
							if (revision != null && parts.Length > 3)
								parts[3] = revision;

							//AssemblyFileVersion doesn't use '*', so trim off the build and/or revision
							if (match.Groups["Type"].Value == "AssemblyFileVersion")
							{
								if (parts.Length >= 4 && parts[3] == "*")
									parts = new string[] { parts[0], parts[1], parts[2] };
								if (parts.Length >= 3 && parts[2] == "*")
									parts = new string[] { parts[0], parts[1] };

							    if (!String.IsNullOrEmpty(fileversion))
							    {
							        parts = fileversion.Split('.');
							    }
							}

							string newVersion = String.Join(".", parts);
							//Console.WriteLine("Changed '{0}' to '{1}'", verMatch.Value, newVersion);
							content.Append(newVersion);
						}
						content.Append(text, lastpos, text.Length - lastpos);

					    if ((file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
					        file.Attributes = file.Attributes & ~FileAttributes.ReadOnly;
                            
						File.WriteAllText(file.FullName, content.ToString());
					}
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
			}

			if (args.Contains("wait"))
			{
				Console.WriteLine();
				Console.WriteLine("Press [Enter] to continue...");
				Console.ReadLine();
			}

			return Environment.ExitCode;
		}

		/// <summary>
		/// Retrieves only the numeric value specified or, if needed, reads the
		/// specified configuration file and parses.
		/// </summary>
        static string GetNumber(string optionName, ArgumentList args, string defaultValue)
		{
			ushort value;
			string text = args.SafeGet(optionName);

			if (String.IsNullOrEmpty(text)) return defaultValue;
			if (text == "*") return text;

			if (!ushort.TryParse(text, out value)) // not already a number?
			{
				if (File.Exists(text))
					text = ParseRevisionText(optionName, text); // read from file
				else
					text = String.Format(text, DateTime.Now); // format from date
			}

			try { text = ushort.Parse(text).ToString(); }//makes sure this is a number... 
			catch (Exception e) 
			{ throw new ApplicationException(String.Format("Number '{0}' is not valid: {1}", text, e.Message), e); }
			
			Console.WriteLine("{0} = {1}", optionName, text);
			return text;
		}

		static string ParseRevisionText(string optionName, string inputFile)
		{
			String regExpText = String.Format(@"^\s*{0}\s*[:,=]\s*(?<Data>\d+)\s*$", optionName);
			Regex exp = new Regex(regExpText, RegexOptions.Multiline | RegexOptions.IgnoreCase);
			try
			{
				Match match = exp.Match(File.ReadAllText(inputFile));
				if (match.Success == false || match.Groups["Data"].Success == false)
					throw new ApplicationException("Regex match failed.");

				return match.Groups["Data"].Value;
			}
			catch (Exception e)
			{ throw new ApplicationException(String.Format("Unable to parse input file, '{1}' not found: {0}", inputFile, optionName), e); }
		}
	}
}