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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Resources;

namespace CSharpTest.Net.Generators.ResX
{
    class ResXOptions
    {
        const string OptionPrefix = ".";
        string _defaultName;

        public Int32 NextMessageId = -1;
        public String HelpLinkFormat;
        public String EventSource;
        public String EventLog;
        public String EventMessageFormat;

        public Int32 FacilityId = -1;
        public String FacilityName;
        
        public Int32 EventCategoryId = -1;
        public String EventCategoryName;

        public bool AutoLog;
        public String AutoLogMethod;

        public IEnumerable<ResxGenItem> ReadFile(string filePath)
        {
            _defaultName = Path.GetFileNameWithoutExtension(filePath);
            bool dirty = false;
            string basePath = Path.GetDirectoryName(filePath);
            List<ResXDataNode> options = new List<ResXDataNode>();
            List<ResXDataNode> nodes = new List<ResXDataNode>();
            List<ResxGenItem> items = new List<ResxGenItem>();

            using (ResXResourceReader reader = new ResXResourceReader(filePath))
            {
                reader.BasePath = basePath;
                reader.UseResXDataNodes = true;
                foreach (DictionaryEntry entry in reader)
                {
                    ResXDataNode node = (ResXDataNode) entry.Value;

                    if (ReadOption(node))
                        options.Add(node);
                    else
                        nodes.Add(node);
                }
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                ResXDataNode node = nodes[i];
                ResxGenItem item = new ResxGenItem(this, node);

                if (ProcessItem(item, ref node, nodes))
                {
                    dirty = true;
                    nodes[i] = node;
                    item = new ResxGenItem(this, node);
                }

                items.Add(item);
            }

            if (dirty)
            {
                DateTime modified = new FileInfo(filePath).LastWriteTime;
                using (ResXResourceWriter writer = new ResXResourceWriter(filePath))
                {
                    writer.BasePath = basePath;

                    foreach (ResXDataNode node in UpdateOptions(options))
                        writer.AddResource(node);
                    foreach (ResXDataNode node in nodes)
                        writer.AddResource(node);

                    writer.Generate();
                }

                //This is stupid, but VStudio will continue to build if we don't
                new FileInfo(filePath).LastWriteTime = modified;
                Console.Error.WriteLine("Contents modified: {0}", filePath);
            }

            return items;
        }

        bool ReadOption(ResXDataNode data)
        {
            if (!data.Name.StartsWith(OptionPrefix))
                return false;
            string field = data.Name.Substring(OptionPrefix.Length);
            object value = data.GetValue(new System.Reflection.AssemblyName[0]);
            switch(field)
            {
                case "NextMessageId": NextMessageId = Check.InRange(Convert.ToInt32(value), 1, 0x0FFFF); break;
                case "HelpLink": HelpLinkFormat = Convert.ToString(value); break;
                case "EventMessageFormat": EventMessageFormat = Convert.ToString(value); break;
                case "AutoLog": AutoLog = Convert.ToBoolean(value); break;
                case "AutoLogMethod": AutoLogMethod = Convert.ToString(value); break;

                case "EventSource":
                    {
                        string[] parts = Convert.ToString(value).Split('/', '\\');
                        if (parts.Length == 1) parts = new string[] { "", parts[0] };
                        EventLog = parts[0];
                        EventSource = parts[1];
                        break;
                    }
                case "EventCategory":
                    {
                        EventCategoryId = Check.InRange(Convert.ToInt32(value), 1, 0x0FFFF);
                        EventCategoryName = !String.IsNullOrEmpty(data.Comment) ? data.Comment : _defaultName;
                        break;
                    }
                case "Facility":
                    {
                        FacilityId = Check.InRange(Convert.ToInt32(value), 256, 2047);
                        FacilityName = !String.IsNullOrEmpty(data.Comment) ? data.Comment : _defaultName;
                        break;
                    }
                default: return false;
            }
            return true;
        }

        IEnumerable<ResXDataNode> UpdateOptions(IEnumerable<ResXDataNode> allnodes)
        {
            foreach (ResXDataNode node in allnodes)
            {
                if (node.Name == OptionPrefix + "NextMessageId")
                {
                    yield return new ResXDataNode(OptionPrefix + "NextMessageId", NextMessageId);
                }
                else
                    yield return node;
            }
        }

        bool ProcessItem(ResxGenItem item, ref ResXDataNode node, List<ResXDataNode> allNodes)
        {
            if (NextMessageId < 0)
                return false;

            if (node.FileRef != null) return false;
            try
            {
                if (typeof(String) != Type.GetType(node.GetValueTypeName(new System.Reflection.AssemblyName[0])))
                    return false;
            }
            catch { return false; }

            string hr;
            if (!item.TryGetOption("MessageId", out hr))
            {
                bool hasOptions = node.Comment.IndexOf('#') >= 0;
                node.Comment = String.Format("{0}{1}MessageId={2}", node.Comment, !hasOptions ? " #" : ", ", NextMessageId++).Trim();
                return true;
            }

            return false;
        }
    }
}
