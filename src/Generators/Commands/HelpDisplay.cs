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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;

namespace CSharpTest.Net.Commands
{
	partial class CommandInterpreter
    {
        /// <summary> Display the Help text to Console.Out </summary>
        [IgnoreMember]
        public void Help(string name) { Help(name, false); }

        /// <summary> Returns the Help as HTML text </summary>
        [IgnoreMember]
        public string GetHtmlHelp(string name)
        {
            ICommand cmd;
            IOption opt;
            if (name != null && _commands.TryGetValue(name, out cmd))
                return GenerateHtml(cmd);
            else if (name != null && _options.TryGetValue(name, out opt))
                return GenerateHtml(opt);
            else
            {
                List<IDisplayInfo> list = new List<IDisplayInfo>(Options);
                list.AddRange(Commands);
                return GenerateHtml(list.ToArray());
            }
        }

		/// <summary> Display the Help text to Console.Out </summary>
        [Command("Help", "-?", "/?", "?", "--help", Category = "Built-in", Description = "Gets the help for a specific command or lists available commands.")]
		public void Help(
			[Argument("name", "command", "c", "option", "o", Description = "The name of the command or option to show help for.", DefaultValue = null)] 
			string name,
            [Argument("html", DefaultValue = false, Description = "Output the full help content to HTML and view in the local browser.")]
            bool viewAsHtml
			)
		{
			ICommand cmd;
			IOption opt;
            if (name != null && _commands.TryGetValue(name, out cmd))
            {
                DisplayHelp(viewAsHtml, cmd);
            }
            else if (name != null && _options.TryGetValue(name, out opt))
            {
                DisplayHelp(viewAsHtml, opt);
            }
            else
            {
                List<IDisplayInfo> list = new List<IDisplayInfo>(Options);
                list.AddRange(Commands);
                DisplayHelp(viewAsHtml, list.ToArray());
            }
		}

        private void DisplayHelp(bool viewAsHtml, params IDisplayInfo[] items)
        {
            if (!viewAsHtml)
            {
                ShowHelp(items);
            }
            else
            {
                var path = Path.Combine(Path.GetTempPath(), AppDomain.CurrentDomain.FriendlyName + ".htm");
                File.WriteAllText(path, GenerateHtml(items));
                System.Diagnostics.Process.Start(path);
            }
        }

