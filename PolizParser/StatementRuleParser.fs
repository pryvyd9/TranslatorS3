module RpnParser.StatementRuleParser

open System.Xml.Linq

module private Xml =
    let (|NotNull|_|) = function null -> None | x -> Some x


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

    let nodes =
        caseNode
        |> allElements
        |> List.map
            (fun node ->

            let attr name = node |> attribute name

            match node.Name.LocalName with
            | "s" ->
                Stream{ id = attr "id" |> int }
            | "r" ->
                match attr "call" with
                | NotNull x ->
                    Call{ address = x; paramCount = attr "param-count" |> int}
                    |> Reference
                | _ -> 
                    match attr "jmp" with
                    | NotNull x ->
                        Jmp{ address = x }
                        |> Reference
                    | _ ->
                        match attr "jn" with
                        | NotNull x ->
                            Jn{ address = x }
                            |> Reference
                        | _ ->
                            match attr "ujmp" with
                            | NotNull _ -> 
                                Ujmp 
                                |> Reference
                            | _ -> failwith "Unsupported reference."
            | "d" ->
                match attr "label" with
                | NotNull x ->
                    Label{ name = x }
                    |> Definition
                | _ -> failwith "Unsupported definition."
            | _ -> failwith "Unsupported rule node.")

    {
        rules = nodes; 
        streamCount = 
            match caseNode |> attribute "stream-count" with
            | null -> None
            | streamCount -> streamCount |> int |> Some
    }

let parseStatement (statementNode:XElement) =
    let cases =
        statementNode
        |> elements "c"
        |> Seq.map parseCase
        |> List.ofSeq

    { cases = cases; name = statementNode |> attribute "name" }


let parse (path:string) =
    let rootElement = path |> load |> element "statements"
    let statements =
         rootElement 
         |> elements "statement" 
         |> Seq.map parseStatement
         |> List.ofSeq

    statements
