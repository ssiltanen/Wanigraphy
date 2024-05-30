[<RequireQualifiedAccess>]
module MainView

open System
open System.Data
open System.Diagnostics
open System.Runtime.InteropServices
open Elmish
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Platform
open Avalonia.Svg.Skia
open ElmishUtility

open Wanikani
open Highlights
open MetaType

type Link = WanikaniProfile

type Msg =
    | OpenSplitViewPane
    | CloseSplitViewPane
    | Logout
    | ClearDatabase
    | SummaryFetchAttempt of AsyncOp<Object<Summary>>
    | AssignmentFetchAttempt of AsyncOp<Resource<Assignment>[]>
    | ReviewFetchAttempt of AsyncOp<Resource<Review>[]>
    | ReviewStatisticsFetchAttempt of AsyncOp<Resource<ReviewStatistics>[]>
    | LevelProgressionFetchAttempt of AsyncOp<Resource<LevelProgression>[]>
    | OpenUrl of Link

type State =
    { isPaneOpen: bool
      token: string
      user: Object<User>
      summary: AsyncOp<Object<Summary>>
      assignments: AsyncOp<Resource<Assignment>[]>
      reviews: AsyncOp<Resource<Review>[]>
      reviewStatistics: AsyncOp<Resource<ReviewStatistics>[]>
      levelProgression: AsyncOp<Resource<LevelProgression>[]> }

let conn = Database.connection

let openUrl (url: string) =
    let isOS = RuntimeInformation.IsOSPlatform

    if isOS OSPlatform.OSX then
        Some("open", url)
    elif isOS OSPlatform.Linux then
        Some("xdg-open", url)
    elif isOS OSPlatform.Windows then
        Some("cmd", $"/c start {url}")
    else
        Console.WriteLine "Unknown OS"
        None
    |> Option.iter (ProcessStartInfo >> Process.Start >> ignore)

let init token user =
    { isPaneOpen = false
      token = token
      user = user
      summary = InProgress
      assignments = InProgress
      reviews = InProgress
      reviewStatistics = InProgress
      levelProgression = InProgress },
    Cmd.batch
        [ Cmd.OfAsync.either Summary.request token (Finished >> SummaryFetchAttempt) (Error >> SummaryFetchAttempt)

          Cmd.OfAsync.either
              (fun _ -> Assignment.refreshAndRead conn token)
              ()
              (Finished >> AssignmentFetchAttempt)
              (Error >> AssignmentFetchAttempt)

          Cmd.OfAsync.either
              (fun _ -> Review.refreshAndRead conn token)
              ()
              (Finished >> ReviewFetchAttempt)
              (Error >> ReviewFetchAttempt)

          Cmd.OfAsync.either
              (fun _ -> ReviewStatistics.refreshAndRead conn token)
              ()
              (Finished >> ReviewStatisticsFetchAttempt)
              (Error >> ReviewStatisticsFetchAttempt)

          Cmd.OfAsync.either
              (fun _ -> LevelProgression.refreshAndRead conn token)
              ()
              (Finished >> LevelProgressionFetchAttempt)
              (Error >> ReviewStatisticsFetchAttempt) ]

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | OpenSplitViewPane -> { state with isPaneOpen = true }, Cmd.none
    | CloseSplitViewPane -> { state with isPaneOpen = false }, Cmd.none
    | Logout -> state, Cmd.none // Handled on App.fs
    | ClearDatabase -> state, Cmd.OfAsync.perform deleteStoredData conn (fun _ -> Logout)
    | SummaryFetchAttempt result -> { state with summary = result }, Cmd.none
    | AssignmentFetchAttempt result -> { state with assignments = result }, Cmd.none
    | ReviewFetchAttempt result -> { state with reviews = result }, Cmd.none
    | ReviewStatisticsFetchAttempt result -> { state with reviewStatistics = result }, Cmd.none
    | LevelProgressionFetchAttempt result -> { state with levelProgression = result }, Cmd.none
    | OpenUrl link ->
        match link with
        | WanikaniProfile -> state.user.data.profile_url.AbsoluteUri
        |> openUrl

        state, Cmd.none

