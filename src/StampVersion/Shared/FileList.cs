#region Copyright 2008-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.IO;

namespace CSharpTest.Net.Utils
{
	/// <summary>
	/// A utility class for gathering files
	/// </summary>
	[System.Diagnostics.DebuggerNonUserCode]
	partial class FileList : System.Collections.ObjectModel.KeyedCollection<string, FileInfo>
	{
		bool _recurse = true;
		bool _ignoreDirAttrs = false;
		FileAttributes _prohibitAttrib = FileAttributes.Hidden | FileAttributes.Offline | FileAttributes.System;

		/// <summary>
		/// Creates an empty FileList
		/// </summary>
		public FileList()
		{ }

		/// <summary>
		/// Constructs a FileList containing the files specified or found within the directories
		/// specified.  See Add(string) for more details.
		/// </summary>
        public FileList(params string[] filesOrDirectories)
			: base(StringComparer.OrdinalIgnoreCase, 0)
		{
			Add(filesOrDirectories);
		}

		/// <summary>
		/// Constructs a FileList containing the files specified or found within the directories
		/// specified.  See Add(string) for more details.  Files and directories that contain the 
		/// attribtes defined in prohibitedAttributes will be ignored, use '0' for everything.
		/// </summary>
        public FileList(FileAttributes prohibitedAttributes, params string[] filesOrDirectories)
			: base(StringComparer.OrdinalIgnoreCase, 0)
		{
			_prohibitAttrib = prohibitedAttributes;
			Add(filesOrDirectories);
		}

		/// <summary>
		/// Creates a list containing the specified FileInfo records.
		/// </summary>
        public FileList(params FileInfo[] copyFrom)
		{
			if (copyFrom == null) throw new ArgumentNullException();
			foreach (FileInfo finfo in copyFrom)
				AddFile(finfo);
		}
		#region Public Properties
		/// <summary>
		/// Gets or sets a value that allows traversal of all directories added.
		/// </summary>
		public bool RecurseFolders { get { return _recurse; } set { _recurse = value; } }
		/// <summary>
		/// Setting this will greatly improve performance at the cost of not evaluating filters on directories
		/// </summary>
		public bool IgnoreFolderAttributes { get { return _ignoreDirAttrs; } set { _ignoreDirAttrs = value; } }
		/// <summary>
		/// Set this to the set of attributes that if a directory or file contains should be skipped. For
		/// example when set to FileAttributes.Hidden, hidden files and folders will be ignored.
		/// </summary>
		public FileAttributes ProhibitedAttributes { get { return _prohibitAttrib; } set { _prohibitAttrib = value; } }
		#endregion Public Properties

		/// <summary>
		/// Adds a set of items to the collection, see Add(string) for details.
		/// </summary>
		public void Add(params string[] filesOrDirectories)
		{
			if (filesOrDirectories == null) throw new ArgumentNullException();
			foreach (string fd in filesOrDirectories)
				Add(fd);
		}

