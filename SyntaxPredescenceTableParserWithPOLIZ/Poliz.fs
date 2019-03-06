module SyntaxPredescenceTableParser.Poliz

open Extentions
open System
open System.Windows
open System.Windows.Controls

let ID = 45
let mutable processed = []

let mutable parsedNodes:Core.IParsedToken list = []

let mutable polizList:list<int*Core.INode> = []
//let mutable polizList = []

type Var = Operator of string*(int list -> int list) | Constant of int | Variable of string ref*string | Undefined of string

let calculate (expression: Var list) =
    let rec inFunc buffer (stream:Var list) =
        match stream with
        | head :: tail ->
            match head with
            | Constant c ->
                inFunc (c :: buffer) tail
            | Variable (value, name) ->
                let num = ref 0
                if Int32.TryParse (value.Value, num)
                then
                    inFunc (int value.Value :: buffer) tail
                else
                    "Unassigned variable " + name
                //if value.Value <> null
                //then
                //    inFunc (int value.Value :: buffer) tail
                //else
                //    "Unassigned variable " + name
            | Operator (o, func) ->
                let result = func buffer
                inFunc (func buffer) tail
                //match buffer with
                //| f :: s :: buffer ->
                //    let result = func buffer
                //    inFunc (func buffer) tail
                //| _ ->
                //    failwith("not enough arguments for operator to function")


                //match o with
                //| "+" -> 
                //    match buffer with
                //    | f :: s :: buffer ->
                //        let result = func buffer
                //        inFunc (func buffer) tail
                //    | _ ->
                //        failwith("not enough arguments for operator to function")
                //| "-" ->
                //    match buffer with
                //    | f :: s :: buffer ->
                //        inFunc (s - f :: buffer) tail
                //    | _ ->
                //        failwith("not enough arguments for operator to function")
            | Undefined u ->
                failwith("undefined member in expression")
        | _ -> buffer.[0] |> string
    inFunc [] expression

let operatorFuncs = dict[
    "+",(function f::s::tail -> s + f :: tail | _ -> []);
    "-",(function f::s::tail -> s - f :: tail | _ -> []);
    "/",(function f::s::tail -> s / f :: tail | _ -> []);
    "*",(function f::s::tail -> s * f :: tail | _ -> []);
    "--",(function f::tail -> -f :: tail | _ -> []);
    "^",(function f::s::tail -> (float s ** float f |> int) :: tail | _ -> []);


]

let launchWindow() =
    let win = Window()

    //let getExpression() =
    //    let mutable str = ""
    //    for (node:int*Core.INode) in polizList
    //        do
    //            if snd node :? Core.IDefinedToken
    //            then
    //                //let pn = parsedNodes |> List.find (fun x -> x.InStringPosition = fst node)
    //                let pn = parsedNodes.[fst node]
    //                str <- str + pn.Name
    //            else
    //                str <- str + (snd node).Name
    //    str

    let getExpression() =
        let mutable (str:Var list) = []
        for (node:int*Core.INode) in polizList
            do
                if snd node :? Core.IDefinedToken
                then
                    //let pn = parsedNodes |> List.find (fun x -> x.InStringPosition = fst node)
                    let pn = parsedNodes.[fst node]
                    if pn.TokenClassId = 3 // id
                    then str <- str @ [ Variable (ref "NaN", pn.Name) ]
                    elif pn.TokenClassId = 2 // const
                    then str <- str @ [ Constant (int pn.Name) ]
                    else str <- str @ [ Operator (pn.Name, operatorFuncs.[pn.Name]) ]
                else
                    str <- str @ [ Undefined ((snd node).Name) ]
        str

    let getString vars =
        let mutable str = ""
        for node in vars 
            do
                match node with
                | Operator (o,_) | Undefined o -> 
                    str <- str + o
                | Variable (_,s) ->
                    str <- str + s
                | Constant c ->
                    str <- str + (c |> string)
        str



    let expression = getExpression()

    let res = calculate expression

    let stringRepresentation = getString expression

    printfn "%A" (stringRepresentation)

    let stack = StackPanel()

    let expressionView = Label(Content = (stringRepresentation))

    let variables = expression |> List.choose (function Variable (value, name) -> Some (value, name) | _ -> None)

    stack.Children.Add expressionView |> ignore

    let resultLabel = Label()

    stack.Children.Add resultLabel |> ignore

    let varFields = 
        [
            for e in variables -> 
                let tb = TextBox(Text = (fst e).Value)
                tb.TextChanged |> Event.add (fun _ -> fst e := tb.Text; printfn "%A" e; printfn "%A" (calculate expression))
                stack.Children.Add tb |> ignore
                
        ]



    win.Content <- stack

    win.Show() |> ignore



let poliz (idsToChange:(int*int) list) (nodes:Core.INode list) =

    
    let root = nodes |> List.find (fun x -> x.Id = ID)
    let tokens = 
        idsToChange 
        |> List.choose (fun x -> match nodes |> List.tryFind (fun y -> y.Id = snd x && (y :? Core.ITerminal || y :? Core.IDefinedToken)) with Some s -> Some (fst x, s) | None -> None)

    if tokens |> List.forall ( fun x -> BaseSearching.contains (snd x) root)
    then
        if tokens.Length = 1
        then
            polizList <- polizList @ tokens
    else
        processed <- processed @ [polizList]

        launchWindow()

        printfn "%A" polizList
        polizList <- []

    //let root = nodes |> List.find (fun x -> x.Id = ID)
    //let tokens = 
    //    idsToChange 
    //    |> List.choose (fun x -> nodes |> List.tryFind (fun y -> y.Id = snd x && (y :? Core.ITerminal || y :? Core.IDefinedToken)))

    //if tokens |> List.forall ( fun x -> BaseSearching.contains x root)
    //then
    //    if tokens.Length = 1
    //    then
    //        polizList <- polizList @ tokens
    //else
    //    processed <- processed @ [polizList]

    //    launchWindow()

    //    printfn "%A" polizList
    //    polizList <- []
