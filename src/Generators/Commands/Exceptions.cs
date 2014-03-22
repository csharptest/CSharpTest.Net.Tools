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

namespace CSharpTest.Net.Commands
{
	/// <summary>
	/// Base exception for assertions and errors encountered while processing commands
	/// </summary>
	[Serializable]
	[System.Diagnostics.DebuggerNonUserCode]
	public class InterpreterException : ApplicationException
	{
		/// <summary>
		/// Constructs an exception
		/// </summary>
		public InterpreterException(string text, params object[] format)
			: base(format.Length == 0 ? text : String.Format(text, format))
        { }
        /// <summary>
        /// Constructs an exception
        /// </summary>
        public InterpreterException(string text, Exception innerException)
            : base(text, innerException)
        { }
		/// <summary>
		/// Constructs an exception durring deserialization
		/// </summary>
		protected InterpreterException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
			: base(info, context)
		{ }
		/// <summary>
		/// Asserts the condition and throws on failure
		/// </summary>
		internal static void Assert(bool cond, string text, params object[] format)
		{ if (!cond) throw new InterpreterException(text, format); }
	}
}
