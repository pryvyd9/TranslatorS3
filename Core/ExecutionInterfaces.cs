using System;
using System.Collections.Generic;
using System.Linq;



namespace Core
{

    //public interface ITable
    //{
    //    IList<IScope> Scopes { get; }
    //    IList<IRegisterNode> Registers { get; }
    //    IList<IReferenceNode> References { get; }
    //    IList<INode> Nodes { get; }
    //}

    //public interface IScope
    //{
    //    int ParentScope { get; }
    //    IList<int> ChildrenScopes { get; }
    //    IList<int> Registers { get; }
    //}

    //public interface INode
    //{
    //    int Scope { get; }
    //}

    //public interface IRegisterNode : INode
    //{

    //}

    //public interface IRegisterValue : IRegisterNode
    //{
    //    string Name { get; }
    //}

    //public interface IRegisterLabel : IRegisterNode
    //{
    //    string Name { get; }
    //}


    //public interface IReferenceNode : INode
    //{

    //}

    //public interface IReferenceValue : IReferenceNode
    //{
    //    string Name { get; }
    //}

    //public interface IReferenceLabel : IReferenceNode
    //{
    //    string Name { get; }
    //}

    //public enum JumpCondition
    //{
    //    None,
    //    Positive,
    //    Negative
    //}

    //public interface IJump : INode
    //{
    //    JumpCondition Condition { get; }
    //}

    public enum StreamControlNodeType
    {
        None,
        ScopeIn,
        ScopeOut,
        ParensIn,
        ParensOut,
        Breaker,
        Streamer,
        Statement,
    }


    public delegate void ExecutionStartedEventHandler();
    public delegate void ExecutionEndedEventHandler();
    public delegate void ExecutionInputEventHandler(Action<object> input);

    public enum State
    {
        Idle,
        Paused,
        Running,
        Inputting,
    }

    public interface IExecutor
    {
        IEnumerable<Optimize.INode> ExecutionNodes { set; }
        IEnumerable<INode> GrammarNodes { set; }
        IEnumerable<IVariable> VisibleVariables { get; }

        State State { get; }

        //void Input(object data);

        Action<object> Output { set; }

        event ExecutionStartedEventHandler Started;
        event ExecutionEndedEventHandler Ended;
        event ExecutionInputEventHandler Input;

        int[] BreakPositions { set; }

        void StepOver();
        void Run();
        void Abort();
    }


    public interface IExecutionStreamNode
    {
        IScope Scope { get; }
        StreamControlNodeType Type { get; }
    }

    public interface IDefinedStreamNode : IExecutionStreamNode
    {
        int GrammarNodeId { get; }
        int InStringPosition { get; }
    }

    public interface IScope
    {
        IScope ParentScope { get; }
        IList<IVariable> Variables { get; }
        IList<ILabel> Labels { get; }
        IList<IScope> ChildrenScopes { get; }
        IEnumerable<IExecutionStreamNode> Stream { get; }
        IEnumerable<IExecutionStreamNode> RpnStream { get; set; }
        IEnumerable<IExecutionStreamNode> GetConsistentStream();
        IEnumerable<IExecutionStreamNode> GetRpnConsistentStream();
    }

    public interface IVariable : IDefinedStreamNode
    {
        bool IsAssigned { get; }
        object Value { get; set; }
        string Name { get; }
    }

    public interface ILiteral : IDefinedStreamNode
    {
        object Value { get; }
    }

    public interface IOperator : IDefinedStreamNode
    {
        int OperandCount { get; }
    }

    public interface IStatement : IDefinedStreamNode
    {
        IEnumerable<IEnumerable<IExecutionStreamNode>> Streams { get; }
        IEnumerable<IExecutionStreamNode> RpnStreamProcessed { get; set; }
        int NodeId { get; }
        bool IsStreamMaxCountSet { get; }
        int StreamMaxCount { get; }
    }

    public interface IDelimiter : IDefinedStreamNode
    {
        IScope ChildScope { get; }
    }

    //#region System

    public interface ILabel : IExecutionStreamNode
    {
        string Name { get; }
    }

    public interface IDefinedLabel : ILabel, IDefinedStreamNode
    {

    }

    public interface IJump : IExecutionStreamNode
    {
        ILabel Label { get; }
    }

    public interface IUserJump : IJump
    {

    }

    public interface IJumpConditional : IJump
    {

    }

    public interface IJumpConditionalNegative : IJumpConditional
    {

    }

    public interface ICall : IExecutionStreamNode
    {
        string Address { get; }
        int ParamCount { get; }
    }

    //public interface IExecutionStream
    //{
    //    IEnumerable<IExecutionStreamNode> Tokens { get; }
    //}

    //#endregion
}
