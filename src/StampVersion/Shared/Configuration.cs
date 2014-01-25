#region Copyright 2008-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Configuration;
using System.IO;

namespace CSharpTest.Net.Utils
{
	/// <summary>
	/// This is basically a big hack on the whole configuration system, it's purpose is to avoid
	/// the entire thing.  Where argument T is any [XmlSerializable] object this class can deserialize
	/// it from the configuration file.  It looks for an xsd either embeded into typeof(T).Assembly or
	/// in the local filesystem.  The Xsd must be named typeof(T).FullName + ".xsd" to be found by this
	/// class.  If found validation will occur durring the deserialization process and an exception of
	/// type XmlException() will be raised on errors.  Optionally, you can directly set the schema via
	/// the static XmlSchema property.
	/// </summary>
	[System.Diagnostics.DebuggerNonUserCode]
	partial class XmlConfiguration<T> : ConfigurationSection
	{
        /// <summary>
        /// Allows explicit setting of the XmlSchema to use when validating the xml input.
        /// </summary>
		public static XmlSchema XmlSchema = null;// <-- allows external setting
		static readonly XmlSerializer Serializer = new XmlSerializer(typeof(T));
		
		string _schemaName;
		T _data = default(T);

        /// <summary>
        /// Constructs the XmlConfiguration element assuming that the derived type name
        /// is the same name as the xsd file name, i.e. "MyCfg : XmlConfiguration" would
        /// expect an XSD named "MyCfg.xsd" to either exist on disk or be embeded into
        /// the type's containing assembly.
        /// </summary>
		public XmlConfiguration() : this(typeof(T).FullName + ".xsd"){}
        /// <summary>
        /// Explicitly sets the xsd file name to use.
        /// </summary>
		public XmlConfiguration(string schemaName)
		{ 
			_schemaName = schemaName; 
		}

		/// <summary>
		/// Allows access to the deserialized data
		/// </summary>
		public T Settings { get { return _data; } }

		/// <summary>
		/// Provides a derived class with the ability to do post-read validation not represented in
		/// the xsd (or in place of an xsd).
		/// </summary>
		protected virtual T ReadComplete(T data) { return data; }

		/// <summary>
		/// The main work goes here, builds the reader and validator and deserializes the object.
		/// </summary>
		protected override void DeserializeSection(System.Xml.XmlReader reader)
		{
			_data = ReadXml(_schemaName, reader);
			_data = ReadComplete(_data);

			base.SetReadOnly();
		}

		/// <summary>
		/// Reads and extracts the configuration settings from the current application's configuraiton file
		/// </summary>
		public static T ReadConfig(string sectionName)
		{
			return (T)((XmlConfiguration<T>)System.Configuration.ConfigurationManager.GetSection(sectionName));
		}

		/// <summary>
		/// Deserialize the xml configuration directly from an XmlReader instance
		/// </summary>
		public static T ReadXml(System.Xml.XmlReader reader)
		{ return ReadXml(typeof(T).FullName + ".xsd", reader); }

		/// <summary>
		/// Deserialize the xml configuration directly from an XmlReader instance
		/// </summary>
		public static T ReadXml(string schemaFile, System.Xml.XmlReader reader)
		{
			List<string> parseErrors = new List<string>();

			System.Xml.Schema.XmlSchema schema = XmlSchema;
			if (schema == null)
			{
				Stream schemaIo = null;
				string schemaLocation = schemaFile;

				// Try to read from three places in this order:
				// 1 - the application's base directory
				// 2 - the environment's current directory
				// 3 - for type T the declaring assembly's resource manifest
				if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, schemaLocation)))
					schemaLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, schemaLocation);
				if (File.Exists(schemaLocation))
					schemaIo = File.Open(schemaLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				else if (null == (schemaIo = typeof(T).Assembly.GetManifestResourceStream(schemaLocation)))
				{
					//maybe unqualified:
					string tmpSchemaName = String.Format("{0}.{1}", typeof(T).Namespace, schemaLocation);
					schemaIo = typeof(T).Assembly.GetManifestResourceStream(tmpSchemaName);
				}

				if (schemaIo != null) // if we found an xml schema, use it for validation
				{
					using (XmlTextReader rdr = new XmlTextReader(schemaIo))
					{
						schema = XmlSchema.Read(rdr, null);
					}
				}
			}

			XmlReaderSettings settings = new XmlReaderSettings();
			settings.CheckCharacters = true;
			settings.CloseInput = false;
			settings.ValidationEventHandler += new ValidationEventHandler(
				delegate(object sender, ValidationEventArgs args)
				{
					string message = String.Format("{0} ({1},{2}): {3}", 
						args.Severity, 
						args.Exception.LineNumber, 
						args.Exception.LinePosition, 
						args.Message
					);
					System.Diagnostics.Trace.WriteLine(message, typeof(T).FullName);
					parseErrors.Add(message);
				}
			);

			if (schema != null)
			{
				settings.Schemas.Add(schema);
				//these options would be nice to allow; however, these will cause a duplicate defininition
				//when the root node already defines a valid xsd and we internally rem our own.
				settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings; 
				settings.ValidationType = ValidationType.Schema;
			}

			XmlReader validation = XmlReader.Create(reader, settings);
			T data = (T)Serializer.Deserialize(validation);

			if (data == null || parseErrors.Count > 0)
				throw new XmlException(String.Join(Environment.NewLine, parseErrors.ToArray()));

			return data;
		}

        /// <summary>
        /// Allows implicit casting of the configuration element to the actual type contained.
        /// </summary>
		public static implicit operator T(XmlConfiguration<T> o) { return o == null ? default(T) : o._data; }
	}
}