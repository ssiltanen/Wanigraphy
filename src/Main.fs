module Main

open System
open System.Data
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.Layout
open Elmish
open ElmishUtility

open Wanikani

type Msg =
    | DbInitialized
    | UseToken of string option
    | TokenInputChanged of string
    | SaveToken
    | DeleteToken

type State =
    { token: string option
      tokenInput: string option }

let conn = Database.connection ()

let init () =
    { token = None; tokenInput = None }, Cmd.OfAsync.perform Database.createIfNotExist conn (fun _ -> DbInitialized)

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | DbInitialized -> state, Cmd.OfAsync.perform AccessToken.tryGet conn UseToken
    | UseToken token -> { state with token = token }, Cmd.none
    | TokenInputChanged input -> { state with tokenInput = Some input }, Cmd.none
    | SaveToken ->
        match state.tokenInput with
        | Some input ->
            { state with tokenInput = None },
            Cmd.OfAsync.perform (AccessToken.save conn) input (fun _ -> UseToken state.tokenInput)
        | None -> state, Cmd.none
    | DeleteToken -> state, Cmd.OfAsync.perform AccessToken.delete conn (fun _ -> UseToken None)

let view (state: State) (dispatch) =
    DockPanel.create
        [ DockPanel.children
              [ StackPanel.create
                    [ StackPanel.orientation Orientation.Horizontal
                      StackPanel.horizontalAlignment HorizontalAlignment.Center
                      StackPanel.spacing 10.0
                      StackPanel.children (
                          match state.token with
                          | Some token ->
                              [ TextBlock.create
                                    [ TextBlock.fontSize 48.0
                                      TextBlock.verticalAlignment VerticalAlignment.Center
                                      TextBlock.text (state.token |> Option.defaultValue "No token") ]

                                Button.create
                                    [ Button.onClick (fun _ -> dispatch DeleteToken)
                                      Button.content "Change Token" ] ]
                          | None ->
                              [ TextBox.create
                                    [ TextBox.passwordChar '*'
                                      TextBox.width 500.0
                                      TextBox.verticalAlignment VerticalAlignment.Center
                                      TextBox.onTextChanged (fun input ->
                                          if String.IsNullOrWhiteSpace input |> not then
                                              TokenInputChanged input |> dispatch) ]
                                Button.create
                                    [ Button.onClick (fun _ -> dispatch SaveToken)
                                      Button.isEnabled (Option.isSome state.tokenInput)
                                      Button.content "Save" ] ]
                      ) ] ] ]
