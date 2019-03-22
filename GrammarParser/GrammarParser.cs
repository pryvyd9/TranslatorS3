using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Core;
using System.Xml.Linq;


namespace GrammarParser
{
    /// <summary>
    /// Gets xml file with grammar and parses it into a tree.
    /// </summary>
    public class GrammarParser : IParser<GrammarParserResult>
    {
        private readonly string grammarXmlFilePath;
        private readonly bool shouldIncludeTerminalsFromInsideOfDefinedTokens;
        private readonly bool shouldConvertLeftRecursionToRight;

        /// <summary>
        /// List of classes and characters removed from them.
        /// </summary>
        private Dictionary<string, List<char>> classesWithRemovedSymbols;

        private List<INode> nodes;
        private Medium axiom;
        private ClassTable classTable;
        private string unclassifiedTokenClassName;
        private string unsupportedTokenClassName;
        private List<string> unclassifiedTerminals;

        private XElement rootElement;







        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="grammarXmlFilePath">Path to xml file
        /// containing the grammar to parse.</param>
        /// <param name="shouldIncludeTerminalsFromInsideOfDefinedTokens">If should
        /// include terminals from inside os defined tokens in the class table.</param>
        /// <param name="shouldConvertLeftRecursionToRight">If should convert
        /// left recursion to right.</param>
        public GrammarParser(string grammarXmlFilePath, 
            bool shouldIncludeTerminalsFromInsideOfDefinedTokens,
            bool shouldConvertLeftRecursionToRight)
        {
            this.grammarXmlFilePath = grammarXmlFilePath;
            this.shouldIncludeTerminalsFromInsideOfDefinedTokens = shouldIncludeTerminalsFromInsideOfDefinedTokens;
            this.shouldConvertLeftRecursionToRight = shouldConvertLeftRecursionToRight;
        }



        public GrammarParserResult Parse()
        {
            rootElement = XDocument.Load(grammarXmlFilePath).Element("g");

            nodes = new List<INode>();
            classesWithRemovedSymbols = new Dictionary<string, List<char>>();

            unclassifiedTokenClassName = rootElement
                .Attribute("unclassified-token-class-name").Value;

            unsupportedTokenClassName = rootElement
                .Attribute("unsupported-token-class-name").Value;

            var firstDefinition = rootElement.Element("d");
            axiom = (Medium)ParseNode(firstDefinition, false);

            // Make copy of nodes list to print 
            // factorized grammar in right order later.
            var unsortedNodesCopy = nodes.ToList();

            // Sort nodes.
            nodes.Sort(new NodeComparer());






            classTable = new ClassTable
            {
                UnclassifiedTokenClassName = unclassifiedTokenClassName,
                UndefinedTokenClassName = unsupportedTokenClassName,
                WhiteDelimiters = rootElement
                    .Attribute("white-delimiters")
                    .Value
                    .Replace("\\t", "\t")
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r"),
                SymbolClasses = ParseClasses(rootElement),
                TokenClasses = GetTokenClassesIndices(),
            };

            FactorizeAllNonTerminals();

            // Set indices now.
            FillIndices();

            // Concat copies with created mediums while removing left recursion.
            var unsortedCopyWithMediums = unsortedNodesCopy.Concat(nodes.Except(unsortedNodesCopy).OfType<IMedium>().OrderBy(n => n.Name));



            GrammarParserResult result = new GrammarParserResult
            {
                ClassTable = classTable,
                HasLeftRecursion = !shouldConvertLeftRecursionToRight,

                SortedNodes = nodes,
                UnsortedNodes = unsortedCopyWithMediums.ToList(),
                Axiom = axiom,
                UnclassifiedTerminals = unclassifiedTerminals,
                Errors = null,
            };

            return result;
        }

