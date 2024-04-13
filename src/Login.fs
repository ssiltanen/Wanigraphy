[<RequireQualifiedAccess>]
module Login

open System
open System.Data
open Elmish
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.Layout
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

let conn = Database.connection ()

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
        | Some input ->
            { state with tokenInput = None },
            Cmd.OfAsync.perform (AccessToken.save conn) input (fun _ -> UseToken state.tokenInput)
        | None -> state, Cmd.none
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
    StackPanel.create
        [ StackPanel.orientation Orientation.Horizontal
          StackPanel.horizontalAlignment HorizontalAlignment.Center
          StackPanel.verticalAlignment VerticalAlignment.Center
          StackPanel.spacing 10
          StackPanel.children
              [ TextBox.create
                    [ TextBox.passwordChar '*'
                      TextBox.width 400
                      TextBox.verticalAlignment VerticalAlignment.Top
                      TextBox.hasErrors hasErrors
                      TextBox.onTextChanged (fun input ->
                          if String.IsNullOrWhiteSpace input |> not then
                              TokenInputChanged input |> dispatch) ]
                Button.create
                    [ Button.onClick (fun _ -> dispatch SaveToken)
                      // Button.isEnabled (Option.isSome inputValue)
                      Button.verticalAlignment VerticalAlignment.Top
                      Button.content "Login" ] ] ]

let view (state: State) (dispatch: Msg -> unit) =
    DockPanel.create
        [ DockPanel.children (
              match state.userFetchAttempt, state.token with
              | Finished user, Some token -> []

              | Finished user, None ->
                  [ TextBlock.create
                        [ TextBlock.text "Unexpected error"
                          TextBlock.verticalAlignment VerticalAlignment.Center ] ]

              | InProgress, _
              | NotStarted, Some _ ->
                  [ TextBlock.create
                        [ TextBlock.text "Loading"
                          TextBlock.horizontalAlignment HorizontalAlignment.Center
                          TextBlock.verticalAlignment VerticalAlignment.Center ] ]

              | NotStarted, None -> [ inputTokenView state dispatch false ]
              | Error ex, _ -> [ inputTokenView state dispatch true ]
          ) ]
