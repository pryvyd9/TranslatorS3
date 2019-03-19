using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core;

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
        public int GrammarNodeId { get; set; }

        public int InStringPosition { get; set; }
        public StreamControlNodeType Type { get; set; }
    }

    class Scope : IScope
    {
        public IScope ParentScope { get; set; }
        public IList<IVariable> Variables { get; set; }
        public IList<IScope> ChildrenScopes { get; set; }
        public IExecutionStream Stream { get; set; }

        private IEnumerable<IExecutionStreamNode> GetConsistentStream(IExecutionStream stream)
        {
            foreach (var node in stream.Tokens)
            {
                yield return node;

                if (node is IStatement st)
                {
                    foreach (var innerStream in st.Streams)
                    {
                        foreach (var innerNodes in GetConsistentStream(innerStream))
                        {
                            yield return innerNodes;
                        }
                    }
                }
            }
        }

        public IEnumerable<IExecutionStreamNode> GetConsistentStream()
        {
            return GetConsistentStream(Stream);
        }
    }

    class Variable : ExecutionNode, IVariable
    {
        public bool IsAssigned { get; set; }
        public object Value { get; set; }
        public string Name { get; set; }
    }

    class Literal : ExecutionNode, ILiteral
    {
        public object Value { get; set; }
    }

    class Operator : ExecutionNode, IOperator
    {
        public int OperandCount { get; set; }
    }

    class Statement : ExecutionNode, IStatement
    {
        public IEnumerable<IExecutionStream> Streams { get; set; }
        public int NodeId { get; set; }
        public bool IsStreamMaxCountSet { get; set; }
        public int StreamMaxCount { get; set; }
    }

    class Delimiter : ExecutionNode, IDelimiter
    {

    }

    class Label : ExecutionNode, ILabel
    {

    }

    class Jump : ExecutionNode, IJump
    {

    }

    class JumpConditional : Jump, IJumpConditional
    {
        
    }

    class JumpConditionalNegative : JumpConditional, IJumpConditionalNegative
    {

    }

    class ExecutionStream : IExecutionStream
    {
        internal List<IExecutionStreamNode> Tokens { get; set; }
        IEnumerable<IExecutionStreamNode> IExecutionStream.Tokens => Tokens;
    }
}
