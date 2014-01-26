#region Copyright 2010-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Reflection;

namespace CSharpTest.Net.GeneratorsTest
{
    class TestResourceResult
    {
        public readonly Assembly Assembly;
        public readonly string Namespace;
        public readonly string ClassName;
        public string FullName { get { return String.Format("{0}.{1}", Namespace, ClassName).Trim('.'); } }

        public TestResourceResult(Assembly asm, string nameSpace, string className)
        {
            Assembly = asm;
            Namespace = nameSpace;
            ClassName = className;
        }

        public Type ResType
        {
            get { return Assembly.GetType(FullName, true); }
        }

        public PropertyInfo Property(string name)
        {
            PropertyInfo pi = ResType.GetProperty(name, BindingFlags.GetProperty | BindingFlags.Static | BindingFlags.Public);
            if (pi == null) throw new ArgumentException("Property " + name + " not found.");
            return pi;
        }

        public string GetValue(string name)
        {
            return (string)Property(name).GetValue(null, null);
        }

        delegate string MethodAction<T>(T name);
        MethodAction<T1> Method<T1>(string name)
        {
            MethodInfo mi = ResType.GetMethod(name, BindingFlags.GetProperty | BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(T1) }, null);
            if (mi == null) throw new ArgumentException("Method " + name + "(" + typeof(T1).Name + ") not found.");
            return (MethodAction<T1>)Delegate.CreateDelegate(typeof(MethodAction<T1>), mi, true);
        }

        public string GetValue<T1>(string name, T1 arg)
        {
            return Method<T1>(name)(arg);
        }

        public Exception CreateException(string name, params object[] arguments)
        {
            Type exType = Assembly.GetType(String.Format("{0}.{1}", Namespace, name), true);
            return (Exception)exType.InvokeMember(null, BindingFlags.CreateInstance | BindingFlags.Public | BindingFlags.Instance, null, null, arguments);
        }

        delegate void AssertAction<T1, T2>(T1 a, T2 b);
        public void Assert<T1>(string name, bool condition, T1 arg)
        {
            Type exType = Assembly.GetType(String.Format("{0}.{1}", Namespace, name), true);
            MethodInfo mi = exType.GetMethod("Assert", BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(bool), typeof(T1) }, null);
            if (mi == null) throw new ArgumentException("Method Assert on type " + name + " not found.");
            ((AssertAction<bool, T1>)Delegate.CreateDelegate(typeof(AssertAction<bool, T1>), mi, true))(condition, arg);
        }
    }
}