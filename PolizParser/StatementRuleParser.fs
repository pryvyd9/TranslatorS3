module RpnParser.StatementRuleParser

open System.Xml.Linq

module private Xml =
    let private (|NotNull|_|) e =
        match e with
        | null -> None
        |_-> Some e

    let private xname name = XName.Get name

    //let value (e:XElement) = e.Value

    let element name (e:XContainer) =
        match name |> xname |> e.Element with
        |NotNull e -> e
        |_->null

    let elements name (e:XContainer) =
        match name |> xname |> e.Elements with
        |NotNull e -> e
        |_->null

    let allElements (e:XContainer) =
        e.Elements () |> List.ofSeq

    let attribute name (e:XElement) =
        match name |> xname |> e.Attribute with
        |NotNull e -> e.Value
        |_->null

    let load (path:string) = XDocument.Load path

open Xml

[<AutoOpen>]
module Types =

    type RuleNode = 
        | Stream of Stream
        | Reference of Reference
        | Definition of Definition
    and Stream = {id:int}
    and Reference = 
        | Call of Call 
        | Jmp of Jmp
        | Jn of Jn
        | Ujmp
    and Call = {address:string; paramCount:int}
    and Jmp = {address:string}
    and Jn = {address:string}
    and Definition = | Label of Label
    and Label = {name:string}

    type Case = {rules:RuleNode list; streamCount:int option}

    type Statement = {cases:Case list; name:string}



let parseCase (caseNode:XElement) =
    let nodes = [
        for node in caseNode |> allElements ->
            match node.Name.LocalName with
            | "s" ->
               let id = node |> attribute "id" |> int
               Stream{id=id} 
            | "r" ->
                match node |> attribute "call" with
                | null ->
                    match node |> attribute "jmp" with
                    | null ->
                        match node |> attribute "jn" with
                        | null ->
                            match node |> attribute "ujmp" with
                            | null ->
                                failwith("Unsupported reference.")
                            | ujmp -> 
                                Ujmp |> Reference
                        | jn -> 
                            Jn{address=jn}
                            |> Reference
                    | jmp ->
                        Jmp{address=jmp}
                        |> Reference
                | call ->
                    Call{address=call;paramCount=node |> attribute "param-count" |> int}
                    |> Reference
            | "d" ->
                match node |> attribute "label" with
                | null ->
                    failwith("Unsupported definition.")
                | label ->
                    Label{name=label}
                    |> Definition
            | _ ->
                failwith("Unsupported rule node.")
    ]

    {
        rules = nodes; 
        streamCount = 
            match caseNode |> attribute "stream-count" with
            | null -> None
            | streamCount -> streamCount |> int |> Some
    }

let parseStatement (statementNode:XElement) =
    let cases = [
        for case in statementNode |> elements "c" ->
            parseCase case
    ]

    {cases=cases; name = statementNode |> attribute "name"}


let parse (path:string) =
    let rootElement = path |> load |> element "statements"
    let statements = [
        for node in rootElement |> elements "statement" ->
            parseStatement node
    ]

    statements
