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

namespace CSharpTest.Net
{
	/// <summary>
	/// This class name is actually wrong... These values are only constant for the life the current
	/// app domain ;)
	/// </summary>
	public static class Constants
	{
		/// <summary> Returns the string '[Unknown]'</summary>
		public const string UNKNOWN = "[Unknown]";
		#region Process Data
		/// <summary> Returns the current process id </summary>
		public static readonly Int32 ProcessId = 0;
		/// <summary> Returns the current process name without an extension </summary>
		public static readonly string ProcessName = UNKNOWN;
		/// <summary> Returns the file path to the exe for this process </summary>
		public static readonly string ProcessFile = UNKNOWN;
		#endregion
		#region AppDomain Data
		/// <summary> Returns the current AppDomain's friendly name </summary>
		public static readonly string AppDomainName = UNKNOWN;
		/// <summary> Returns the entry-point assembly or the highest stack assembly </summary>
		public static readonly Assembly EntryAssembly = typeof(Constants).Assembly;
		/// <summary> Returns the product version of the entry assembly </summary>
		public static readonly Version ProductVersion = new Version();
		/// <summary> Returns the product name of the entry assembly </summary>
		public static readonly string ProductName = UNKNOWN;
		/// <summary> Returns the company name of the entry assembly </summary>
		public static readonly string CompanyName = UNKNOWN;
		/// <summary> Returns true if the current process is running a unit test </summary>
		public static readonly bool IsUnitTest;
		#endregion
		#region Misc
		/// <summary> Returns true if a debugger is attached to the process </summary>
		public static readonly bool IsDebugging = false;
		#endregion
		#region Derived Paths
		/// <summary>
		/// Returns the HKCU or HKLM path for this software application based
		/// on the process that is running: Software\{CompanyName}\{ProductName}
		/// </summary>
		public static readonly string RegistrySoftwarePath = UNKNOWN;
		/// <summary>
		/// Returns the roaming user profile path for the currently running software
		/// application: {SpecialFolder.ApplicationData}\{CompanyName}\{ProductName}
		/// </summary>
		public static readonly string ApplicationData = UNKNOWN;
		/// <summary>
		/// Returns the non-roaming user profile path for the currently running software
		/// application: {SpecialFolder.LocalApplicationData}\{CompanyName}\{ProductName}
		/// </summary>
		public static readonly string LocalApplicationData = UNKNOWN;
		/// <summary>
		/// Returns a default log file name derived as:
		/// {SpecialFolder.LocalApplicationData}\{CompanyName}\{ProductName}\{AppDomainName}.txt
		/// </summary>
		public static readonly string DefaultLogFile = UNKNOWN;
		#endregion

		/// <summary>
		/// This is some ugly code, the intent is to be able to answer the above questions in 
		/// a wide array of environments.  I admit now this will fail eventually.
		/// </summary>
		static Constants()
		{
			CSharpTest.Net.Utils.ProcessInfo info = null;
			try { info = new CSharpTest.Net.Utils.ProcessInfo(); }
			catch { return; }

			ProcessId = info.ProcessId;
			ProcessName = info.ProcessName;
			ProcessFile = info.ProcessFile;
			AppDomainName = info.AppDomainName;
			EntryAssembly = info.EntryAssembly;
			ProductVersion = info.ProductVersion;
			ProductName = info.ProductName;
			CompanyName = info.CompanyName;
			IsDebugging = info.IsDebugging;
			RegistrySoftwarePath = info.RegistrySoftwarePath;
			ApplicationData = info.ApplicationData;
			LocalApplicationData = info.LocalApplicationData;
			DefaultLogFile = info.DefaultLogFile;

			IsUnitTest = (ProcessName.IndexOf("NUnit", StringComparison.OrdinalIgnoreCase) >= 0);
		}
	}
}
