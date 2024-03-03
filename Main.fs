module Main

open System.Data
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.Layout
open Elmish

type Msg =
    | Increment
    | Decrement
    | SetCount of int
    | Reset
    | Token of string
    | NoToken
// | Login of Async<string option>
// | InitializeDatabase

type State =
    { count: int
      token: string option
      conn: IDbConnection
      loggedIn: bool }


let init () =

    let conn = Database.connection
    // Cmd.OfAsync.start (Database.createIfNotExist conn)

    { conn = conn
      count = 0
      token = None
      loggedIn = false },
    Cmd.none
// Cmd.OfAsync.perform Wanikani.AccessToken.tryGet conn (Option.map Token >> Option.defaultValue NoToken)

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | Increment -> { state with count = state.count + 1 }, Cmd.none
    | Decrement -> { state with count = state.count - 1 }, Cmd.none
    | SetCount count -> { state with count = count }, Cmd.none
    | Reset -> init ()
    | Token token ->
        { state with
            token = Some token
            loggedIn = true },
        Cmd.none
    | NoToken ->
        { state with
            token = None
            loggedIn = false },
        Cmd.none

let view (state: State) (dispatch) =
    DockPanel.create
        [ DockPanel.children
              [ TextBlock.create
                    [ //TextBlock.dock Dock.Top
                      TextBlock.fontSize 48.0
                      TextBlock.verticalAlignment VerticalAlignment.Center
                      TextBlock.horizontalAlignment HorizontalAlignment.Center
                      TextBlock.text (state.token |> Option.defaultValue "No token") ]
                Button.create
                    [ Button.dock Dock.Bottom
                      Button.onClick (fun _ -> dispatch Increment)
                      Button.content "Login"
                      Button.horizontalAlignment HorizontalAlignment.Stretch ]
                // Button.create
                //     [ Button.dock Dock.Bottom
                //       Button.onClick (fun _ -> dispatch Decrement)
                //       Button.content "-"
                //       Button.horizontalAlignment HorizontalAlignment.Stretch ]
                // Button.create
                //     [ Button.dock Dock.Bottom
                //       Button.onClick (fun _ -> dispatch Increment)
                //       Button.content "+"
                //       Button.horizontalAlignment HorizontalAlignment.Stretch ]
                // Button.create
                //     [ Button.dock Dock.Bottom
                //       Button.onClick (
                //           (fun _ -> state.count * 2 |> SetCount |> dispatch),
                //           SubPatchOptions.OnChangeOf state.count
                //       )
                //       Button.content "x2"
                //       Button.horizontalAlignment HorizontalAlignment.Stretch ]
                // TextBox.create
                //     [ TextBox.dock Dock.Bottom
                //       TextBox.onTextChanged (
                //           (fun text ->
                //               let isNumber, number = System.Int32.TryParse text

                //               if isNumber then
                //                   number |> SetCount |> dispatch)
                //       )
                //       TextBox.text (string state.count)
                //       TextBox.horizontalAlignment HorizontalAlignment.Stretch ]
                ] ]
