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
using System.Reflection;

namespace CSharpTest.Net.Commands
{
	class CommandFilter : Command, ICommandFilter
	{
		static readonly char[] DefaultKey = new char[] { '*' };
		readonly Char[] _keys;

		public static bool TryCreate(object target, MethodInfo mi, out ICommand command)
		{
			command = null;
			ParameterInfo[] args = mi.GetParameters();
			if (args.Length == 3 &&
				args[0].ParameterType == typeof(ICommandInterpreter) &&
				args[1].ParameterType == typeof(ICommandChain) &&
				args[2].ParameterType == typeof(string[]))
			{
				ExecuteFilter filter = (ExecuteFilter)Delegate.CreateDelegate(typeof(ExecuteFilter), target is Type ? null : target, mi, false);
				if (filter != null)
					command = new CommandFilter(target, mi, filter);
			}

			return command != null;
		}

		/// <summary> Returns the possible character keys for this filter when setting the precedence </summary>
		public Char[] Keys { get { return (Char[])_keys.Clone(); } } 
		
		delegate void ExecuteFilter(ICommandInterpreter ci, ICommandChain chain, string[] args);
		readonly ExecuteFilter _filterProc;

		CommandFilter(object target, MethodInfo mi, ExecuteFilter filter)
			: base(target, mi)
		{
			_filterProc = filter;

			foreach (CommandFilterAttribute a in mi.GetCustomAttributes(typeof(CommandFilterAttribute), true))
				_keys = a.Keys;

			if (_keys == null || _keys.Length == 0)
				_keys = DefaultKey;
		}

		public void Run(ICommandInterpreter ci, ICommandChain chain, string[] arguments)
		{
			_filterProc(ci, chain, arguments);
		}

		public override void Run(ICommandInterpreter interpreter, string[] arguments)
		{
			Run(interpreter, null, arguments);
		}
	}

	class FilterChainItem : ICommandChain
	{
		ICommandInterpreter _ci;
		ICommandFilter _filter;
		ICommandChain _next;

		public FilterChainItem(ICommandInterpreter ci, ICommandFilter filter, ICommandChain next)
		{
			_ci = ci;
			_filter = filter;
			_next = next;
		}

		public void Next(string[] arguments)
		{
			_filter.Run(_ci, _next, arguments);
		}
	}

	class LastFilter : ICommandChain
	{
		readonly CommandInterpreter _ci;
		public LastFilter(CommandInterpreter ci)
		{ _ci = ci; }

		void ICommandChain.Next(string[] arguments)
		{
			_ci.ProcessCommand(arguments);
		}
	}
}
