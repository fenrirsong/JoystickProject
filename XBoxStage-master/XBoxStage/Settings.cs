using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace XBoxStage
{
    // Put all values to be saved between sessions into this object
    class Settings
    {
        private const string filename = @"settings.json";

        public Point pointA { get; set; }
        public Point pointB { get; set; }

        public static void Save (Settings settings)
        {
            File.WriteAllText(filename, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
        public static Settings Restore()
        {
            if (File.Exists(filename))
            {
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(filename)); ;
            }
            return new Settings();
        }
    }
}
