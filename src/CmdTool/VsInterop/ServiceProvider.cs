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
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CSharpTest.Net.CustomTool.Interfaces;

namespace CSharpTest.Net.CustomTool.VsInterop
{
    internal class ServiceProvider : IServiceProvider, IObjectWithSite
    {

        private static Guid IID_IUnknown = new Guid("{00000000-0000-0000-C000-000000000046}");

        private object _serviceProvider;

        public ServiceProvider(object sp)
        {
            _serviceProvider = sp;
        }

        public virtual void Dispose()
        {
            _serviceProvider = null;
        }

        private static bool Failed(int hr)
        {
            return (hr < 0);
        }

        private static bool Succeeded(int hr)
        {
            return (hr >= 0);
        }

        public virtual object GetService(Type serviceClass)
        {
            if (serviceClass == null)
                return null;

            return GetService(serviceClass.GUID, serviceClass);
        }

        public virtual object GetService(Guid guid)
        {
            return GetService(guid, null);
        }

        private object GetService(Guid guid, Type serviceClass)
        {
            object service = null;

            if (guid.Equals(Guid.Empty))
                return null;

            if (guid.Equals(typeof (IOleServiceProvider).GUID))
                return _serviceProvider;
            if (guid.Equals(typeof (IObjectWithSite).GUID))
                return (IObjectWithSite) this;

            // Straight-forward COM interop, works with *most* visual studio solutions
            try
            {
                if (TryQueryService(_serviceProvider as IOleServiceProvider, guid, out service))
                    return service;
            }
            catch { }
            // Possible .NET implementation of Microsoft.VisualStudio.OLE.Interop.IServiceProvider
            try 
            {
                if (TryQueryService(CreateInteropServiceProvider(_serviceProvider), guid, out service))
                    return service;
            }
            catch { }
            // Last resort: crawl .NET fields of instance and see if we can find the original VStudio COM instance.
            try
            {
                if (_serviceProvider != null)
                {
                    foreach (IOleServiceProvider sp in StealServiceProvider(_serviceProvider, _serviceProvider.GetType()))
                    {
                        if (TryQueryService(sp, guid, out service))
                            return service;
                    }
                }
            }
            catch { }

            return null;
        }

        static bool TryQueryService(IOleServiceProvider svcs, Guid guid, out object service)
        {
            IntPtr pUnk;
            if (svcs != null)
            {
                int hr = svcs.QueryService(ref guid, ref IID_IUnknown, out pUnk);

                if (Succeeded(hr) && (pUnk != IntPtr.Zero))
                {
                    try { service = Marshal.GetObjectForIUnknown(pUnk); }
                    finally { Marshal.Release(pUnk); }
                    return true;
                }
            }
            service = null;
            return false;
        }

        static IOleServiceProvider CreateInteropServiceProvider(object serviceProvider)
        { return InteropServiceProvider.Create(serviceProvider); }

        // ROK - a total and complete hack.  Again I'm bitten by the lack of COM interface isolation in .NET, causing 
        // the need for us to use reflection to hopefully find the COM based implmenetation of the actual project item
        // site in a member field.  I don't know of a worse way to do this, but I don't know of a better one either.
        private static IEnumerable<IOleServiceProvider> StealServiceProvider(object instance, Type type)
        {
            foreach (FieldInfo fi in type.GetFields(BindingFlags.Instance | BindingFlags.Public | 
                                                    BindingFlags.NonPublic | BindingFlags.GetField))
            {
                IOleServiceProvider value = fi.GetValue(instance) as IOleServiceProvider;
                if (value != null)
                    yield return value;
            }

            foreach (IOleServiceProvider sp in StealServiceProvider(instance, type.BaseType))
                yield return sp;
        }

        void IObjectWithSite.GetSite(ref Guid riid, object[] ppvSite)
        {
            ppvSite[0] = GetService(riid);
        }

        void IObjectWithSite.SetSite(object pUnkSite)
        {
            _serviceProvider = pUnkSite;
        }
    }

    internal static class InteropServiceProvider
    {
        public static IOleServiceProvider Create(object serviceProvider)
        {
            if (serviceProvider is Microsoft.VisualStudio.OLE.Interop.IServiceProvider)
            {
                return new ConvertToOleServiceProvider(
                    (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)serviceProvider);
            }
            return null;
        }

        private class ConvertToOleServiceProvider : IOleServiceProvider
        {
            private readonly Microsoft.VisualStudio.OLE.Interop.IServiceProvider _provider;

            public ConvertToOleServiceProvider(Microsoft.VisualStudio.OLE.Interop.IServiceProvider provider)
            {
                _provider = provider;
            }

            public int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
            {
                return _provider.QueryService(ref guidService, ref riid, out ppvObject);
            }
        }
    }
}