using System.Xml;

namespace svchost
{
    public  class Config
    {
        private string configFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\CBEnforcer.config";
        private XmlDocument xml = new XmlDocument();
        private static Config instance = null;


        public string ConfigFile
        {
            get => configFile;
        }

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
        }

        public string Read(string key)
        {
            var node = xml.DocumentElement.SelectSingleNode(key.Replace(" ", ""));
            return node != null ? node.InnerText : "";
        }
    }
}