        private void FillIndices()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is Node n)
                {
                    n.Id = i;
                }
                else
                {
                    (nodes[i] as Factor).Id = i;
                }
            }
        }

        private Map<string, int> GetTokenClassesIndices()
        {
            int i = 0;

            Map<string, int> indexer = new Map<string, int>();

            indexer.Add(unclassifiedTokenClassName, i++);

            foreach (var token in nodes.OfType<DefinedToken>())
            {
                indexer.Add(token.TokenClass, i++);
                token.TokenClassId = indexer.Forward(token.TokenClass);
            }

            indexer.Add(unsupportedTokenClassName, -1);

            return indexer;
        }

        #region Factorization

        /// <summary>
        /// Converts left recursive sequences in cases in to right recursive
        /// with adding medium FactorNode.
        /// </summary>
        /// <remarks>
        /// Works only with recursions where node references itself.
        /// </remarks>
        /// <example>
        /// A::=B|AcB|AeB|D
        /// A::=B|D{cB|eB}
        /// 
        /// A->B
        /// A->D
        /// A->AcB
        /// A->AeB
        /// 
        /// A->B
        /// A->BX
        /// X->cBX
        /// X->cB
        /// 
        /// X->eBX
        /// X->eB
        /// 
        /// A->D
        /// A->DY
        /// Y->cBY
        /// Y->cB
        /// 
        /// Y->eBY
        /// Y->eB
        /// 
        /// A::=B(X|^)|D(Y|^)
        /// X::=cB(X|^)|eB(X|^)
        /// Y::=cB(Y|^)|eB(Y|^)
        /// 
        /// where X and Y are medium FactorNodes.
        /// </example>
        private void GetRidOfLeftRecursion()
        {
            // Create a copy of the list as its original will be changed
            // in process; hence foreach won't be able to iterate through it.
            List<Medium> nodes = this.nodes.OfType<Medium>().ToList();


            // First get rid of left recursion
            foreach (Medium node in nodes)
            {
                // Nodes with first lexem equal to the node
                var recursive = node.Cases.Where(n => n.Count() > 0 && n.ElementAt(0) == node);


                // If there are recursive sequences(cases)
                if (recursive.Count() != 0)
                {
                    int mediumIndex = 0;

                    // Non-recursive sequences(cases)
                    // Create a list to be able to add new elements.
                    var nonRecursiveSequences = node.Cases.Except(recursive).ToList();

                    if (nonRecursiveSequences.Count == 0)
                    {
                        throw new Exception("Cannot transform left recursion to right " +
                            "if there is no non-left-recursive sequences.");
                    }

                    // Create a copy of the list as its original will be changed
                    // in process; hence foreach won't be able to iterate through it.
                    var nonRecursiveSequencesToIterate = nonRecursiveSequences.ToList();

                    // When creating medium the beheaded is required.
                    // Look example.
                    var beheadedRecursive = recursive.Select(n => n.Skip(1).ToList());

                    foreach (var nonRecursiveSequence in nonRecursiveSequencesToIterate)
                    {
                        // Create new medium node.
                        Medium mediumNode = new Medium
                        {
                            Name = $"{node.Name}({mediumIndex})",
                        };

                        // Add medium to general node list.
                        this.nodes.Add(mediumNode);
                        mediumIndex++;


                        var mediumCases = new List<List<INode>>();

                        // X->cBX, X->cB and X->eBX, X->eB sequences.
                        foreach (var recursiveSequence in beheadedRecursive)
                        {
                            // X->cBX
                            var newSequence = recursiveSequence.ToList();
                            newSequence.Add(mediumNode);

                            mediumCases.Add(newSequence);

                            // X->cB
                            mediumCases.Add(recursiveSequence.ToList());
                        }

                        mediumNode.Cases = mediumCases;

                        // A->BX sequence.
                        // Create a copy as we will add new element to the sequence
                        // and want the original to stay unchanged.
                        var newNonRecursiveSequence = nonRecursiveSequence.ToList();
                        newNonRecursiveSequence.Add(mediumNode);

                        // Add A->BX sequence to non-recursive cases of A.
                        nonRecursiveSequences.Add(newNonRecursiveSequence);
                    }

                    node.Cases = nonRecursiveSequences;
                }

            }
        }

        /// <summary>
        /// Factorize all non-terminals
        /// </summary>
        /// <remarks>
        /// Divides sequence even if there are several have to stay together.
        /// 
        /// If shouldConvertLeftRecursionToRight then first gets rid of 
        /// left recursion and then factorizes.
        /// </remarks>
        /// <example>
        /// abcd|abhh -> a(b(c(d)|h(h)))
        /// abcd|ab -> a(b(c(d)|^))
        /// </example>
        private void FactorizeAllNonTerminals()
        {
            if (shouldConvertLeftRecursionToRight)
            {
                GetRidOfLeftRecursion();

                var nodesToFactorize = nodes.OfType<Medium>().ToList();

                foreach (Medium node in nodesToFactorize)
                {
                    var (factorCases, isInterruptable) = Factorize(node.Cases);

                    node.FactorCases = factorCases;
                    node.IsInterruptable = isInterruptable;
                }
            }
            else
            {
                var mediums = nodes.OfType<Medium>().ToList();

                foreach (Medium node in mediums)
                {
                    // Nodes with first lexem equal to the node
                    var recursive = node.Cases.Where(n => n.Count() > 0 && n.ElementAt(0) == node);

                    // If there are recursive sequences(cases)
                    if (recursive.Count() != 0)
                    {
                        // Non-recursive sequences(cases)
                        var nonRecursiveSequences = node.Cases.Except(recursive);

                        var (factorCases, isInterruptable) = Factorize(nonRecursiveSequences);

                        node.FactorCases = factorCases;
                        node.IsInterruptable = isInterruptable;

                        // Nodes with first node removed
                        var beheadedRecursive = recursive.Select(n => n.Skip(1));

                        var (nextFactorCases, isNextFactorInterruptable) = Factorize(beheadedRecursive);

                        Factor nextFactor = new Factor
                        {
                            FactorCases = nextFactorCases,
                            IsInterruptable = isNextFactorInterruptable,
                        };

                        nodes.Add(nextFactor);

                        node.Recursion = nextFactor;
                    }
                    else
                    {
                        var (factorCases, isInterruptable) = Factorize(node.Cases);

                        node.FactorCases = factorCases;
                        node.IsInterruptable = isInterruptable;
                    }
                }

            }
        }

        /// <summary>
        /// Divides sequences of cases in to factors.
        /// </summary>
        /// <example>
        /// 1.
        /// abcd|ab
        /// factor.FactorCases[a] = Factorize(bcd|b)
        /// factor.IsInterruptable = false
        /// a
        /// 2.
        /// bcd|b
        /// factor.FactorCases[b] = Factorize(cd|)
        /// factor.IsInterruptable = false
        /// a(b)
        /// 3.
        /// factor.FactorCases[c] = Factorize(d)
        /// factor.IsInterraptable = true
        /// a(b(c|^))
        /// 4.
        /// factor.FactorCases[d] = null
        /// factor.IsInterruptable = false
        /// a(b(c(d)|^))
        /// </example>
        /// <param name="cases">Non-terminal's cases.</param>
        /// <returns>Factor.</returns>
        private (IDictionary<INode, IFactor> factorCases, bool isInterruptable) Factorize(IEnumerable<IEnumerable<INode>> cases)
        {
            IDictionary<INode, IFactor> factorCases = new Dictionary<INode, IFactor>();
            bool isInterruptable = false;


            // Group cases by first node or by null if case is empty.
            var groups = cases.GroupBy(n => n.Count() > 0 ? n.First() : null);

            // DEBUG
            // groups but in a Dictionary form
            //var grt = groups.ToDictionary(n => n.Key == null ? "null" : n.Key.ToString(), n=>n.ToList());


            // If the only group has null head then hit the bottom.
            if (groups.Count() == 1 && groups.First().Key == null)
            {
                return (null, false);
            }

            // Process all the cases of the factor
            foreach (var group in groups)
            {
                // DEBUG
                // group but in List form
                var temp = group.Select(n => n.ToList()).ToList();

                // If there is only sequence in case
                if (group.Count() == 1)
                {
                    // If the only sequence has no elements then factor is interruptable.
                    if (group.First().Count() == 0)
                    {
                        isInterruptable = true;
                    }
                    else
                    {
                        var (nextFactorCases, isNextFactorInterruptable) =
                            Factorize(group.Select(n => n.Skip(1)));


                        if (nextFactorCases != null)
                        {
                            CreateFactor(nextFactorCases, isNextFactorInterruptable, group);
                        }
                        else
                        {
                            factorCases[group.Key] = null;
                        }
                    }
                }
                else
                {
                    var (nextFactorCases, isNextFactorInterruptable) =
                        Factorize(group.Select(n => n.Skip(1)));


                    if (nextFactorCases != null)
                    {
                        CreateFactor(nextFactorCases, isNextFactorInterruptable, group);
                    }
                    else
                    {
                        factorCases[group.Key] = null;

                        isInterruptable = true;
                    }
                }
            }

            return (factorCases, isInterruptable);

            void CreateFactor(
                IDictionary<INode, IFactor> nextFactorCases,
                bool isNextFactorInterruptable,
                IGrouping<INode, IEnumerable<INode>> group
            )
            {

                Factor nextFactor = new Factor
                {
                    FactorCases = nextFactorCases,
                    IsInterruptable = isNextFactorInterruptable,
                };

                nodes.Add(nextFactor);

                factorCases[group.Key] = nextFactor;
            }

        }

        #endregion

        #region Classes

        /// <summary>
        /// Gets classes and white-delimiters
        /// based on ones defined in grammar and
        /// deconstructs terminals to classes.
        /// </summary>
        /// <param name="grammarElement">Grammar element only to get white delimiters from it.</param>
        Dictionary<string, string> ParseClasses(XElement grammarElement)
        {
            // Get classes that were defined in the grammar file
            var definedInGrammarClasses = nodes.OfType<IClass>()
                .ToDictionary(n => n.SymbolClass, m => m.Symbols);

            // Get terminals that do not fully belong to any defined in grammar classes
            var unclassifiedTerminals = GetUnclassifiedTerminals(definedInGrammarClasses);

            var controlTerminals = nodes.OfType<Terminal>()
                .Where(n => !unclassifiedTerminals.Contains(n.Name));

            foreach (Terminal terminal in controlTerminals)
            {
                terminal.IsControl = true;
            }

            // Get terminals that do not fully belong to any defined in grammar classes
            // and have one or more characters in common with each other.
            var intersectedUnclassifiedTerminals = GetIntersectedUnclassifiedTerminals(unclassifiedTerminals);

            // Deconstruct terminals to classes and combine singleDelimiters into a single class.
            var notPredefinedInGrammarClasses = GetNotPredefinedInGrammarClasses(intersectedUnclassifiedTerminals, unclassifiedTerminals);

            var classes = definedInGrammarClasses.Concat(notPredefinedInGrammarClasses).ToDictionary(n => n.Key, n => n.Value);

            this.unclassifiedTerminals = intersectedUnclassifiedTerminals.ToList();

            return classes;
        }


        /// <summary>
        /// Get terminals that do not fully belong to any of defined in grammar classes.
        /// </summary>
        /// <param name="definedInGrammarClasses">Classes that were defined in the grammar file.</param>
        /// <returns>List of unclassified terminals.</returns>
        private List<string> GetUnclassifiedTerminals(Dictionary<string, string> definedInGrammarClasses)
        {
            var unclassifiedTerminals = new List<string>();

            foreach (var terminal in nodes.OfType<ITerminal>())
            {
                string className = null;

                foreach (char ch in terminal.Name)
                {
                    if (className == null)
                    {
                        if (definedInGrammarClasses.Any(n => n.Value.Contains(ch)))
                        {
                            className = definedInGrammarClasses.First(n => n.Value.Contains(ch)).Key;
                        }
                    }
                    else
                    {
                        if (definedInGrammarClasses.Any(n => n.Value.Contains(ch)))
                        {
                            // Check if terminal belongs to more than one class
                            if (className != definedInGrammarClasses.First(n => n.Value.Contains(ch)).Key)
                            {
                                // If second and / or followed characters do not belong to the class
                                // defined on first character
                                // then the terminal does not fully belong to any class
                                // so the terminal is unclassified.

                                className = null;

                                break;
                            }
                        }
                        else
                        {
                            // If second and/or followed characters do not belong to any class
                            // then the terminal does not fully belong to any class
                            // so the terminal is unclassified.

                            className = null;

                            break;
                        }
                    }
                }

                // If after all characters checked no class was matched with
                // then the terminal is unclassified.
                if (className == null)
                {
                    unclassifiedTerminals.Add(terminal.Name);
                }
            }

            return unclassifiedTerminals.Distinct().ToList();
        }

        /// <summary>
        /// Get terminals that intersect with at least one character.
        /// </summary>
        /// <param name="unclassifiedTerminals">Unclassified terminals.</param>
        /// <returns>List of intersected terminals.</returns>
        private List<string> GetIntersectedUnclassifiedTerminals(List<string> unclassifiedTerminals)
        {
            var intersectedUnclassifiedTerminals = new List<string>();

            foreach (string terminal in unclassifiedTerminals)
            {
                if (unclassifiedTerminals.Any(n => terminal != n && (n.Contains(terminal) || terminal.Contains(n)))
                    || intersectedUnclassifiedTerminals.Any(n => terminal != n && terminal.Contains(n)))
                {
                    intersectedUnclassifiedTerminals.Add(terminal);
                }
            }

            return intersectedUnclassifiedTerminals.Distinct().ToList();
        }

        /// <summary>
        /// Deconstruct terminals to classes and combine singleDelimiters into a single class.
        /// </summary>
        /// <param name="intersectedUnclassifiedTerminals">Terminals that do not fully belong to any defined class 
        /// but have some characters in common with each other.</param>
        /// <param name="unclassifiedTerminals">Terminals that do not fully belong to any defined class.</param>
        /// <returns>Classes.</returns>
        private Dictionary<string, string> GetNotPredefinedInGrammarClasses(List<string> intersectedUnclassifiedTerminals, List<string> unclassifiedTerminals)
        {
            var classes = new Dictionary<string, string>();

            var exceptTerminals = unclassifiedTerminals.Except(intersectedUnclassifiedTerminals);

            classes["singleDelimiter"] = string.Join("", exceptTerminals.Where(n => n.Count() == 1));

            foreach (string terminal in intersectedUnclassifiedTerminals)
            {
                foreach (char ch in terminal)
                {
                    if (!classes["singleDelimiter"].Contains(ch))
                    {
                        classes[ch.ToString()] = ch.ToString();
                    }
                }
            }

            return classes;
        }

        #endregion

        #region Nodes

        /// <summary>
        /// Reads a node from the grammar file with all its children
        /// </summary>
        /// <example>
        /// <d>a</d> -> define new non-terminal (class or token or medium)
        /// <n>b</n> -> search for defined non-terminal
        /// <t>c</t> -> define terminal
        /// </example>
        /// <param name="element">Definition node in grammar file.</param>
        /// <param name="isInsideToken">If current node is located inside a token.</param>
        /// <returns>Parsed node.</returns>
        Node ParseNode(XElement element, bool isInsideToken)
        {
            switch (element.Name.LocalName)
            {
                case "d":
                {
                    var name = element.Attribute("name").Value;

                      

                    // If node is symbol class
                    if (element.Attribute("symbol-class") != null)
                    {
                        var node = new Class
                        {
                            Name = name,
                            SymbolClass = element.Attribute("symbol-class").Value,
                            Symbols = element.Element("cta").Value,
                        };

                        nodes.Add(node);

                        return node;
                    }

                    // If node is token
                    else if (element.Attribute("token-class") != null)
                    {
                        string execClass = element.Attribute("exec-class")?.Value;

                        var node = new DefinedToken
                        {
                            Name = name,
                            TokenClass = element.Attribute("token-class").Value,
                            ExecuteStreamNodeType = execClass,
                        };

                        nodes.Add(node);

                        node.Cases = ParseCases(element, true);

                        return node;
                    }

                    // If node is medium
                    else
                    {
                        var node = new Medium
                        {
                            Name = name,
                        };

                        nodes.Add(node);

                        node.Cases = ParseCases(element, false);

                        return node;
                    }
                }
                case "n":
                    {
                        string name = element.Value;

                        if (nodes.OfType<INonterminal>().All(n => n.Name != name))
                        {
                            throw new Exception($"Node {name} was referenced before it was defined");
                        }

                        return (Node)nodes.OfType<INonterminal>().Single(n => n.Name == name);
                    }
                case "t":
                    {
                        string name = element.Value;
                        string execClass = element.Attribute("exec-class")?.Value ?? string.Empty;

                        string[] streamers = element.Attribute("streamers")?.Value.Split('|') ?? new string[0];
                        string[] breakers = element.Attribute("breakers")?.Value.Split('|') ?? new string[0];

                        bool isStreamMaxCountSet = int.TryParse(element.Attribute("stream-max-count")?.Value,
                            out int streamMaxCount);

                        bool isOperatorPrioritySet = int.TryParse(element.Attribute("operator-priority")?.Value,
                            out int operatorPriority);

                        if (nodes.OfType<ITerminal>().Any(n => n.Name == name))
                        {
                            var node1 = (Node)nodes.OfType<ITerminal>().Single(n => n.Name == name);

                            if (!string.IsNullOrEmpty(execClass))
                            {
                                if (!string.IsNullOrEmpty(node1.ExecuteStreamNodeType) 
                                    && node1.ExecuteStreamNodeType != execClass)
                                {
                                    throw new Exception("Attempt to assign two different execute stream node types.");
                                }

                                node1.ExecuteStreamNodeType = execClass;
                            }


                            return node1;
                        }

                        Terminal node;

                        switch (execClass)
                        {
                            case "statement":
                                node = new DefinedStatement
                                {
                                    Name = name,
                                    TokenClass = unclassifiedTokenClassName,
                                    ExecuteStreamNodeType = execClass,
                                    Streamers = streamers,
                                    Breakers = breakers,
                                    IsStreamMaxCountSet = isStreamMaxCountSet,
                                    StreamMaxCount = streamMaxCount,
                                };
                                break;
                            case "operator":
                                node = new DefinedOperator
                                {
                                    Name = name,
                                    TokenClass = unclassifiedTokenClassName,
                                    ExecuteStreamNodeType = execClass,
                                    Priority = operatorPriority,
                                };
                                break;
                            default:
                                node = new Terminal
                                {
                                    Name = name,
                                    TokenClass = unclassifiedTokenClassName,
                                    ExecuteStreamNodeType = execClass,
                                };
                                break;
                        }
                           

                        if (!isInsideToken || shouldIncludeTerminalsFromInsideOfDefinedTokens)
                        {
                            nodes.Add(node);
                        }

                        return node;
                    }
                default:
                    throw new Exception($"Unsupported tag {element.Name.LocalName} was found in grammar.");
            }
        }

        /// <summary>
        /// Reads all nodes of cases of the element from the grammar file.
        /// </summary>
        /// <example>
        /// <c>
        ///     <d>a</d>
        ///     <t>b</t>
        ///     <d>c</d>
        ///     <d>d</d>
        /// </c>
        /// <c>
        ///     <n>a</n>
        ///     <t>b</t>
        ///     <d>h</d>
        ///     <n>h</n>
        /// </c>
        /// ->
        /// [
        ///     [a,b,c,d],
        ///     [a,b,h,h],
        /// ]
        /// </example>
        /// <param name="element">Definition node in grammar file.</param>
        /// <param name="isInsideToken">If current node is located inside a token.</param>
        /// <returns>List of arsed cases.</returns>
        List<List<Node>> ParseCases(XElement element, bool isInsideToken)
        {
            List<List<Node>> cases = new List<List<Node>>();

            foreach (var @case in element.Elements("c"))
            {
                List<Node> nodes = new List<Node>();

                foreach (var grammarNode in @case.Elements())
                {
                    nodes.Add(ParseNode(grammarNode, isInsideToken));
                }

                cases.Add(nodes);
            }

            return cases;
        }

        object IParser.Parse()
        {
            return Parse();
        }

        #endregion
    }

}
