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
using CSharpTest.Net.Utils;
using System.Diagnostics;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;

namespace CSharpTest.Net.Commands
{
	[System.Diagnostics.DebuggerDisplay("{Method}")]
	partial class Command : DisplayInfoBase, ICommand
	{
		Dictionary<string, int> _names;
		Argument[] _arguments;

		public static ICommand Make(object target, MethodInfo mi)
		{
			ICommand cmd;
			if (CommandFilter.TryCreate(target, mi, out cmd))
				return cmd;
			return new Command(target, mi);
		}

		protected Command(object target, MethodInfo mi)
			: base(target, mi)
		{
			ParameterInfo[] paramList = mi.GetParameters();

			_names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			List<Argument> tempList = new List<Argument>();

			foreach (ParameterInfo pi in paramList)
			{
				Argument arg = new Argument(target, pi);
				foreach(string name in arg.AllNames)
					_names.Add(name, tempList.Count);
				tempList.Add(arg);
			}
			_arguments = tempList.ToArray();

			if (Description == mi.ToString())
            {//if no description provided, let's build a better one
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("{0} ", this.DisplayName);
                foreach(Argument a in tempList)
					if(a.Visible)
						sb.AppendFormat("{0} ", a.FormatSyntax(a.DisplayName));
                _description = sb.ToString(0, sb.Length - 1);
            }
		}

		public IArgument[] Arguments { get { return (Argument[])_arguments.Clone(); } }

		private MethodInfo Method { get { return (MethodInfo)base.Member; } }

		public virtual void Run(ICommandInterpreter interpreter, string[] arguments)
		{
			ArgumentList args = new ArgumentList(arguments);

			if (args.Count == 1 && args.Contains("?"))
			{ Help(); return; }

			//translate ordinal referenced names
		    Argument last = null;
			for (int i = 0; i < _arguments.Length && args.Unnamed.Count > 0; i++)
			{
			    if (_arguments[i].Type == typeof (ICommandInterpreter))
			        break;
			    last = _arguments[i];
                args.Add(last.DisplayName, args.Unnamed[0]);
				args.Unnamed.RemoveAt(0);
			}

            if (last != null && args.Unnamed.Count > 0 && last.Type.IsArray)
		    {
                for (int i = 0; i < _arguments.Length && args.Unnamed.Count > 0; i++)
                {
                    args.Add(last.DisplayName, args.Unnamed[0]);
                    args.Unnamed.RemoveAt(0);
                }
		    }

		    List<object> invokeArgs = new List<object>();
			foreach (Argument arg in _arguments)
			{
				object argValue = arg.GetArgumentValue(interpreter, args, arguments);
				invokeArgs.Add(argValue);
			}

			//make sure we actually used all arguments.
			List<string> names = new List<string>(args.Keys);
			InterpreterException.Assert(names.Count == 0, "Unknown argument(s): {0}", String.Join(", ", names.ToArray()));
			InterpreterException.Assert(args.Unnamed.Count == 0, "Too many arguments supplied.");

			Invoke(Method, Target, invokeArgs.ToArray());
		}

		[System.Diagnostics.DebuggerNonUserCode]
		[System.Diagnostics.DebuggerStepThrough]
		private static void Invoke(MethodInfo method, Object target, params Object[] invokeArgs)
		{
			try
			{
				method.Invoke(target, invokeArgs);
			}
			catch (TargetInvocationException te) 
			{
				if (te.InnerException == null)
					throw;
				Exception innerException = te.InnerException;

				ThreadStart savestack = Delegate.CreateDelegate(typeof(ThreadStart), innerException, "InternalPreserveStackTrace", false, false) as ThreadStart;
				if(savestack != null) savestack();
				throw innerException;// -- now we can re-throw without trashing the stack
			}
		}
	}
}
