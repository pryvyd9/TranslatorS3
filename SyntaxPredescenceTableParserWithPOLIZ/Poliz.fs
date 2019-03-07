module SyntaxPredescenceTableParser.Poliz

open Extentions
open System
open System.Windows
open System.Windows.Controls

let ID = 45

let mutable parsedNodes:Core.IParsedToken list = []

let mutable polizList:list<int*Core.INode> = []

let clear() =
    polizList <- []

type Member = 
    | Operator of string*(int list -> int list) 
    | Constant of int 
    | Variable of value:string ref*name:string 
    | Undefined of string


let calculate (expressionStack:Member list) =
    let rec inFunc buffer =
        function
        | head :: tail ->
            match head with
            | Constant c ->
                inFunc (c :: buffer) tail
            | Variable (value, name) ->
                match Int32.TryParse (value.Value) with
                | true, _ -> inFunc (int value.Value :: buffer) tail
                | _ -> "Unassigned variable " + name
            | Operator (_, func) ->
                try inFunc (func buffer) tail
                with e -> e.Message
            | Undefined u ->
                failwith("undefined member '" + u + "' in expression")
        | _ -> buffer.Head |> string
    inFunc [] (expressionStack |> List.rev) 

let operatorFuncs = dict[
    "+",(function f::s::tail -> s + f :: tail | _ -> []);
    "-",(function f::s::tail -> s - f :: tail | _ -> []);
    "/",(function f::s::tail -> s / f :: tail | _ -> []);
    "*",(function f::s::tail -> s * f :: tail | _ -> []);
    "--",(function f::tail -> -f :: tail | _ -> []);
    "^",(function f::s::tail -> (float s ** float f |> int) :: tail | _ -> []);
]


let getExpression() =
    let rec inFunc =
        function
        | (index, node) :: tail ->
            match box node with 
            | :? Core.IDefinedToken ->
                let pn =  parsedNodes.[index]
                match pn.TokenClassId with
                | 3 -> inFunc tail @ [ Variable (ref "NaN", pn.Name) ]
                | 2 -> inFunc tail @ [ Constant (int pn.Name) ]
                | _ -> inFunc tail @ [ Operator (pn.Name, operatorFuncs.[pn.Name]) ]
            | _ -> inFunc tail @ [ Undefined (node:>Core.INode).Name ]
        | _ -> []
    inFunc polizList
  
let getString vars =
       let rec inFunc str =
           function
           | h::tail ->
               match h with
               | Operator (o,_) | Undefined o -> 
                   (inFunc str tail) + o
               | Variable (_,s) ->
                   (inFunc str tail) + s
               | Constant c ->
                   (inFunc str tail) + (c |> string)
           | _ -> str
       inFunc "" vars

let launchWindow() =
    let win = Window()
    let stack = StackPanel()
    win.Content <- stack


    let expression = getExpression()
    let firstResult = calculate expression
    let stringRepresentation = getString expression

    //printfn "%A" (stringRepresentation)



    // Exression
    let expressionField = StackPanel(Orientation = Orientation.Horizontal)
       
    let expressionLabel = Label(Content = "Expression: ")
    let expressionContent = Label(Content = stringRepresentation)

    expressionField.Children.Add expressionLabel |> ignore
    expressionField.Children.Add expressionContent |> ignore

    stack.Children.Add expressionField |> ignore


    // Result
    let resultField = StackPanel(Orientation = Orientation.Horizontal)
    
    let resultLabel = Label(Content = "Result: ")
    let resultContent = Label(Content = firstResult)

    resultField.Children.Add resultLabel |> ignore
    resultField.Children.Add resultContent |> ignore

    stack.Children.Add resultField |> ignore


    // Variables
    let variables = 
        expression 
        |> List.choose (function Variable (value, name) -> Some (value, name) | _ -> None)
        |> List.rev

    for (value, name) in variables 
        do
            let variableField = StackPanel(Orientation = Orientation.Horizontal)
        
            let variableContent = TextBox(Text = (value).Value)
            let variableLabel = Label(Content = name + ": ")


            // TextChanged event
            let eventHandler _ =
                value := variableContent.Text
                resultContent.Content <- calculate expression

            variableContent.TextChanged |> Event.add (eventHandler)

            variableField.Children.Add variableLabel |> ignore
            variableField.Children.Add variableContent |> ignore

            stack.Children.Add variableField |> ignore





    win.ShowDialog() |> ignore



let poliz (idsToChange:(int*int) list) (nodes:Core.INode list) =
    
    let root = nodes |> List.find (fun x -> x.Id = ID)
    let tokens = 
        idsToChange 
        |> List.choose (fun x -> match nodes |> List.tryFind (fun y -> y.Id = snd x && (y :? Core.ITerminal || y :? Core.IDefinedToken)) with Some s -> Some (fst x, s) | None -> None)

    if tokens |> List.forall ( fun x -> BaseSearching.contains (snd x) root)
    then
        if tokens.Length = 1
        then polizList <- polizList @ tokens
    else 
        launchWindow()
