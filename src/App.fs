module App

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

type View =
    | LoginView
    | MainView

type Msg =
    | DbInitialized
    | LoginMsg of Login.Msg
    | MainMsg of MainView.Msg

type State =
    { currentView: View
      login: Login.State
      main: MainView.State option }

let conn = Database.connection

let init () =
    let loginState, loginCmd = Login.init ()

    let initialState =
        { currentView = LoginView
          login = loginState
          main = None }

    let initialCmd =
        Cmd.batch
            [ Cmd.OfAsync.perform Database.createIfNotExist conn (fun _ -> DbInitialized)
              Cmd.map LoginMsg loginCmd ]

    initialState, initialCmd

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | DbInitialized -> state, Cmd.none
    | LoginMsg(Login.LoggedIn(token, user)) ->
        let mainState, mainCmd = MainView.init token user

        { state with
            login = Login.init () |> fst
            main = Some mainState
            currentView = MainView },
        Cmd.map MainMsg mainCmd
    | LoginMsg loginMsg ->
        let loginState, loginCmd = Login.update loginMsg state.login

        { state with login = loginState }, Cmd.map LoginMsg loginCmd
    | MainMsg MainView.Logout ->
        { state with
            main = None
            currentView = LoginView },
        Cmd.none
    | MainMsg mainMsg ->
        match state.main with
        | Some main ->
            let mainState, mainCmd = MainView.update mainMsg main
            { state with main = Some mainState }, Cmd.map MainMsg mainCmd
        | None -> state, Cmd.none

let view (state: State) (dispatch: Msg -> unit) =
    match state.currentView with
    | LoginView -> Login.view state.login (LoginMsg >> dispatch)
    | MainView -> MainView.view state.main.Value (MainMsg >> dispatch)
