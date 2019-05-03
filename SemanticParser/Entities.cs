using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;
using Core.Entity;

namespace SemanticParser
{
    class SemanticParserResult : ISemanticParserResult
    {
        public IEnumerable<IParserError> Errors { get; internal set; }

        public IScope RootScope { get; internal set; }
    }

    class SemanticParserError : IParserError
    {
        public string Message { get; internal set; }

        public string Tag { get; internal set; }

        public IEnumerable<IParsedToken> TokensOnError { get; internal set; }
    }



    abstract class ExecutionNode : IExecutionStreamNode
    {
        public IScope Scope { get; set; }
        public StreamControlNodeType Type { get; set; }
    }

    abstract class DefinedExecutionNode : ExecutionNode, IDefinedStreamNode
    {
        public int GrammarNodeId { get; set; }
        public int InStringPosition { get; set; }
    }

    class Scope : IScope
    {
        public IScope ParentScope { get; set; }
        public IList<IVariable> Variables { get; set; }
        public IList<ILabel> Labels { get; set; }
        public IList<IScope> ChildrenScopes { get; set; }
        public IEnumerable<IExecutionStreamNode> Stream { get; set; }
        public IEnumerable<IExecutionStreamNode> RpnStream { get; set; }

        private IEnumerable<IExecutionStreamNode> GetConsistentStream(IEnumerable<IExecutionStreamNode> stream, bool shouldGetRpn)
        {
            foreach (var node in stream)
            {
                yield return node;

                if (node is IStatement st)
                {
                    if (shouldGetRpn)
                    {
                        foreach (var innerNodes in GetConsistentStream(st.RpnStreamProcessed, shouldGetRpn))
                        {
                            yield return innerNodes;
                        }
                    }
                    else
                    {
                        foreach (var innerStream in st.Streams)
                        {
                            foreach (var innerNodes in GetConsistentStream(innerStream, shouldGetRpn))
                            {
                                yield return innerNodes;
                            }
                        }
                    }
                }
                else if (node is IDelimiter d && d.Type == StreamControlNodeType.ScopeIn)
                {
                    foreach (var innerNodes in GetConsistentStream(d.ChildScope.Stream, shouldGetRpn))
                    {
                        yield return innerNodes;
                    }
                }
            }
        }

        public IEnumerable<IExecutionStreamNode> GetConsistentStream()
        {
            return GetConsistentStream(Stream, false);
        }

        public IEnumerable<IExecutionStreamNode> GetRpnConsistentStream()
        {
            return GetConsistentStream(RpnStream, true);
        }
    }

    class Variable : DefinedExecutionNode, IVariable
    {
        public bool IsAssigned { get; set; }
        public object Value { get; set; }
        public string Name { get; set; }
    }

    class Literal : DefinedExecutionNode, ILiteral
    {
        public object Value { get; set; }
    }

    class Operator : DefinedExecutionNode, IOperator
    {
        public int OperandCount { get; set; }
    }

    class Statement : DefinedExecutionNode, IStatement
    {
        public IEnumerable<IEnumerable<IExecutionStreamNode>> Streams { get; set; }
        public IEnumerable<IExecutionStreamNode> RpnStreamProcessed { get; set; }
        public int NodeId { get; set; }
        public bool IsStreamMaxCountSet { get; set; }
        public int StreamMaxCount { get; set; }
    }

    class Delimiter : DefinedExecutionNode, IDelimiter
    {
        public IScope ChildScope { get; set; }
    }

    class Label : ExecutionNode, ILabel
    {
        public string Name { get; set; }
    }

    class DefinedLabel : DefinedExecutionNode, IDefinedLabel
    {
        public string Name { get; set; }
    }

}
