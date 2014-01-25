#region Copyright 2009-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CSharpTest.Net.Utils
{
	/// <summary>
	/// Utility class for obtaining information about the currently running
	/// process and AppDomain
	/// </summary>
	partial class ProcessInfo
	{
		/// <summary> Returns the string '[Unknown]'</summary>
		const string UNKNOWN = "[Unknown]";
		#region Process Data
		/// <summary> Returns the current process id </summary>
		public readonly Int32 ProcessId = 0;
		/// <summary> Returns the current process name without an extension </summary>
		public readonly string ProcessName = UNKNOWN;
		/// <summary> Returns the file path to the exe for this process </summary>
		public readonly string ProcessFile = UNKNOWN;
		#endregion
		#region AppDomain Data
		/// <summary> Returns the current AppDomain's friendly name </summary>
		public readonly string AppDomainName = UNKNOWN;
		/// <summary> Returns the entry-point assembly or the highest stack assembly </summary>
		public readonly Assembly EntryAssembly = typeof(ProcessInfo).Assembly;
		/// <summary> Returns the product version of the entry assembly </summary>
		public readonly Version ProductVersion= new Version();
		/// <summary> Returns the product name of the entry assembly </summary>
		public readonly string ProductName= UNKNOWN;
		/// <summary> Returns the company name of the entry assembly </summary>
		public readonly string CompanyName= UNKNOWN;
		#endregion
		#region Misc
		/// <summary> Returns true if a debugger is attached to the process </summary>
		public readonly bool IsDebugging = false;
		#endregion
		#region Derived Paths
		/// <summary>
		/// Returns the HKCU or HKLM path for this software application based
		/// on the process that is running: Software\{CompanyName}\{ProductName}
		/// </summary>
		public readonly string RegistrySoftwarePath = UNKNOWN;
		/// <summary>
		/// Returns the roaming user profile path for the currently running software
		/// application: {SpecialFolder.ApplicationData}\{CompanyName}\{ProductName}
		/// </summary>
		public readonly string ApplicationData = UNKNOWN;
		/// <summary>
		/// Returns the non-roaming user profile path for the currently running software
		/// application: {SpecialFolder.LocalApplicationData}\{CompanyName}\{ProductName}
		/// </summary>
		public readonly string LocalApplicationData = UNKNOWN;
		/// <summary>
		/// Returns a default log file name derived as:
		/// {SpecialFolder.LocalApplicationData}\{CompanyName}\{ProductName}\{AppDomainName}.txt
		/// </summary>
		public readonly string DefaultLogFile = UNKNOWN;
		#endregion

		/// <summary>
		/// This is some ugly code, the intent is to be able to answer the above questions in 
		/// a wide array of environments.  I admit now this may fail eventually.
		/// </summary>
		public ProcessInfo()
		{
			try
			{
				EntryAssembly = FindEntryAssembly();

				AssemblyName entryAsmName = EntryAssembly.GetName();
				ProcessName = entryAsmName.Name;
				ProcessFile = EntryAssembly.Location;

				ProductVersion = entryAsmName.Version;
				// read product name from attributes
				object[] attrs = EntryAssembly.GetCustomAttributes(typeof(AssemblyProductAttribute), true);
				if (attrs.Length > 0)
					ProductName = ((AssemblyProductAttribute)attrs[0]).Product;
				// read company name from attributes
				attrs = EntryAssembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), true);
				if (attrs.Length > 0)
					CompanyName = ((AssemblyCompanyAttribute)attrs[0]).Company;
			}
			catch { }
			try
			{
				//this can fail when not fully-trusted
				System.Diagnostics.Process thisProcess = System.Diagnostics.Process.GetCurrentProcess();

				ProcessId = thisProcess.Id;
				ProcessModule module = thisProcess.MainModule;

				if (!String.IsNullOrEmpty(module.ModuleName))
					ProcessName = thisProcess.MainModule.ModuleName;

				if (!String.IsNullOrEmpty(module.FileName))
					ProcessFile = thisProcess.MainModule.FileName;

				if (module.FileVersionInfo != null)
				{
					FileVersionInfo fver = module.FileVersionInfo;
					ProductVersion = new Version(fver.ProductMajorPart, fver.ProductMinorPart, fver.ProductBuildPart, fver.ProductPrivatePart);
					if(!String.IsNullOrEmpty(fver.CompanyName))
						CompanyName = fver.CompanyName;
					if (!String.IsNullOrEmpty(fver.ProductName))
						ProductName = fver.ProductName;
				}
			}
			catch { }
			try { IsDebugging = System.Diagnostics.Debugger.IsAttached; }
			catch { }
			try { AppDomainName = AppDomain.CurrentDomain.FriendlyName; }
			catch { }
			if (String.IsNullOrEmpty(AppDomainName))
				AppDomainName = ProcessName;
			
			//Before we go further, we are going to make sure that the following fields are safe
			//for use in file system api calls by removing the following characters: /\:*?'"<>|
			ProcessName = SafeName(ProcessName);
			AppDomainName = SafeName(AppDomainName);
			ProductName = SafeName(ProductName);
			CompanyName = SafeName(CompanyName);

			RegistrySoftwarePath = String.Format(@"Software\{0}\{1}", CompanyName, ProductName);

			try
			{
				ApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				ApplicationData = Path.Combine(Path.Combine(ApplicationData, CompanyName), ProductName);
			}
			catch
			{
				try { ApplicationData = Path.Combine(Path.Combine(Path.GetTempPath(), CompanyName), ProductName); }
				catch { ApplicationData = null; } //you have no access to files
			}

			try
			{
				LocalApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				LocalApplicationData = Path.Combine(Path.Combine(LocalApplicationData, CompanyName), ProductName);
				DefaultLogFile = Path.Combine(LocalApplicationData, AppDomainName + ".txt");
			}
			catch
			{
				try 
				{ 
					LocalApplicationData = Path.Combine(Path.Combine(Path.GetTempPath(), CompanyName), ProductName);
					DefaultLogFile = Path.Combine(LocalApplicationData, AppDomainName + ".txt");
				}
				catch { DefaultLogFile = LocalApplicationData = null; } //you have no access to files
			}
		}

		/// <summary>
		/// Copy from StringUtils
		/// </summary>
		private static string SafeName(string name)
		{
			StringBuilder sbName = new StringBuilder();
			foreach (char ch in name)
			{
				if (ch >= ' ' && ch != '/' && ch != '\\' && ch != ':' &&
					ch != '*' && ch != '?' && ch != '\'' && ch != '"' &&
					ch != '<' && ch != '>' && ch != '|' && !Char.IsControl(ch))
					sbName.Append(ch);
			}
			return sbName.ToString();
		}

		private static Assembly FindEntryAssembly()
		{
			Assembly asm = Assembly.GetEntryAssembly();

			if (asm != null)
				return asm;

			// Find the first non-Microsoft assembly on the stack
			try
			{
				StackTrace trace = new StackTrace(false);
				int count = trace.FrameCount;
				while (count > 0)
				{
					StackFrame frame = trace.GetFrame(count - 1);//top of the call stack
					asm = frame.GetMethod().ReflectedType.Assembly;
					//first non-Microsoft assembly will have to do...
					object[] attrs = asm.GetCustomAttributes(typeof(AssemblyCompanyAttribute), true);
					if (attrs.Length > 0 && ((AssemblyCompanyAttribute)attrs[0]).Company.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
						count--;
					else
						break;
				}

				if(asm != null)
					return asm;

				if (null != (asm = Assembly.GetCallingAssembly()))
					return asm;
			}
			catch 
			{ }

			return typeof(ProcessInfo).Assembly;//all else fails...
		}
	}
}
