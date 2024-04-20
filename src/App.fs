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
    | Login
    | UserOverview

type Msg =
    | DbInitialized
    | LoginMsg of Login.Msg
    | UserOverviewMsg of UserOverview.Msg

type State =
    { currentView: View
      login: Login.State
      user: UserOverview.State option }

let conn = Database.connection

let init () =
    let loginState, loginCmd = Login.init ()

    let initialState =
        { currentView = Login
          login = loginState
          user = None }

    let initialCmd =
        Cmd.batch
            [ Cmd.OfAsync.perform Database.createIfNotExist conn (fun _ -> DbInitialized)
              Cmd.map LoginMsg loginCmd ]

    initialState, initialCmd

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | DbInitialized -> state, Cmd.none
    | LoginMsg(Login.LoggedIn(token, user)) ->
        let userState, userCmd = UserOverview.init token user

        { state with
            login = Login.init () |> fst
            user = Some userState
            currentView = UserOverview },
        Cmd.map UserOverviewMsg userCmd
    | LoginMsg loginMsg ->
        let loginState, loginCmd = Login.update loginMsg state.login

        { state with login = loginState }, Cmd.map LoginMsg loginCmd
    | UserOverviewMsg UserOverview.Logout ->
        { state with
            user = None
            currentView = Login },
        Cmd.none
    | UserOverviewMsg userMsg ->
        let userOverviewState, userOverviewCmd =
            UserOverview.update userMsg state.user.Value

        { state with
            user = Some userOverviewState },
        Cmd.map UserOverviewMsg userOverviewCmd

let view (state: State) (dispatch: Msg -> unit) =
    match state.currentView with
    | Login -> Login.view state.login (LoginMsg >> dispatch)
    | UserOverview -> UserOverview.view state.user.Value (UserOverviewMsg >> dispatch)
