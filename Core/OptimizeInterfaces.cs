using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Core.Optimize
{
    public interface INode
    {
        int Id { get; }
    }

    public enum EntityType
    {
        Label,
        Variable,
    }

    public enum TTL
    {
        Short,
        Long,
    }

    public enum DataType
    {
        Int,
        String,
        Label,
    }

    public enum CallType
    {
        Operator,
        Function
    }

    public enum JumpType
    {
        Unconditional,
        Positive,
        Negative,
    }

    public interface IDeclare : INode
    {
        string Name { get; }

        EntityType EntityType { get; }

        TTL TTL { get; }

        DataType DataType { get; }
    }

    public interface ILiteral : INode
    {
        object Value { get; }

        DataType DataType { get; }
    }

    public interface IReference : INode
    {
        string Name { get; }

        EntityType EntityType { get; }

        DataType DataType { get; }

        // Declaration Id
        int Address { get; }
    }

  

    public interface ICall : INode
    {
        string Name { get; }

        CallType CallType { get; }

        int ArgumentNumber { get; }
    }


    public interface IJump : INode
    {
        JumpType JumpType { get; }
    }
}