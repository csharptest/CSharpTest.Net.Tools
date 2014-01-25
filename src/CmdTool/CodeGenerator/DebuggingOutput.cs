#region Copyright 2010-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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

namespace CSharpTest.Net.CustomTool.CodeGenerator
{
	class DebuggingOutput : IDisposable
	{
		private readonly bool _enabled;
		private readonly Action<string> _output;

		public DebuggingOutput(bool isDebugEnabled, Action<string> output)
		{
			_output = output;
			_enabled = isDebugEnabled;
		}

		public void WriteLine(string format, params object[] args)
		{
			if (_enabled)
			{
				_output(String.Format("Verbose - {0}", String.Format(format, args)));
				//Log.Verbose(format, args);
			}
		}

		public void Dispose()
		{
		}
	}
}
