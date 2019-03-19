using System;
using System.Collections.Generic;
using System.Linq;

namespace Core
{
    public enum StreamControlNodeType
    {
        None,
        ScopeIn,
        ScopeOut,
        Breaker,
        Streamer,
        Statement,
    }

    public interface IExecutionStreamNode
    {
        IScope Scope { get; }
        int GrammarNodeId { get; }
        int InStringPosition { get; }
        StreamControlNodeType Type { get; }
    }

    public interface IScope
    {
        IScope ParentScope { get; }
        IList<IVariable> Variables { get; }
        IList<IScope> ChildrenScopes { get; }
    }

    public interface IVariable : IExecutionStreamNode
    {
        bool IsAssigned { get; }
        object Value { get; set; }
        string Name { get; }
    }

    public interface ILiteral : IExecutionStreamNode
    {
        object Value { get; }
    }

    public interface IOperator : IExecutionStreamNode
    {
        int OperandCount { get; }
    }

    public interface IStatement : IExecutionStreamNode
    {
        IEnumerable<IExecutionStream> Streams { get; }
        //int[] Streamers { get; }
        //int[] Breakers { get; }
        int NodeId { get; }
        bool IsStreamMaxCountSet { get; }
        int StreamMaxCount { get; }
    }

    public interface IDelimiter : IExecutionStreamNode
    {
        
    }

    //#region System

    public interface ILabel : IExecutionStreamNode
    {
        
    }

    public interface IJump : IExecutionStreamNode
    {
        
    }

    public interface IJumpConditional : IJump
    {
        
    }

    public interface IJumpConditionalNegative : IJumpConditional
    {

    }

    public interface IExecutionStream
    {
        IEnumerable<IExecutionStreamNode> Tokens { get; }
    }

    //#endregion
}
