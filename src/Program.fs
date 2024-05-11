namespace WanigraphyApp

open Avalonia
open Avalonia.Controls
open Avalonia.Themes.Fluent
open Elmish
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish
open Avalonia.Controls.ApplicationLifetimes

open FsHttp
open Wanikani
open Database
open App

type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "Wanigraphy"
        base.Icon <- WindowIcon(System.IO.Path.Combine("Assets", "Icons", "turtle.ico"))
        base.Height <- 2000
        base.Width <- 2000

        Elmish.Program.mkProgram App.init App.update App.view
        |> Program.withHost this
        // |> Program.withConsoleTrace
        |> Program.runWithAvaloniaSyncDispatch ()

type Wanigraphy() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.Styles.Load "avares://Wanigraphy/Styles.xaml"
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
