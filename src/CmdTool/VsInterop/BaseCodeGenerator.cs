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
using System.Runtime.InteropServices;
using Microsoft.Win32;
using CSharpTest.Net.CustomTool.Interfaces;

namespace CSharpTest.Net.CustomTool.VsInterop
{
    public abstract class BaseCodeGenerator : IVsSingleFileGenerator
    {
        private const int S_OK = 0;
        private const int E_ABORT = unchecked((int) 0x80004004);
        protected const int E_FAIL = unchecked((int) 0x80004005);
        protected const string DEFAULT_EXT = ".cs";

        private IVsGeneratorProgress codeGeneratorProgress;

        public virtual string GetExtension()
        {
            return DEFAULT_EXT;
        }

        protected abstract byte[] GenerateCode(string defaultNamespace, string inputFileName);

        protected virtual void OnBeforeGenerate(string defaultNamespace, string inputFileName)
        {
        }

        protected virtual void OnAfterGenerate(string defaultNamespace, string inputFileName)
        {
        }

        protected void Error(string message, int line, int column)
        {
            try
            {
                IVsGeneratorProgress progress = codeGeneratorProgress;
                if (progress != null)
                    progress.GeneratorError(false, 1, message, line, column);
            }
            catch { }
        }

        protected void Warning(string message, int line, int column)
        {
            try
            {
                IVsGeneratorProgress progress = codeGeneratorProgress;
                if (progress != null)
                    progress.GeneratorError(true, 1, message, line, column);
            }
            catch { }
        }

        string IVsSingleFileGenerator.GetDefaultExtension()
        {
            try
            {
                return GetExtension();
            }
            catch (System.Threading.ThreadAbortException)
            {
                throw;
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e.ToString(), GetType().FullName);
                return DEFAULT_EXT;
            }
        }

        int IVsSingleFileGenerator.Generate(string wszInputFilePath,
                                            string bstrInputFileContents,
                                            string wszDefaultNamespace,
                                            out IntPtr rgbOutputFileContents,
                                            out int pcbOutput,
                                            IVsGeneratorProgress pGenerateProgress)
        {
            rgbOutputFileContents = IntPtr.Zero;
            pcbOutput = 0;

            codeGeneratorProgress = pGenerateProgress;
            byte[] bytes = null;
            try
            {
                try
                {
                    OnBeforeGenerate(wszDefaultNamespace, wszInputFilePath);
                    bytes = GenerateCode(wszDefaultNamespace, wszInputFilePath);
                }
                finally
                {
                    try
                    {
                        OnAfterGenerate(wszDefaultNamespace, wszInputFilePath);
                    }
                    catch
                    {
                    }
                }
            }
            catch (System.Threading.ThreadAbortException)
            {
                throw;
            }
            catch (COMException)
            {
                throw;
            }
            catch (Exception e)
            {
                bytes = null;
                OnError(e);
                throw;
            }
            finally
            {
                codeGeneratorProgress = null;
            }

            if (bytes == null)
                return E_ABORT;

            pcbOutput = bytes.Length;
            rgbOutputFileContents = Marshal.AllocCoTaskMem(pcbOutput);
            Marshal.Copy(bytes, 0, rgbOutputFileContents, pcbOutput);
            return S_OK;
        }

        protected virtual void OnError(Exception e)
        {
            System.Diagnostics.Trace.WriteLine(e.ToString(), GetType().FullName);
            Error(e.Message, 0, 0);
        }

        #region COM Interop/Registration

        private static IEnumerable<string> GetRegistryKeysToAdd()
        {
            string[] versions = new string[] {"8.0", "9.0", "10.0", "11.0"};
            string[] languages = new string[]
                                     {
                                         /* CSharp */ "{FAE04EC1-301F-11D3-BF4B-00C04F79EFBC}",
                                                      /* CSEdit */ "{694DD9B6-B865-4C5B-AD85-86356E9C88DC}",
                                                      /* VBProj */ "{164B10B9-B200-11D0-8C61-00A0C91E29D5}",
                                                      /* VBEdit */ "{E34ACDC0-BAAE-11D0-88BF-00A0C9110049}",
                                                      /* JSProj */ "{E6FDF8B0-F3D1-11D4-8576-0002A516ECE8}",
                                                      /* JSEdit */ "{E6FDF88A-F3D1-11D4-8576-0002A516ECE8}",
                                     };
            foreach (string ver in versions)
                foreach (string lang in languages)
                    yield return String.Format(@"SOFTWARE\Microsoft\VisualStudio\{0}\Generators\{1}\", ver, lang);
        }

        /// <summary>
        /// Registeres this assembly with COM using the custom keys required for TortoiseSVN interop
        /// </summary>
        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                object[] attribs = t.GetCustomAttributes(typeof (GuidAttribute), true);
                if (attribs.Length == 0)
                    return;
                string GUID = "{" + ((GuidAttribute) attribs[0]).Value.ToUpper() + "}";

                foreach (string keypath in GetRegistryKeysToAdd())
                    using (RegistryKey key = Registry.LocalMachine.CreateSubKey(keypath + t.Name))
                    {
                        Check.Assert<UnauthorizedAccessException>(key != null);
                        key.SetValue("CLSID", GUID, RegistryValueKind.String);
                        key.SetValue("GeneratesDesignTimeSource", 1, RegistryValueKind.DWord);
                        key.Close();
                    }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Unregisteres this assembly removing the custom keys required for TortoiseSVN interop
        /// </summary>
        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                foreach (string keypath in GetRegistryKeysToAdd())
                    Registry.LocalMachine.DeleteSubKey(keypath + t.Name, false);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
        }

        #endregion
    }
}