	    private string GenerateHtml(params IDisplayInfo[] items)
	    {
	        Assembly exec = Assembly.GetEntryAssembly()
	                        ?? Assembly.GetCallingAssembly();

	        var programName = Process.GetCurrentProcess().ProcessName;

            using (StringWriter sw = new StringWriter())
            using (XmlTextWriter w = new XmlTextWriter(sw))
            {
                w.Formatting = System.Xml.Formatting.Indented;
                w.WriteStartElement("html");
                w.WriteStartElement("head");
                {
                    w.WriteElementString("title", programName + " Help");

                    w.WriteStartElement("link");
                    w.WriteAttributeString("rel", "stylesheet");
                    w.WriteAttributeString("href", "http://netdna.bootstrapcdn.com/bootstrap/3.1.1/css/bootstrap.min.css");
                    w.WriteEndElement();

                    //w.WriteStartElement("link");
                    //w.WriteAttributeString("rel", "stylesheet");
                    //w.WriteAttributeString("href", "http://netdna.bootstrapcdn.com/bootstrap/3.1.1/css/bootstrap-theme.min.css");
                    //w.WriteEndElement();

                    w.WriteStartElement("style");
                    w.WriteAttributeString("type", "text/css");
                    w.WriteString(@"
body { padding: 20px; }
blockquote { font-size: inherit; }
");
                    w.WriteEndElement();
                }
                w.WriteEndElement();
                w.WriteStartElement("body");
                {
                    w.WriteElementString("h1", "Usage:");
                    w.WriteStartElement("p");
                    w.WriteElementString("pre", String.Format("C:\\> {0} COMMAND [arguments]", programName));
                    w.WriteEndElement();
                    w.WriteElementString("p", System.Diagnostics.FileVersionInfo.GetVersionInfo(exec.Location).Comments);

                    var options = new List<IOption>();
                    var commands = new List<ICommand>();

                    foreach (IDisplayInfo info in items)
                    {
                        if (info.Visible && info is IOption)
                            options.Add((IOption)info);
                        else if (info.Visible && info is ICommand)
                            commands.Add((ICommand)info);
                    }
                    items = null;

                    if (options.Count > 0)
                    {
                        w.WriteElementString("h1", "Options:");
                        w.WriteStartElement("ul");
                        foreach (IOption option in options)
                        {
                            w.WriteStartElement("li");
                            w.WriteElementString("strong", String.Format("/{0}={1}", option.AllNames[0], option.Type.Name));
                            w.WriteRaw(" ");
                            w.WriteString(option.Description.TrimEnd('.'));
                            w.WriteString(".");
                            w.WriteEndElement();

                        }
                        w.WriteEndElement();
                    }
                    if (commands.Count > 0)
                    {
                        w.WriteElementString("h1", "Commands:");
                        foreach (ICommand command in commands)
                        {
                            w.WriteElementString("h3", command.DisplayName);
                            int argCount = 0;
                            w.WriteStartElement("blockquote");
                            {
                                w.WriteElementString("p", command.Description.TrimEnd('.') + ".");
                                w.WriteElementString("strong", "Usage:");
                                w.WriteStartElement("p");
                                w.WriteStartElement("pre");
                                w.WriteString(String.Format("C:\\> {0} {1} ", programName, command.AllNames[0].ToUpper()));
                                foreach (IArgument arg in command.Arguments)
                                {
                                    if (arg.Visible == false || arg.IsInterpreter)
                                        continue;
                                    if (arg.IsAllArguments)
                                    {
                                        w.WriteString("[argument1] [argument2] [etc]");
                                        continue;
                                    }
                                    argCount++;
                                    w.WriteString(String.Format("{0} ", arg.FormatSyntax(arg.DisplayName)));
                                }
                                w.WriteEndElement();
                                w.WriteEndElement();
                            }
                            if (argCount > 0)
                            {
                                w.WriteStartElement("p");
                                w.WriteElementString("strong", "Arguments:");
                                w.WriteEndElement();
                                w.WriteStartElement("ul");
                                foreach (IArgument arg in command.Arguments)
                                {
                                    if (arg.Visible == false || arg.IsInterpreter)
                                        continue;
                                    if (arg.IsAllArguments)
                                        continue;
                                    w.WriteStartElement("li");
                                    w.WriteElementString("strong", arg.FormatSyntax(arg.DisplayName));
                                    if (!arg.Required && arg.DefaultValue != null)
                                        w.WriteString(String.Format(" = ({0})", arg.DefaultValue));
                                    w.WriteString(" - " + arg.Description.TrimEnd('.') + ".");
                                    w.WriteEndElement();
                                }
                                w.WriteEndElement();
                            }

                            w.WriteEndElement();
                            //w.WriteStartElement("hr");
                            //w.WriteEndElement();
                        }
                        w.WriteEndElement();
                    }
                    w.WriteStartElement("p");
                    w.WriteStartElement("hr");
                    w.WriteEndElement();

                    var entryAssembly = Assembly.GetEntryAssembly();
                    if (entryAssembly != null)
                    {
                        w.WriteStartElement("div");
                        w.WriteAttributeString("class", "text-muted");
                        {
                            w.WriteString(String.Format("{0}", entryAssembly.GetName()));
                            w.WriteStartElement("br");
                            w.WriteEndElement();
                            foreach (AssemblyCopyrightAttribute a in entryAssembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false))
                                w.WriteString(a.Copyright);
                            w.WriteStartElement("br");
                            w.WriteEndElement();
                        }
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }
                w.WriteEndElement();
                w.Flush();
                return sw.ToString();
            }
        }

        /// <summary>
        /// Can be overridden to control or rewrite help output
        /// </summary>
        protected virtual void ShowHelp(IDisplayInfo[] items)
        {
            if (items.Length == 1)
                items[0].Help();
            else
                ShowHelpFor(items);
        }

	    private void ShowHelpFor(IDisplayInfo[] items)
		{
			Dictionary<string, List<IDisplayInfo>> found = new Dictionary<string, List<IDisplayInfo>>(StringComparer.OrdinalIgnoreCase);
			foreach (IDisplayInfo item in items)
			{
				if (!item.Visible)
					continue;

				List<IDisplayInfo> list;
				string group = item is Option ? "Options" : "Commands"/*item.Category*/;

				if (!found.TryGetValue(group, out list))
					found.Add(group, list = new List<IDisplayInfo>());
				if (!list.Contains(item))
					list.Add(item);
			}

			List<string> categories = new List<string>(found.Keys);
			categories.Sort();
			foreach (string cat in categories)
			{
				Console.Out.WriteLine("{0}:", cat);
				found[cat].Sort(new OrderByName<IDisplayInfo>());

                int indent = 6;
			    foreach (IDisplayInfo info in found[cat])
			    {
			        if (info.DisplayName.Length > indent)
			            indent = info.DisplayName.Length;
			    }
			    string fmt = "  {0," + indent + "}:  {1}";
                foreach (IDisplayInfo info in found[cat])
                {
			        Console.Out.WriteLine(fmt, info.DisplayName.ToUpper(), info.Description);
			    }
			    Console.WriteLine();
			}
		}
	}

