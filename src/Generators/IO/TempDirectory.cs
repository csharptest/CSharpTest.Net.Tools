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
using System.IO;
using System.Diagnostics;
using System.Text;

namespace CSharpTest.Net.IO
{
    /// <summary>
    /// Provides a class for managing a temporary directory and making reasonable a attempt to remove it upon disposal.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{TempPath}")]
    public class TempDirectory : IDisposable
    {
        private string _temppath;

        /// <summary>
        /// Creates a new temp directory path that is not currently in use.
        /// </summary>
        /// <returns></returns>
        [DebuggerNonUserCode]
        static string CreatePath()
        {
            int attempt = 0;
            while(true)
            {
                try
                {
                    string path = Path.GetTempFileName();
                    if (File.Exists(path))
                        File.Delete(path);
                    if (Directory.Exists(path))
                        throw new IOException();
                    Directory.CreateDirectory(path);
                    return path;
                }
				catch (UnauthorizedAccessException)
				{
					if (++attempt < 10)
						continue;
					throw;
				}
                catch (IOException)
                {
                    if(++attempt < 10)
                        continue;
                    throw;
                }
            }
        }

        /// <summary>
        /// Attaches a new instances of a TempFile to the provided directory path
        /// </summary>
        public static TempDirectory Attach(string existingPath)
        {
            return new TempDirectory(existingPath);
        }
        /// <summary>
        /// Safely delete the provided directory name
        /// </summary>
        public static void Delete(string path)
        {
            bool exists = false;
            try { exists = !String.IsNullOrEmpty(path) && Directory.Exists(path); }
            catch (IOException) { }

            if (exists)
                new TempDirectory(path).Dispose();
        }
        /// <summary>
        /// Constructs a new temp directory with a newly created directory.
        /// </summary>
        public TempDirectory() : this(CreatePath()) { }
        /// <summary>
        /// Manage the provided directory path
        /// </summary>
        public TempDirectory(string directory)
        {
            TempPath = directory;
        }
        /// <summary>
        /// Removes the directory if Dispose() is not called
        /// </summary>
        ~TempDirectory() { try { Dispose(false); } catch { } }
        /// <summary>
        /// Returns the temporary directory path being managed.
        /// </summary>
        public string TempPath
        {
            [DebuggerNonUserCode]
            get
            {
                if (String.IsNullOrEmpty(_temppath))
                    throw new ObjectDisposedException(GetType().ToString());
                return _temppath;
            }
            protected set
            {
                if (Exists)
                    TempDirectory.Delete(_temppath);

                if (!String.IsNullOrEmpty(_temppath = value))
                    _temppath = Path.GetFullPath(_temppath);
            }
        }
        /// <summary> Disposes of the temporary directory </summary>
        public void Dispose() { Dispose(true); }
        /// <summary>
        /// Disposes of the temporary directory
        /// </summary>
        [DebuggerNonUserCode]
        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (_temppath != null && Exists)
                    Directory.Delete(_temppath, true);
                _temppath = null;

                if (disposing)
                    GC.SuppressFinalize(this);
            }
            catch (System.IO.IOException e)
            {
                string directoryname = _temppath;

                if (!disposing) //wait for next GC's collection
                {
                    new TempFile(directoryname);
                    _temppath = null;
                }

                Trace.TraceWarning("Unable to delete temp directory: {0}, reason: {1}", directoryname, e.Message);
            }
        }
        /// <summary>
        /// Detatches this instance from the temporary directory and returns the temp directory's path
        /// </summary>
        public string Detatch()
        {
            GC.SuppressFinalize(this);
            string name = _temppath;
            _temppath = null;
            return name;
        }
        /// <summary>
        /// Returns true if the current temp directory exists.
        /// </summary>
        [DebuggerNonUserCode]
        public bool Exists { get { return !String.IsNullOrEmpty(_temppath) && Directory.Exists(_temppath); } }
        /// <summary>
        /// Returns the FileInfo object for this temp directory.
        /// </summary>
        public DirectoryInfo Info { get { return new DirectoryInfo(TempPath); } }
        /// <summary>
        /// Deletes the current temp directory immediatly if it exists.
        /// </summary>
        public void Delete() { if (Exists) Directory.Delete(TempPath, true); }
        /// <summary>
        /// Copies the file content to the specified target file name
        /// </summary>
        public void CopyTo(string target) { DeepCopy(TempPath, target, false); }
        /// <summary>
        /// Copies the directory content to the specified target directory name
        /// </summary>
        public void CopyTo(string target, bool replace) { DeepCopy(TempPath, target, replace); }
        /// <summary>
        /// Creates a deep-copy of the directory contents
        /// </summary>
        public static void DeepCopy(string srcDirectory, string targetDirectory, bool replace)
        {
            Directory.CreateDirectory(targetDirectory);
            foreach(string file in Directory.GetFiles(srcDirectory))
                File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), replace);
            foreach (string dir in Directory.GetDirectories(srcDirectory))
                DeepCopy(dir, Path.Combine(targetDirectory, Path.GetFileName(dir)), replace);
        }
    }
}
