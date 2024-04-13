[<RequireQualifiedAccess>]
module UserOverview

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
    | Logout
    | ClearDatabase

type State = { token: string; user: Object<User> }

let conn = Database.connection ()

let init token user =
    { token = token; user = user }, Cmd.none

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | Logout -> state, Cmd.none // Handled on App.fs
    | ClearDatabase -> state, Cmd.OfAsync.perform deleteStoredData conn (fun _ -> Logout)

[<RequireQualifiedAccess>]
module Icons =
    let user =
        lazy new SvgImage(Source = SvgSource.Load("avares://Wanigraphy/Assets/Icons/turtle.svg", null))

let view ({ user = user }: State) (dispatch: Msg -> unit) =
    DockPanel.create
        [ DockPanel.children
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
                                                      [ Image.source Icons.user.Value; Image.height 25; Image.width 25 ] ] ]
                                  )
                                  Button.flyout (
                                      MenuFlyout.create
                                          [ MenuFlyout.placement PlacementMode.BottomEdgeAlignedRight
                                            MenuFlyout.showMode FlyoutShowMode.TransientWithDismissOnPointerMoveAway
                                            MenuFlyout.viewItems
                                                [ MenuItem.create
                                                      [ MenuItem.header "Logout"
                                                        MenuItem.onClick (fun _ -> dispatch ClearDatabase) ] ] ]
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

          ]
