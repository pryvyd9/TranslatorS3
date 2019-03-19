using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public interface IEntity<T> where T : IParserResult
    {
        void Load(string path);
        void Save(string path);
        void Parse(IParser<T> parser);
    }


    public interface INode
    {
        int Id { get; }

        string Name { get; }

        string ToString();

        string ExecuteStreamNodeType { get; }

    }

    public interface ITerminal : INode
    {
        bool IsControl { get; }

        bool IsClassified { get; }

        string[] Streamers { get; }

        string[] Breakers { get; }

        bool IsStreamMaxCountSet { get; }

        int StreamMaxCount { get; }
    }

    public interface IClass : INode
    {
        string SymbolClass { get; }

        int SymbolClassId { get; }

        string Symbols { get; }
    }

    public interface INonterminal : INode
    {

    }

    public interface IMedium : INonterminal, IFactor
    {
        IEnumerable<IEnumerable<INode>> Cases { get; }
    }

    public interface IDefinedToken : INode
    {
        string TokenClass { get; }

        int TokenClassId { get; }
    }

    public interface IFactor : INode
    {
        IDictionary<INode, IFactor> FactorCases { get; }

        IFactor Recursion { get; }

        bool IsInterruptable { get; }

        new string ToString();
    }

    public interface IGrammar
    {
        bool HasLeftRecursion { get; }

        INodeCollection Nodes { get; }

        IClassTable ClassTable { get; }
    }


    public interface IClassTable
    {
        IDictionary<string, string> SymbolClasses { get; }

        Map<string, int> TokenClasses { get; }

        string UnclassifiedTokenClassName { get; }

        string UndefinedTokenClassName { get; }

        string WhiteDelimiters { get; }

        char PotentialWhiteDelimiter { get; }

        (SymbolCategory category, string @class) GetSymbolInfo(char ch);
    }

    public interface INodeCollection : IReadOnlyCollection<INode>
    {
        IEnumerable<INode> Unsorted { get; }

        IEnumerable<ITerminal> Terminals { get; }

        IEnumerable<INonterminal> Nonterminals { get; }

        IEnumerable<IDefinedToken> Tokens { get; }

        IEnumerable<IMedium> Mediums { get; }

        IEnumerable<IClass> Classes { get; }

        IEnumerable<IFactor> Factors { get; }

        IMedium Axiom { get; }
    }


    public interface IParsedToken
    {
        string Name { get; }

        int Id { get; }

        int TokenClassId { get; }

        int RowIndex { get; }

        int InRowPosition { get; }

        int InStringPosition { get; }
    }

    public interface IAutomaton<TState>
    {
        IDictionary<int, TState> States { get; }

        int StartState { get; }
    }

    public interface IFiniteAutomaton : IAutomaton<IFiniteState>
    {

    }

    public interface IState
    {
    }

    public interface IFiniteState : IState
    {
        IDictionary<string, int> Links { get; }

        string TokenName { get; }

        string TokenClass { get; }
    }

    public interface IPushdownState : IState
    {
        IDictionary<int, (int? stateIn, int? stateOut)> Links { get; }

        bool IsInterruptable { get; }
    }

    public interface IPushdownAutomaton : IAutomaton<IPushdownState>
    {

    }


    public interface IPredescenceNode
    {
        IDictionary<int, Relationship> Relashionships { get; }
    }

    public interface IPredescenceTable
    {
        IDictionary<int, IPredescenceNode> Nodes { get; }

        Relationship GetRelashionship(int left, int right);

        IEnumerable<Relationship> DistinguishRelasionship(Relationship relashionship);
    }

}
