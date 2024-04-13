module App

open System
open System.Data
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media.Imaging
open Avalonia.Platform
open Avalonia.Svg.Skia
open Elmish
open ElmishUtility

open Wanikani
open MetaType

type Msg =
    | DbInitialized
    | UseToken of string option
    | TokenInputChanged of string
    | SaveToken
    | FetchUserAttempt of AsyncOp<Object<User>>

type State =
    { token: string option
      tokenInput: string option
      user: AsyncOp<Object<User>> }

let conn = Database.connection ()

let init () =
    { token = None
      tokenInput = None
      user = NotStarted },
    Cmd.OfAsync.perform Database.createIfNotExist conn (fun _ -> DbInitialized)

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | DbInitialized -> state, Cmd.OfAsync.perform AccessToken.tryGet conn UseToken
    | TokenInputChanged input -> { state with tokenInput = Some input }, Cmd.none
    | UseToken(Some token) ->
        { state with
            token = Some token
            tokenInput = None
            user = InProgress },
        Cmd.OfAsync.either User.request token (Finished >> FetchUserAttempt) (Error >> FetchUserAttempt)
    | UseToken None ->
        Cmd.OfAsync.start (AccessToken.delete conn)

        { state with
            token = None
            tokenInput = None
            user = NotStarted },
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
            user = Error e
            token = None },
        Cmd.none
    | FetchUserAttempt user -> { state with user = user }, Cmd.none

[<RequireQualifiedAccess>]
module Icons =
    let user =
        lazy new SvgImage(Source = SvgSource.Load("avares://Wanigraphy/Assets/Icons/turtle.svg", null))

let view (state: State) (dispatch) =
    DockPanel.create
        [ DockPanel.children (
              match state.user, state.token with
              | Finished user, Some token ->
                  [ StackPanel.create
                        [ StackPanel.orientation Orientation.Horizontal
                          StackPanel.horizontalAlignment HorizontalAlignment.Right
                          StackPanel.dock Dock.Top
                          StackPanel.margin 10
                          StackPanel.children
                              [ Button.create
                                    [ Button.content (
                                          StackPanel.create
                                              [ StackPanel.orientation Orientation.Horizontal
                                                StackPanel.spacing 10
                                                StackPanel.children
                                                    [ TextBlock.create
                                                          [ TextBlock.text user.data.username
                                                            TextBlock.verticalAlignment VerticalAlignment.Center
                                                            TextBlock.fontSize 16 ]

                                                      Image.create
                                                          [ Image.source Icons.user.Value
                                                            Image.height 25
                                                            Image.width 25 ] ] ]
                                      )
                                      Button.flyout (
                                          MenuFlyout.create
                                              [ MenuFlyout.placement PlacementMode.BottomEdgeAlignedRight
                                                MenuFlyout.showMode FlyoutShowMode.TransientWithDismissOnPointerMoveAway
                                                MenuFlyout.viewItems
                                                    [ MenuItem.create
                                                          [ MenuItem.header "Logout"
                                                            MenuItem.onClick (fun _ -> dispatch (UseToken None)) ] ] ]
                                      ) ] ] ]
                    StackPanel.create
                        [ StackPanel.orientation Orientation.Horizontal
                          StackPanel.horizontalAlignment HorizontalAlignment.Center
                          StackPanel.verticalAlignment VerticalAlignment.Center
                          StackPanel.spacing 10
                          StackPanel.children
                              [ TextBlock.create
                                    [ TextBlock.fontSize 48
                                      TextBlock.verticalAlignment VerticalAlignment.Center
                                      TextBlock.text $"Welcome {user.data.username}" ] ] ] ]

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

              | NotStarted, None ->
                  [ StackPanel.create
                        [ StackPanel.orientation Orientation.Horizontal
                          StackPanel.horizontalAlignment HorizontalAlignment.Center
                          StackPanel.verticalAlignment VerticalAlignment.Center
                          StackPanel.spacing 10
                          StackPanel.children
                              [ TextBox.create
                                    [ TextBox.passwordChar '*'
                                      TextBox.width 400
                                      TextBox.verticalAlignment VerticalAlignment.Top
                                      TextBox.onTextChanged (fun input ->
                                          if String.IsNullOrWhiteSpace input |> not then
                                              TokenInputChanged input |> dispatch) ]
                                Button.create
                                    [ Button.onClick (fun _ -> dispatch SaveToken)
                                      Button.isEnabled (Option.isSome state.tokenInput)
                                      Button.verticalAlignment VerticalAlignment.Top
                                      Button.content "Login" ] ] ] ]

              | Error ex, _ ->
                  [ StackPanel.create
                        [ StackPanel.orientation Orientation.Horizontal
                          StackPanel.horizontalAlignment HorizontalAlignment.Center
                          StackPanel.verticalAlignment VerticalAlignment.Center
                          StackPanel.spacing 10
                          StackPanel.height 60
                          StackPanel.children
                              [ TextBox.create
                                    [ TextBox.passwordChar '*'
                                      TextBox.width 400
                                      TextBox.errors [ "Invalid token" ]
                                      TextBox.verticalAlignment VerticalAlignment.Top
                                      TextBox.onTextChanged (fun input ->
                                          if String.IsNullOrWhiteSpace input |> not then
                                              TokenInputChanged input |> dispatch) ]
                                Button.create
                                    [ Button.onClick (fun _ -> dispatch SaveToken)
                                      Button.isEnabled (Option.isSome state.tokenInput)
                                      Button.verticalAlignment VerticalAlignment.Top
                                      Button.content "Login" ] ] ] ]
          ) ]
