using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Enforcer
{
    internal class Config
    {
        private string configFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\SafeSurf.config";
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
            var node = xml.DocumentElement.SelectSingleNode(key);
            return node != null ? node.InnerText : "";
        }
    }
}
