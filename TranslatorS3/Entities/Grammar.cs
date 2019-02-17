using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using System.Xml.Linq;
using GrammarParser;

namespace TranslatorS3.Entities
{
    class Grammar : IEntity, IGrammar
    {
        public bool HasLeftRecursion { get; private set; }

        public INodeCollection Nodes { get; private set; }

        public IClassTable ClassTable { get; private set; }

        internal IEnumerable<string> UnclassifiedTerminals;

        public bool Load()
        {
            throw new NotImplementedException();

            try
            {
                var doc = XDocument.Load(Configuration.Path.GrammarXml);

                var root = doc.Element("grammar");

                UnclassifiedTerminals = root.Element("unclassified-terminals").Elements()
                    .Select(n => n.Value).ToList();

                var classTable = root.Element("class-table");

                var whiteDelimiters = classTable.Attribute("white-delimiters").Value
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\n", "\n");

                var unclassifiedTokenClass = classTable.Attribute("unclassified-token-class-name").Value;
                var undefinedTokenClass = classTable.Attribute("undefined-token-class-name").Value;

                var symbolClasses = classTable.Element("symbol-classes").Elements()
                    .Select(n => (n.Attribute("name").Value, n.Attribute("symbols").Value))
                    .ToDictionary(n => n.Item1, n => n.Item2);

                var tokenClasses = classTable.Element("token-classes").Elements()
                    .Select(n => (n.Attribute("name").Value, int.Parse(n.Attribute("id").Value)));

                ClassTable = new GrammarParser.ClassTable
                {
                    WhiteDelimiters = whiteDelimiters,
                    UnclassifiedTokenClassName = unclassifiedTokenClass,
                    UndefinedTokenClassName = undefinedTokenClass,
                    SymbolClasses = symbolClasses,
                    TokenClasses = new Map<string, int>(tokenClasses),
                };

                HasLeftRecursion = bool.Parse(root.Attribute("has-left-recursion").Value);

                var nodesElement = root.Element("nodes");


                var nodes = new Dictionary<int, INode>();


                var terminals = nodesElement.Elements("t");

                var classes = nodesElement.Elements("nt")
                    .Where(n => n.Attribute("is-class") != null);

                var definedTokens = nodesElement.Elements("nt")
                    .Where(n => n.Attribute("is-defined-token") != null);

                var mediums = nodesElement.Elements("nt")
                    .Except(classes)
                    .Except(definedTokens);

                foreach (var terminal in terminals)
                {
                    int id = int.Parse(terminal.Attribute("id").Value);

                    nodes[id] = new Terminal
                    {
                        Id = id,
                        IsControl = bool.Parse(terminal.Attribute("is-control").Value),
                        Name = terminal.Attribute("name").Value,
                    };
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

            throw new NotImplementedException();
        }

        public bool Parse(IParser parser)
        {
            dynamic result = parser.Parse();

            try
            {
                ClassTable = result.ClassTable;
                Nodes = new NodeCollection
                {
                    Axiom = result.Axiom,
                    SortedNodes = result.SortedNodes,
                    UnsortedNodes = result.UnsortedNodes,
                };
                HasLeftRecursion = result.HasLeftRecursion;

                UnclassifiedTerminals = result.UnclassifiedTerminals;

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Save()
        {
            throw new NotImplementedException();

            try
            {
                var root = new XElement("grammar");

                root.Add(new XAttribute("has-left-recursion", HasLeftRecursion));

                root.Add(new XElement("unclassified-terminals",
                    UnclassifiedTerminals.Select(n => new XElement("t", n))
                ));

                root.Add(new XElement("nodes",
                    Nodes.Mediums.Select(medium =>
                    {
                        var element = new XElement("nt",
                            new XAttribute("id", medium.Id)
                        );

                        if (medium.Name!= null)
                        {
                            element.Add(new XAttribute("name", medium.Name));
                        }

                        if(medium is IDefinedToken definedToken)
                        {
                            element.Add(
                                new XAttribute("is-defined-token", true),
                                new XAttribute("token-class-id", definedToken.TokenClassId)
                            );
                        }

                        if (medium is IClass @class)
                        {
                            element.Add(
                                new XAttribute("is-class", true),
                                new XAttribute("symbol-class-id", @class.SymbolClassId)
                            );
                        }

                        element.Add(medium.Cases.Select((IEnumerable<INode> m) =>
                        {
                            var @case = new XElement("case", m.Select(k => new XElement("node", new XAttribute("id", k.Id))));
                            return @case;
                        }));

                        element.Add(GetFactors(medium));
                        

                        return element;
                    }),
                    Nodes.Terminals.Select(n =>
                    {
                        var element = new XElement("t",
                            new XAttribute("id", n.Id),
                            new XAttribute("name", n.Name),
                            new XAttribute("is-control", n.IsControl)
                        );

                        return element;
                    })
                ));

                root.Add(new XElement("class-table",
                    new XAttribute("white-delimiters", 
                        ClassTable.WhiteDelimiters
                            .Replace("\n", "\\n")
                            .Replace("\r", "\\r")
                            .Replace("\n", "\\n")
                    ),
                    new XAttribute("unclassified-token-class-name", ClassTable.UnclassifiedTokenClassName),
                    new XAttribute("undefined-token-class-name", ClassTable.UndefinedTokenClassName),
                    new XElement("token-classes",
                        ClassTable.TokenClasses.Select(n => new XElement("class",
                            new XAttribute("id", n.Value),
                            new XAttribute("name", n.Key)
                        ))
                    ),
                    new XElement("symbol-classes",
                        ClassTable.SymbolClasses.Select(n => new XElement("class",
                            new XAttribute("name", n.Key),
                            new XAttribute("symbols", n.Value)
                        ))
                    )
                ));

                new XDocument(root).Save(Configuration.Path.GrammarXml);

                return true;

                IEnumerable<object> GetFactors(IFactor factor)
                {
                    var elements = new List<object>
                    {
                        new XAttribute("is-interruptable", factor.IsInterruptable),
                    };

                    if(factor.FactorCases != null)
                    {
                        elements.AddRange(factor.FactorCases.Select(n =>
                        {
                            var element = new XElement("f",
                                new XAttribute("node", n.Key.Id)
                            );

                            if(n.Value != null)
                            {
                                element.Add(GetFactors(n.Value));
                            }

                            return element;
                        }));
                    }
                    

                    if (factor.Recursion != null)
                    {
                        elements.AddRange(GetFactors(factor.Recursion));
                    }

                    return elements;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
           
        }
    }
}
