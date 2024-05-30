[<RequireQualifiedAccess>]
module Login

open System
open System.Data
open Elmish
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Platform
open Avalonia.Svg.Skia
open ElmishUtility

open Wanikani
open MetaType

type Msg =
    | UseToken of string option
    | TokenInputChanged of string
    | SaveToken
    | FetchUserAttempt of AsyncOp<Object<User>>
    | LoggedIn of token: string * user: Object<User>

type State =
    { token: string option
      tokenInput: string option
      userFetchAttempt: AsyncOp<Object<User>> }

let conn = Database.connection

let init () =
    { token = None
      tokenInput = None
      userFetchAttempt = NotStarted },
    Cmd.OfAsync.perform AccessToken.tryGet conn UseToken

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | TokenInputChanged input -> { state with tokenInput = Some input }, Cmd.none
    | UseToken(Some token) ->
        { state with
            token = Some token
            tokenInput = None
            userFetchAttempt = InProgress },
        Cmd.OfAsync.either User.request token (Finished >> FetchUserAttempt) (Error >> FetchUserAttempt)
    | UseToken None ->
        Cmd.OfAsync.start (AccessToken.delete conn)

        { state with
            token = None
            tokenInput = None
            userFetchAttempt = NotStarted },
        Cmd.none
    | SaveToken ->
        match state.tokenInput with
        | Some input when input.Length = 36 ->
            { state with tokenInput = None },
            Cmd.OfAsync.perform (AccessToken.save conn) input (fun _ -> UseToken state.tokenInput)
        | _ -> state, Cmd.none
    | FetchUserAttempt(Error e) ->
        Cmd.OfAsync.start (AccessToken.delete conn)

        { state with
            userFetchAttempt = Error e
            token = None },
        Cmd.none
    | FetchUserAttempt(Finished user) ->
        { state with
            userFetchAttempt = Finished user },
        Cmd.ofMsg (LoggedIn(state.token.Value, user))
    | FetchUserAttempt user -> { state with userFetchAttempt = user }, Cmd.none
    | LoggedIn _ -> state, Cmd.none // Handled on App.fs

[<RequireQualifiedAccess>]
module Icons =
    let user =
        lazy new SvgImage(Source = SvgSource.Load("avares://Wanigraphy/Assets/Icons/turtle.svg", null))

let inputTokenView (state: State) (dispatch: Msg -> unit) (hasErrors: bool) : IView =
    let errors =
        seq {
            if hasErrors then
                yield "Invalid API token"

            if
                state.tokenInput
                |> Option.exists (fun input -> input.Length > 0 && input.Length <> 36)
            then
                yield $"{state.tokenInput.Value.Length} out of 36 characters"
        }
        |> Seq.map box

    let hasCorrectLength =
        state.tokenInput |> Option.exists (fun input -> input.Length = 36)

    StackPanel.create
        [ StackPanel.orientation Orientation.Vertical
          StackPanel.spacing 5
          StackPanel.children
              [ TextBlock.create [ TextBlock.text "Personal API Token" ]

                StackPanel.create
                    [ StackPanel.orientation Orientation.Horizontal
                      StackPanel.verticalAlignment VerticalAlignment.Top
                      StackPanel.spacing 10
                      StackPanel.children
                          [ TextBox.create
                                [ TextBox.width 400
                                  TextBox.minHeight 35
                                  TextBox.fontSize 18
                                  TextBox.watermark "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                                  TextBox.passwordChar '*'
                                  TextBox.tip "Get your personal API token from Wanikani settings"
                                  TextBox.hasErrors hasErrors
                                  TextBox.errors errors
                                  TextBox.onTextChanged (Option.ofObj >> Option.iter (TokenInputChanged >> dispatch))
                                  TextBox.onKeyDown (fun evt ->
                                      if evt.Key = Key.Return then
                                          dispatch SaveToken) ]
                            Button.create
                                [ Button.verticalAlignment VerticalAlignment.Top
                                  Button.height 35
                                  Button.onClick (fun _ -> dispatch SaveToken)
                                  Button.isEnabled hasCorrectLength
                                  Button.content (
                                      TextBlock.create
                                          [ TextBlock.verticalAlignment VerticalAlignment.Center
                                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                                            TextBlock.text "Login" ]
                                  ) ] ] ] ] ]

let view (state: State) (dispatch: Msg -> unit) =
    Grid.create
        [ Grid.rowDefinitions "250, auto, *"
          Grid.columnDefinitions "*, auto, *"
          Grid.background Color.background
          Grid.children
              [ TextBlock.create
                    [ Grid.row 0
                      Grid.column 1
                      TextBlock.verticalAlignment VerticalAlignment.Center
                      TextBlock.text "Wanigraphy"
                      TextBlock.fontSize 60
                      TextBlock.textAlignment TextAlignment.Center ]

                Panel.create
                    [ Grid.row 1
                      Grid.column 1
                      Panel.height 60
                      Panel.children (
                          match state.userFetchAttempt, state.token with
                          | Finished user, Some token -> []

                          | Finished user, None ->
                              [ TextBlock.create
                                    [ TextBlock.text "Unexpected error"
                                      TextBlock.fontSize 20
                                      TextBlock.horizontalAlignment HorizontalAlignment.Center
                                      TextBlock.verticalAlignment VerticalAlignment.Center ] ]

                          | InProgress, _
                          | NotStarted, Some _ ->
                              [ TextBlock.create
                                    [ TextBlock.text "Loading..."
                                      TextBlock.fontSize 20
                                      TextBlock.horizontalAlignment HorizontalAlignment.Center
                                      TextBlock.verticalAlignment VerticalAlignment.Center ] ]

                          | NotStarted, None -> [ inputTokenView state dispatch false ]
                          | Error ex, _ -> [ inputTokenView state dispatch true ]
                      ) ] ] ]
