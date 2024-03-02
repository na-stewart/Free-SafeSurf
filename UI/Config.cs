using System.Xml;

namespace UI
{
    internal class Config
    {
        private string configFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\CBEnforcer.config";
        private XmlDocument xml = new XmlDocument();
        private static Config instance = null;

        public static Config Instance
        {
            get
            {
                if (instance == null)
                    instance = new Config();
                return instance;
            }
        }

        private Config()
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
            var rootNode = xml.DocumentElement;
            var node = rootNode.SelectSingleNode(parsedKey);
            if (node == null)
            {
                node = xml.CreateElement(parsedKey);
                rootNode.AppendChild(node);
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