namespace WanigraphyApp

open FsHttp
open Wanikani
open Database

// async {
//     // Connect and initialize db
//     use! conn = Database.connection
//     do! Database.createIfNotExist conn

//     // Get user access token from either db or the user
//     let! token = AccessToken.get conn

//     // Fetch the resources not stored in cache straight from the Wanikani API
//     let! user = User.request token
//     let! summary = Summary.request token

//     // Fetch and save latest changes of resources resources that are stored in cache
//     do!
//         [ ReviewStatistics.refresh conn token
//           Assignment.refresh conn token
//           LevelProgression.refresh conn token
//           Review.refresh conn token ]
//         |> Async.Parallel
//         |> Async.Ignore

//     // Read the updated resources from cache
//     let! reviewStatistics = ReviewStatistics.getCached conn
//     let! assignments = Assignment.getCached conn
//     let! levelProgression = LevelProgression.getCached conn
//     let! review = Review.getCached conn

//     return 0
// }
// |> Async.RunSynchronously

open Avalonia
open Avalonia.Controls
open Avalonia.Themes.Fluent
open Elmish
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish
open Avalonia.Controls.ApplicationLifetimes

open App

type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "Wanigraphy"
        base.Icon <- WindowIcon(System.IO.Path.Combine("Assets", "Icons", "turtle.ico"))
        base.Height <- 2000.0
        base.Width <- 2000.0

        Elmish.Program.mkProgram App.init App.update App.view
        |> Program.withHost this
        |> Program.withConsoleTrace
        |> Program.runWithAvaloniaSyncDispatch ()

type Wanigraphy() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            let mainWindow = MainWindow()
            desktopLifetime.MainWindow <- mainWindow
        | _ -> ()

module Program =

    GlobalConfig.Json.defaultJsonSerializerOptions <- Utility.serializerOptions

    [<EntryPoint>]
    let main (args: string[]) =
        AppBuilder
            .Configure<Wanigraphy>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