		/// <summary>
		/// Adds the specified file to the collection.  If the item specified is a directory
		/// that directory will be crawled for files, and optionally (RecurseFolders) child
		/// directories.  If the name part of the path contains wild-cards they will be
		/// considered throughout the folder tree, i.e: C:\Temp\*.tmp will yeild all files
		/// having an extension of .tmp.  Again if RecurseFolders is true you will get all
		/// .tmp files anywhere in the C:\Temp folder.
		/// </summary>
		public void Add(string fileOrDirectory)
		{
			if (fileOrDirectory == null) throw new ArgumentNullException();

			if (!Path.IsPathRooted(fileOrDirectory))
				fileOrDirectory = Path.Combine(Environment.CurrentDirectory, fileOrDirectory);

			if (File.Exists(fileOrDirectory))
				AddFile(new FileInfo(fileOrDirectory));
			else if (Directory.Exists(fileOrDirectory))
				AddDirectory(new DirectoryInfo(fileOrDirectory), "*");
			else
			{
				string filePart = Path.GetFileName(fileOrDirectory);
				string dirPart = Path.GetDirectoryName(fileOrDirectory);

				//if it is a valid directory and the file exists in the search area, then pass
				//it on to the filters, if it doesn't exist throw not found.
				if (Directory.Exists(dirPart) && (filePart.IndexOfAny(new char[] { '?', '*' }) >= 0 ||
					Directory.GetFiles(dirPart, filePart, RecurseFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Length > 0
					) )
				{
					AddDirectory(new DirectoryInfo(dirPart), filePart);
				}
				else
					throw new FileNotFoundException("File not found.", fileOrDirectory);
			}
		}

		/// <summary>
		/// Returns true if the given file is in the collection
		/// </summary>
		public new bool Contains(FileInfo file)
		{
			return Dictionary != null && Dictionary.ContainsKey(file.FullName);
		}

		/// <summary>
		/// Adds one or files to the collection
		/// </summary>
		public void AddRange(params FileInfo[] files)
		{
			foreach (FileInfo f in files)
			{
				if (!Contains(f))
					Add(f);
			}
		}

		/// <summary>
		/// Remove the files specified if they exist in the collection
		/// </summary>
		public void Remove(params FileInfo[] files)
		{
			foreach (FileInfo finfo in files)
				base.Remove(finfo.FullName);
		}

		/// <summary>
		/// Returns the collection of FileInfo as an array
		/// </summary>
		public FileInfo[] ToArray()
		{
			return new List<FileInfo>(base.Items).ToArray();
		}

        /// <summary>
        /// Converts all FileInfo elements into their fully-qualified file names
        /// </summary>
		public string[] GetFileNames()
		{
			if (base.Dictionary == null) 
				return new string[0];
			return new List<String>(base.Dictionary.Keys).ToArray();
		}

		#region Private / Protected Implementation

		private void AddFile(FileInfo file)
		{
			if (!Allowed(file) || (base.Dictionary != null && base.Dictionary.ContainsKey(file.FullName)))
				return;

			if (FileFound != null)
			{
				FileFoundEventArgs args = new FileFoundEventArgs(false, file);
				FileFound(this, args);
				if (args.Ignore)
					return;
			}
			base.Add(file);
		}

		private void AddDirectory(DirectoryInfo dir, string match)
		{
			if (!_ignoreDirAttrs && !Allowed(dir))
				return;

			SearchOption deepMatch = SearchOption.TopDirectoryOnly;
			if (_recurse && (_ignoreDirAttrs == true || _prohibitAttrib == 0))
				deepMatch = SearchOption.AllDirectories;

			foreach (FileInfo f in dir.GetFiles(match, deepMatch))
				AddFile(f);

			if (_recurse && deepMatch != SearchOption.AllDirectories)
			{
				foreach (DirectoryInfo child in dir.GetDirectories())
					AddDirectory(child, match);
			}
		}

		private bool Allowed(FileSystemInfo item)
		{
			if ((_prohibitAttrib & item.Attributes) != 0)
				return false;

			return true;
		}

        /// <summary>
        /// The key for the specified element.
        /// </summary>
		protected sealed override string GetKeyForItem(FileInfo item)
		{
			if (item == null) throw new ArgumentNullException();
			return item.FullName;
		}

		#endregion

		#region FileFoundEvent

		/// <summary>
		/// Raised when a new file is about to be added to the collection, set e.Ignore
		/// to true will cancel the addition of this file.
		/// </summary>
		public event EventHandler<FileFoundEventArgs> FileFound;

		/// <summary>
		/// Event args passed to the FileFound event
		/// </summary>
		public class FileFoundEventArgs : EventArgs
		{
			/// <summary>
			/// Allows manually filtering a file by setting Ignore=true;
			/// </summary>
			public bool Ignore;
			/// <summary>
			/// Provides access to the FileInfo of this item
			/// </summary>
			public readonly FileInfo File;

			/// <summary>
			/// Constructs the event args
			/// </summary>
			public FileFoundEventArgs(bool ignore, FileInfo file)
			{
				this.Ignore = ignore;
				this.File = file;
			}
		}

		#endregion FileFoundEvent
	}
}
