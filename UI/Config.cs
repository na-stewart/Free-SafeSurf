using System.Xml;

/*
MIT License

Copyright (c) 2024 Nicholas Aidan Stewart

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace UI
{
    internal class Config
    {
        readonly string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SafeSurf.config");
        readonly XmlDocument xml = new();
        static Config? instance = null;

        public static Config Instance
        {
            get
            {
                instance ??= new Config();
                return instance;
            }
        }

        Config()
        {
            if (File.Exists(configFile))
                xml.Load(configFile);
            else
            {
                XmlElement root = xml.CreateElement("Config");
                xml.AppendChild(root);
                xml.Save(configFile);
            }
        }

        public void Write(string key, string value)
        {
            var parsedKey = key.ToLower().Replace(" ", "-");
            var node = xml.DocumentElement.SelectSingleNode(parsedKey);
            if (node == null)
            {
                node = xml.CreateElement(parsedKey);
                xml.DocumentElement.AppendChild(node);
            }
            node.InnerText = value;
            xml.Save(configFile);
        }

        public string Read(string key)
        {
            var node = xml.DocumentElement.SelectSingleNode(key.ToLower().Replace(" ", "-"));
            return node != null ? node.InnerText : "";
        }
    }
}