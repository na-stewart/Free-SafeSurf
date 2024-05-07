using System.Reflection;
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

namespace Enforcer
{
    internal class Config
    {
        readonly string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "SafeSurf.config");
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
            xml.Load(path);
        }

        public string Read(string key)
        {
            var node = xml.DocumentElement.SelectSingleNode(key);
            return node != null ? node.InnerText : string.Empty;
        }

        public void SetExpired()
        {
            xml.DocumentElement.SelectSingleNode("date-enforced").InnerText = string.Empty;
            xml.Save(path);
        }

        public bool HasExpired()
        {
            return Read("date-enforced").Equals("");
        }
    }
}
