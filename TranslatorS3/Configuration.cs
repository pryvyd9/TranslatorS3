using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Reflection;

namespace TranslatorS3
{
    [Category("config")]
    public static class Configuration
    {
        public static bool IsLoaded { get; private set; }

        public static string ConfigPath { get; } = "config.xml";


        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
        private class OptionAttribute : Attribute, ConfigView.IOptionAttribute
        {
            public string Name { get; }

            public bool RequiresRestart { get; } = true;

            public OptionAttribute(string name)
            {
                Name = name;
            }

            public OptionAttribute(string name, bool needsRestart)
            {
                Name = name;
                RequiresRestart = needsRestart;
            }
        }

        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
        private class CategoryAttribute : Attribute, ConfigView.ICategoryAttribute
        {
            public string Name { get; }

            public CategoryAttribute(string name)
            {
                Name = name;
            }
        }



        [Category("path")]
        public static class Path
        {
            [Option("grammar-input-xml")]
            public static string GrammarInputXml { get; set; } = "Input\\grammar-original.xml";

            [Option("grammar-xml")]
            public static string GrammarXml { get; set; } = "Config\\grammar.xml";

            [Option("grammar-txt")]
            public static string GrammarTxt { get; set; } = "Output\\grammar.txt";

            [Option("parsed-nodes-directory")]
            public static string ParsedNodesDirectory { get; set; } = "Output\\";

            [Option("grammar-factorized-txt")]
            public static string GrammarFactorizedTxt { get; set; } = "Output\\grammar-factorized.txt";

            [Option("script-txt")]
            public static string ScriptTxt { get; set; } = "Input\\script.txt";

            [Option("finite-automaton-xml")]
            public static string FiniteAutomatonXml { get; set; } = "Config\\finite-automaton.xml";

            [Option("pushdown-automaton-xml")]
            public static string PushdownAutomatonXml { get; set; } = "Config\\pushdown-automaton.xml";

            [Option("errors-txt")]
            public static string ErrorsTxt { get; set; } = "Output\\errors.txt";

            [Option("predescence-table-txt")]
            public static string PredescenceTableTxt { get; set; } = "Output\\predescence-table.txt";

            [Option("statement-rules-xml")]
            public static string StatementRulesXml { get; set; } = "Input\\statement-rules.xml";
        }

        [Category("parser")]
        public static class Parser
        {
            [Option("should-include-terminals-from-inside-of-defined-tokens")]
            public static bool ShouldIncludeTerminalsFromInsideOfDefinedTokens { get; set; } = false;

            [Option("should-convert-left-recursion-to-right")]
            public static bool ShouldConvertLeftRecursionToRight { get; set; } = true;

            [Option("should-ignore-undefined-tokens-in-syntax-parser")]
            public static bool ShouldIgnoreUndefinedTokensInSyntaxParser { get; set; } = true;
        }

        [Category("general")]
        public static class General
        {
            [Option("tab-indent", false)]
            public static int TabIndent { get; set; } = 4;

            /// <summary>
            /// Whether must print the actual structure of factor
            /// or put up a pretty facade.
            /// </summary>
            [Option("should-show-factor-naked")]
            public static bool ShouldShowFactorNaked { get; set; } = false;
        }





        private static XElement CategoryToXml(Type categoryType)
        {
            string categoryName = (categoryType.GetCustomAttribute(typeof(CategoryAttribute)) as CategoryAttribute).Name;

            XElement category = new XElement(categoryName);

            foreach (var optionProperty in categoryType.GetProperties())
            {
                string optionName = (optionProperty.GetCustomAttribute(typeof(OptionAttribute)) as OptionAttribute).Name;

                XElement option = new XElement(optionName);

                option.Add(new XAttribute("value", optionProperty.GetValue(null)));

                category.Add(option);
            }

            return category;
        }

        private static void ReadCategory(Type categoryType, XElement configElement)
        {
            string categoryName = (categoryType.GetCustomAttribute(typeof(CategoryAttribute)) as CategoryAttribute).Name;

            XElement category = configElement.Element(categoryName);

            if (category == null)
                return;

            foreach (var optionProperty in categoryType.GetProperties())
            {
                string optionName = (optionProperty.GetCustomAttribute(typeof(OptionAttribute)) as OptionAttribute).Name;

                XElement option = category.Element(optionName);


                if (option == null)
                    continue;


                var attribute = option.Attribute("value");


                if (attribute == null)
                    throw new Exception($"Empty option {optionName} was found in config file.");


                string stringValue = attribute.Value;

                object value = Convert.ChangeType(stringValue, optionProperty.PropertyType);

                optionProperty.SetValue(null, value);
            }
        }

        public static void Load()
        {
            IsLoaded = true;

            if (!System.IO.File.Exists(ConfigPath))
            {
                return;
            }

            XDocument document = XDocument.Load(ConfigPath);

            string rootName = (typeof(Configuration).GetCustomAttribute(typeof(CategoryAttribute)) as CategoryAttribute).Name;

            var root = document.Element(rootName);


            foreach (var category in typeof(Configuration).GetNestedTypes())
            {
                ReadCategory(category, root);
            }
        }

        public static void Save()
        {
            CreateDirectoryFromPath(ConfigPath);

            XDocument document = new XDocument();

            var attribute = typeof(Configuration).GetCustomAttribute(typeof(CategoryAttribute)) as CategoryAttribute;

            string rootName = attribute.Name;

            var root = new XElement(rootName);
            

            foreach (var category in typeof(Configuration).GetNestedTypes())
            {
                root.Add(CategoryToXml(category));
            }

            document.Add(root);

            document.Save(ConfigPath);
        }

        public static void CreateDirectoryFromPath(string path)
        {
            string directory = path.Substring(0, path.LastIndexOf('\\') + 1);

            if (string.IsNullOrWhiteSpace(directory))
                return;

            System.IO.Directory.CreateDirectory(directory);
        }


    }

}
