module RpnParser.StatementRuleParser

open System.Xml.Linq

module private Xml =
    let (|NotNull|_|) = function null -> None | e -> Some e

    let private xname name = XName.Get name

    //let value (e:XElement) = e.Value

    let element name (e:XContainer) =
        match name |> xname |> e.Element with
        | NotNull e -> e
        | _ -> null

    let elements name (e:XContainer) =
        match name |> xname |> e.Elements with
        | NotNull e -> e
        | _ -> null

    let allElements (e:XContainer) =
        e.Elements () |> List.ofSeq

    let attribute name (e:XElement) =
        match name |> xname |> e.Attribute with
        | NotNull e -> e.Value
        | _ -> null

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

            let attr x = attribute x node
            
            match node.Name.LocalName with
            | "s" -> Stream{ id = attr "id" |> int } 
            | "r" ->
                match attr "call" with
                | NotNull n -> Call{ address = n; paramCount = attr "param-count" |> int } |> Reference
                | _ ->
                    match attr "jmp" with
                    | NotNull n -> Jmp{ address = n } |> Reference
                    | _ ->
                        match attr "jn" with
                        | NotNull n -> Jn{ address = n } |> Reference
                        | _ ->
                            match attr "ujmp" with
                            | NotNull _ -> Ujmp |> Reference
                            | _ -> failwith "Unsupported reference."
            | "d" ->
                match attr "label" with
                | NotNull label -> Label{ name = label } |> Definition
                | _ -> failwith "Unsupported definition."
            | _ -> failwith "Unsupported rule node."
    ]

    {
        rules = nodes; 
        streamCount = 
            match caseNode |> attribute "stream-count" with
            | NotNull streamCount -> streamCount |> int |> Some
            | _ -> None
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
