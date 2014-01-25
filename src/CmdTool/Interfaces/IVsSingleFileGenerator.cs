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
using System.Runtime.InteropServices;

namespace CSharpTest.Net.CustomTool.Interfaces
{
	[ComImport]
	[Guid("3634494C-492F-4F91-8009-4541234E4E99")]
	[InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
	interface IVsSingleFileGenerator
	{
		[return: MarshalAs(UnmanagedType.BStr)]
		string GetDefaultExtension();

        [PreserveSig()]
		int Generate([MarshalAs(UnmanagedType.LPWStr)] string wszInputFilePath,
				[MarshalAs(UnmanagedType.BStr)] string bstrInputFileContents,
				[MarshalAs(UnmanagedType.LPWStr)] string wszDefaultNamespace,
				out IntPtr rgbOutputFileContents,
				[MarshalAs(UnmanagedType.U4)] out int pcbOutput,
				IVsGeneratorProgress pGenerateProgress);
	}
}