	partial class Command
	{
	    public void Help()
		{
			Console.WriteLine();
			foreach (string name in this.AllNames)
			{
				Console.Write("{0} ", name.ToUpper());
			    foreach (IArgument arg in Arguments)
                {
                    if (arg.Visible == false || arg.IsInterpreter)
						continue;
					if (arg.IsAllArguments)
					{
						Console.Write("[argument1] [argument2] [etc]"); 
						continue;
					}

					Console.Write("{0} ", arg.FormatSyntax(arg.DisplayName));
				}

				Console.WriteLine();
			}

			//Console.WriteLine();
			//Console.WriteLine("Category: {0}", this.Category);
			//Console.WriteLine("Type: {0}", this.target);
			//Console.WriteLine("Prototype: {0}", this.method);
			Console.WriteLine();
			Console.WriteLine(this.Description);
			Console.WriteLine();

			bool startedArgs = false;
			foreach (IArgument arg in Arguments)
            {
                if (arg.Visible == false || arg.IsInterpreter || arg.IsAllArguments)
					continue;
				if (!startedArgs)
				{
					Console.WriteLine("Arguments:");
					Console.WriteLine();
					startedArgs = true;
				}
				arg.Help();
			}
		}
	}

	partial class Argument
	{
		public string FormatSyntax(string name)
		{
			StringBuilder sb = new StringBuilder();
			if (!Required) sb.Append('[');
			if (!IsFlag) sb.Append('[');
			sb.Append('/');
			sb.Append(name);
            if (!IsFlag) sb.AppendFormat("=]{0}", UnderlyingType.Name);
			if (!Required) sb.Append(']');
			return sb.ToString();
		}

		public void Help()
		{
			Console.Write("  {0}", FormatSyntax(DisplayName));

            if (!Required && !IsFlag && DefaultValue != null)
				Console.Write(" ({0})", this.DefaultValue);

			List<string> alt = new List<string>(AllNames);
			alt.Remove(DisplayName);
			if( alt.Count > 0 )
				Console.Write(" [/{0}{1}]", String.Join("=|/", alt.ToArray()), IsFlag ? String.Empty : "=");

			Console.Write(" {0}", this.Description);
			Console.WriteLine();
		}
	}

	partial class Option
	{
		public void Help()
		{
			Console.WriteLine();
			foreach (string name in this.AllNames)
			{
				Console.WriteLine("GET {0}", name.ToUpper());
				Console.WriteLine("SET {0} [value]", name.ToUpper());
			}

			//Console.WriteLine();
			//Console.WriteLine("Category: {0}", this.Category);
			//Console.WriteLine("Type: {0}", this.target);
			//Console.WriteLine("Prototype: {0}", this.Property);
			Console.WriteLine();
			Console.WriteLine(this.Description);
			Console.WriteLine();
		}
	}

}
