module ElmishUtility

// Inpsired by The Elmish Book by Zaid Ajaj https://zaid-ajaj.github.io/the-elmish-book/#/license
// The code here has been extended with extra functions and values, names and properties are
// partially changed, and functions and their signatures are simplified by omitting extra code.
type AsyncOp<'a> =
    | NotStarted
    | InProgress
    | Error of exn
    | Finished of 'a

    member this.value =
        match this with
        | Finished value -> value
        | _ -> failwith "Cannot get async operation value until it has finished"

[<RequireQualifiedAccess>]
module AsyncOp =

    let finished =
        function
        | Finished _ -> true
        | _ -> false

    let inProgress =
        function
        | InProgress -> true
        | _ -> false

    let map (mapping: 'a -> 'b) =
        function
        | NotStarted -> NotStarted
        | InProgress -> InProgress
        | Error e -> Error e
        | Finished value -> Finished(mapping value)

    let bind (mapping: 'a -> AsyncOp<'b>) =
        function
        | NotStarted -> NotStarted
        | InProgress -> InProgress
        | Error e -> Error e
        | Finished value -> mapping value

    let exists (predicate: 'a -> bool) =
        function
        | Finished value -> predicate value
        | _ -> false

    let toOption =
        function
        | Finished value -> Some value
        | _ -> None

    let value =
        function
        | Finished value -> Some value
        | _ -> None
