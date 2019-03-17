using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

namespace GrammarParser
{


    public class GrammarParserResult : IGrammarParserResult
    {
        public IEnumerable<IParserError> Errors { get; internal set; }

        public IEnumerable<string> UnclassifiedTerminals { get; internal set; }

        public IClassTable ClassTable { get; internal set; }

        public bool HasLeftRecursion { get; internal set; }

        public List<INode> SortedNodes { get; internal set; }

        public List<INode> UnsortedNodes { get; internal set; }

        public IMedium Axiom { get; internal set; }
    }
    
    public abstract class Node : INode
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string ExecuteStreamNodeType { get; set; }

        public override string ToString()
        {
            return $"{Name}";
        }

    }

    public class Class : Node, IClass, INonterminal
    {
        public string SymbolClass { get; set; }

        public int SymbolClassId { get; set; }

        public string Symbols { get; set; }

        public override string ToString()
        {
            return $"<{Name}>";
        }
    }

    public class Terminal : Node, ITerminal, IDefinedToken
    {
        public bool IsControl { get; set; }

        public string TokenClass { get; set; }

        public int TokenClassId { get; set; }

        public bool IsClassified { get; set; }

        public string[] Streamers { get; set; }

        public string[] Breakers { get; set; }

        public bool IsStreamMaxCountSet { get; set; }

        public int StreamMaxCount { get; set; }
    }

    public class DefinedToken : Medium, IDefinedToken
    {
        public string TokenClass { get; set; }

        public int TokenClassId { get; set; }


    }

    public class Medium : Node, IMedium
    {
        public IDictionary<INode, IFactor> FactorCases { get; set; }

        public IFactor Recursion { get; set; }

        public IEnumerable<IEnumerable<INode>> Cases { get; set; }

        public bool IsInterruptable { get; set; }

        public override string ToString()
        {
            return $"<{Name}>";
        }

        string IFactor.ToString()
        {
            IEnumerable<string> basic;

            // Whether must show bare factor structure or notation how it has to be.

            basic = FactorCases.Select(n => $"{n.Key}" + (n.Value != null ? (n.Value.FactorCases.Count <= 1 && !n.Value.IsInterruptable ? n.Value.ToString() : $"({n.Value.ToString()})") : null));

            string str = string.Join("|", basic);


            // Add cup(^) if the factor is interruptable
            if (IsInterruptable)
            {
                str += "|^";
            }


            // If the factor is recursive then add recursive part.
            if (Recursion != null)
            {
                str += $"{{{Recursion.ToString()}}}";
            }

            return str;
        }

    }

    public class Factor : Node, IFactor
    {
        public IDictionary<INode, IFactor> FactorCases { get; set; }

        public IFactor Recursion { get; set; }

        public bool IsInterruptable { get; set; }


        string IFactor.ToString()
        {
            IEnumerable<string> basic;

            // Whether must show bare factor structure or notation how it has to be.

            basic = FactorCases.Select(n => $"{n.Key}" + (n.Value != null ? (n.Value.FactorCases.Count <= 1 && !n.Value.IsInterruptable ? n.Value.ToString() : $"({n.Value.ToString()})") : null));

            string str = string.Join("|", basic);


            // Add cup(^) if the factor is interruptable
            if (IsInterruptable)
            {
                str += "|^";
            }


            // If the factor is recursive then add recursive part.
            if (Recursion != null)
            {
                str += $"{{{Recursion.ToString()}}}";
            }

            return str;
        }

    }



    public class ClassTable : IClassTable
    {
        public IDictionary<string, string> SymbolClasses { get; set; }

        public Map<string, int> TokenClasses { get; set; }

        public string UnclassifiedTokenClassName { get; set; }

        public string UndefinedTokenClassName { get; set; }

        public string WhiteDelimiters { get; set; }

        public char PotentialWhiteDelimiter
        {
            get
            {
                if (WhiteDelimiters != null)
                {
                    return WhiteDelimiters[0];
                }

                var allClassChars = SymbolClasses.SelectMany(n => n.Value).OrderBy(n => n);

                uint i = 0;

                foreach (char curr in allClassChars)
                {
                    if (curr - i != 0)
                        return (char)i;
                }

                if (i < char.MaxValue)
                {
                    return (char)(i + 1);
                }
                else
                {
                    throw new Exception("Undefined behaviour. Classes contain " +
                        "all UTF-16 characters so there is no white delimiters.");
                }
            }

        }

        public (SymbolCategory category, string @class) GetSymbolInfo(char ch)
        {
            string className = SymbolClasses.FirstOrDefault(n => n.Value.Contains(ch)).Key;

            bool foundInClasses = !(className is null);

            if (foundInClasses)
            {
                return (SymbolCategory.Classified, className);
            }

            //if null then contains all characters out of classes
            bool whiteDelimitersContainsCharacter = WhiteDelimiters?.Contains(ch) ?? true;

            if (whiteDelimitersContainsCharacter)
            {
                return (SymbolCategory.WhiteDelimiter, className);
            }

            return (SymbolCategory.Undefined, className);

        }
    }

}
