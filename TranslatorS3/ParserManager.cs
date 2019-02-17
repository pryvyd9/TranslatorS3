using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using System.Reflection;


namespace TranslatorS3
{
    static class ParserManager
    {
        internal static ITokenParser TokenParser { get; set; }
        internal static ISyntaxParser SyntaxParser { get; set; }
        internal static ISemanticParser SemanticParser { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parserPath">Relative path to the dll file containing the parser.</param>
        /// <param name="typeName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        internal static IParser InitializeParser(string parserPath, string typeName, params object[] args)
        {
            var assembly = Assembly.LoadFile(Environment.CurrentDirectory + "/" + parserPath);

            var constructors = assembly.GetType(typeName).GetConstructors();

            int i = 0;

            var constructorToInvoke = constructors.Single(n => n.GetParameters()
                .All(m => 
                {
                    if(m.ParameterType.IsAssignableFrom(args[i].GetType())
                        || args[i].GetType().IsAssignableFrom(m.ParameterType))
                    {
                        i++;
                        return true;
                    }
                    i++;
                    return false;
                })
            );


            var parser = constructorToInvoke.Invoke(args);

            if (!(parser is IParser))
                throw new Exception("Created instance does not implement the IParser interface.");


            if (parser is ITokenParser tokenParser)
            {
                TokenParser = tokenParser;
            }
            else if (parser is ISyntaxParser syntaxParser)
            {
                SyntaxParser = syntaxParser;
            }
            else if (parser is ISemanticParser semanticParser)
            {
                SemanticParser = semanticParser;
            }

            return parser as IParser;
        }
    }
}
