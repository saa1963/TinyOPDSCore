using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace TinyOPDSCore
{
    public static class Localizer
    {
        private static string _lang = "en";
        private static Dictionary<string, string> _translations = new Dictionary<string, string>();
        private static XDocument _xml = null;

        /// <summary>
        /// Static classes don't have a constructors but we need to initialize translations
        /// </summary>
        /// <param name="xmlFile">Name of xml translations, added to project as an embedded resource </param>
        public static void Init(string xmlFile = "translation.xml")
        {
            try
            {
                _xml = XDocument.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("TinyOPDS." + xmlFile));
            }
            catch (Exception e)
            {
                Log.WriteLine("Localizer.Init({0}) exception: {1}", xmlFile, e.Message);
            }
        }


        /// <summary>
        /// Returns supported translations in Dictionary<langCode, languageName>
        /// </summary>
        public static Dictionary<string, string> Languages
        {
            get
            {
                return _xml != null ? _xml.Descendants("language").ToDictionary(d => d.Attribute("id").Value, d => d.Value) : null;
            }
        }

        /// <summary>
        /// Current selected language
        /// </summary>
        public static string Language { get { return _lang; } }



        /// <summary>
        /// Translation helper
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string Text(string source)
        {
            return (_translations.ContainsKey(source)) ? _translations[source] : source;
        }


    }
}
