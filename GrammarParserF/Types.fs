[<AutoOpen>]
module DomainTypes
open Core.Entity
open System.Collections.Generic

type Node =
    {id:int;name:string;executeStreamNodeType:string}
    //interface INode with
    //    member this.Id = this.id
    //    member this.Name = this.name
    //    member this.ExecuteStreamNodeType = this.executeStreamNodeType
    //    member this.ToString() = this.name

type Terminal = 
    {
        isControl:bool
        isClassified:bool
        tokenClass:string
        tokenClassId:int
        node:Node
    }
    interface INode with
        member this.Id = this.node.id
        member this.Name = this.node.name
        member this.ExecuteStreamNodeType = this.node.executeStreamNodeType
        member this.ToString() = this.node.name
    interface ITerminal with
        member this.IsControl = this.isControl
        member this.IsClassified = this.isClassified
    interface IDefinedToken with
        member this.TokenClass = this.tokenClass
        member this.TokenClassId = this.tokenClassId

type Class = 
    {symbolClass:string;symbolClassId:int;symbols:string;node:Node}
    interface INode with
            member this.Id = this.node.id
            member this.Name = this.node.name
            member this.ExecuteStreamNodeType = this.node.executeStreamNodeType
            member this.ToString() = "<" + this.node.name + ">"
    interface IClass with
            member this.SymbolClass = this.symbolClass
            member this.SymbolClassId = this.symbolClassId
            member this.Symbols = this.symbols
             
type DefinedStatement =
    {
        streamers:string[]
        breakers:string[]
        isStreamMaxCountSet:bool
        streamMaxCount:int
        terminal:Terminal
    }
    interface INode with
        member this.Id = this.terminal.node.id
        member this.Name = this.terminal.node.name
        member this.ExecuteStreamNodeType = this.terminal.node.executeStreamNodeType
        member this.ToString() = this.terminal.node.name
    interface ITerminal with
        member this.IsControl = this.terminal.isControl
        member this.IsClassified = this.terminal.isClassified
    interface IDefinedStatement with
        member this.Breakers = this.breakers
        member this.Streamers = this.streamers
        member this.IsStreamMaxCountSet = this.isStreamMaxCountSet
        member this.StreamMaxCount = this.streamMaxCount
    
type DefinedOperator =
    {priority:int;terminal:Terminal}
    interface INode with
        member this.Id = this.terminal.node.id
        member this.Name = this.terminal.node.name
        member this.ExecuteStreamNodeType = this.terminal.node.executeStreamNodeType
        member this.ToString() = this.terminal.node.name
    interface ITerminal with
        member this.IsControl = this.terminal.isControl
        member this.IsClassified = this.terminal.isClassified
    interface IDefinedOperator with
        member this.Priority = this.priority

type Medium =
    {
        factorCases:IDictionary<INode,IFactor>
        recursion:IFactor
        cases:seq<seq<INode>>
        isInterruptable:bool
        node:Node
    }
    interface INode with
        member this.Id = this.node.id
        member this.Name = this.node.name
        member this.ExecuteStreamNodeType = this.node.executeStreamNodeType
        member this.ToString() = this.node.name
    interface IFactor with
        member this.FactorCases = this.factorCases
        member this.Recursion = this.recursion
        member this.IsInterruptable = this.isInterruptable
        member this.ToString() =
            "factor.ToString()"
    interface IMedium with
        member this.Cases = this.cases


type DefinedToken =
    {tokenClass:string;tokenClassId:int;medium:Medium}
    interface INode with
        member this.Id = this.medium.node.id
        member this.Name = this.medium.node.name
        member this.ExecuteStreamNodeType = this.medium.node.executeStreamNodeType
        member this.ToString() = this.medium.node.name
    interface IFactor with
        member this.FactorCases = this.medium.factorCases
        member this.Recursion = this.medium.recursion
        member this.IsInterruptable = this.medium.isInterruptable
        member this.ToString() =
            "factor.ToString()"
    interface IMedium with
        member this.Cases = this.medium.cases
    interface IDefinedToken with
        member this.TokenClass = this.tokenClass
        member this.TokenClassId = this.tokenClassId

    type Factor =
        {
            factorCases:IDictionary<INode,IFactor>
            recursion:IFactor
            isInterruptable:bool
            node:Node
        }
        interface INode with
            member this.Id = this.node.id
            member this.Name = this.node.name
            member this.ExecuteStreamNodeType = this.node.executeStreamNodeType
            member this.ToString() = this.node.name
        interface IFactor with
            member this.FactorCases = this.factorCases
            member this.Recursion = this.recursion
            member this.IsInterruptable = this.isInterruptable
            member this.ToString() =
                "factor.ToString()"