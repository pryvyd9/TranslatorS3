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

    public interface IValueHolder : INode
    {
        DataType DataType { get; }
    }

    public interface IDeclare : IValueHolder
    {
        string Name { get; }

        EntityType EntityType { get; }

        TTL TTL { get; }
    }

    public interface ILiteral : IValueHolder
    {
        object Value { get; }
    }

    public interface IReference : IValueHolder
    {
        string Name { get; }

        EntityType EntityType { get; }

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