let WanigraphyIcon =
    Image.create
        [ Image.horizontalAlignment HorizontalAlignment.Center
          Image.source Icons.user.Value
          Image.height 40
          Image.width 40 ]

let sidePanel state dispatch =
    Grid.create
        [ // Grid.showGridLines true
          Grid.rowDefinitions "10, auto, 30, auto, auto, auto, *, auto"
          Grid.columnDefinitions "10, *, 10"
          Grid.children (
              match state.isPaneOpen with
              | false -> [ Panel.create [ Grid.row 1; Grid.column 1; Panel.children [ WanigraphyIcon ] ] ]
              | true ->
                  [ StackPanel.create
                        [ Grid.row 1
                          Grid.column 1
                          StackPanel.margin 0
                          StackPanel.orientation Orientation.Horizontal
                          StackPanel.spacing 10
                          StackPanel.children
                              [ WanigraphyIcon
                                TextBlock.create
                                    [ TextBlock.verticalAlignment VerticalAlignment.Bottom
                                      TextBlock.fontSize 28
                                      TextBlock.fontWeight FontWeight.Bold
                                      TextBlock.text "Wanigraphy" ] ] ]

                    Separator.create [ Grid.row 2; Grid.column 0; Grid.columnSpan 3 ]

                    TextBlock.create
                        [ Grid.row 3
                          Grid.column 1
                          TextBlock.fontSize 20
                          TextBlock.text state.user.data.username ]

                    TextBlock.create
                        [ Grid.row 4
                          Grid.column 1
                          TextBlock.margin (0, 1, 0, 0)
                          TextBlock.text $"Level {state.user.data.level}" ]

                    TextBlock.create
                        [ Grid.row 5
                          Grid.column 1
                          TextBlock.margin (0, 10, 0, 0)
                          TextBlock.classes [ "link" ]
                          TextBlock.text "Go to profile"
                          TextBlock.onTapped (fun _ -> dispatch (OpenUrl WanikaniProfile)) ]

                    Button.create
                        [ Grid.row 7
                          Grid.column 0
                          Grid.columnSpan 3
                          Button.horizontalAlignment HorizontalAlignment.Stretch
                          Button.height 60
                          Button.background Color.secondary
                          Button.content (
                              TextBlock.create
                                  [ TextBlock.horizontalAlignment HorizontalAlignment.Center
                                    TextBlock.verticalAlignment VerticalAlignment.Center
                                    TextBlock.fontSize 18
                                    TextBlock.fontWeight FontWeight.Bold
                                    TextBlock.text "Logout" ]
                          )
                          Button.onClick (fun _ -> dispatch ClearDatabase) ] ]
          ) ]


let mainContent (state: State) dispatch =
    Grid.create
        [ Grid.horizontalAlignment HorizontalAlignment.Center
          Grid.rowDefinitions "auto auto"
          Grid.children
              [ Panel.create
                    [ Grid.row 0
                      Panel.margin (0, 10)
                      Panel.children [ SrsStage.countView state.assignments ] ]
                TextBlock.create [ Grid.row 1; TextBlock.text "Main content" ] ] ]

let view (state: State) (dispatch: Msg -> unit) =
    Grid.create
        [ Grid.background Color.background
          Grid.children
              [ SplitView.create
                    [ SplitView.panePlacement SplitViewPanePlacement.Left
                      SplitView.paneBackground Color.primary
                      SplitView.openPaneLength 250
                      SplitView.compactPaneLengthProperty 60
                      SplitView.isPaneOpen state.isPaneOpen
                      SplitView.displayMode SplitViewDisplayMode.CompactOverlay
                      SplitView.useLightDismissOverlayMode true
                      SplitView.onPointerEntered (fun e ->
                          // Unfortunately, SplitView triggers this event on both pane, and content.
                          // To send our event only on side pane pointer enter, we check the pointer position
                          let point = e.GetPosition(null)

                          if point.X < 60 then
                              dispatch OpenSplitViewPane)
                      SplitView.onPointerExited (fun _ -> dispatch CloseSplitViewPane)

                      sidePanel state dispatch |> SplitView.pane
                      mainContent state dispatch |> SplitView.content ] ] ]
