module Highlights

open System
open System.Collections.Generic
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Platform
open Avalonia.Svg.Skia
open ElmishUtility

open MetaType

[<RequireQualifiedAccess>]
module SrsStage =

    open Wanikani

    type Stage =
        { name: string
          subStageName: string option }

    let subCountText row column text =
        TextBlock.create
            [ Grid.row row
              Grid.column column
              TextBlock.fontSize 14
              TextBlock.verticalAlignment VerticalAlignment.Center
              TextBlock.horizontalAlignment HorizontalAlignment.Center
              TextBlock.text text ]

    let stageName row column columnSpan name =
        TextBlock.create
            [ Grid.row row
              Grid.column column
              Grid.columnSpan columnSpan
              TextBlock.fontSize 18
              TextBlock.verticalAlignment VerticalAlignment.Center
              TextBlock.horizontalAlignment HorizontalAlignment.Center
              TextBlock.text name ]

    let stagePanels
        (apprentice: Stage[] option)
        (guru: Stage[] option)
        (master: Stage[] option)
        (enlightened: Stage[] option)
        (burned: Stage[] option)
        =
        let tryGet (id: string) (d: IDictionary<string, int>) =
            match d.TryGetValue(id) with
            | true, value -> Some value
            | _ -> Some 0
            |> Option.map string

        let getOrEmpty name =
            Option.bind (tryGet "name") >> Option.defaultValue ""

        let subCounts =
            Option.map (Array.countBy (_.subStageName >> Option.defaultValue "") >> dict)

        let subApprentices = subCounts apprentice
        let apprenticeI = subApprentices |> getOrEmpty "I"
        let apprenticeII = subApprentices |> getOrEmpty "II"
        let apprenticeIII = subApprentices |> getOrEmpty "III"
        let apprenticeIV = subApprentices |> getOrEmpty "IV"

        let subGurus = subCounts guru
        let guruI = subGurus |> getOrEmpty "I"
        let guruII = subGurus |> getOrEmpty "II"


        StackPanel.create
            [ StackPanel.orientation Orientation.Horizontal
              StackPanel.spacing 10
              StackPanel.children
                  [ // Apprentice
                    Grid.create
                        [ Grid.rowDefinitions "* 35 25"
                          Grid.columnDefinitions "60 60 60 60"
                          Grid.verticalAlignment VerticalAlignment.Center
                          Grid.horizontalAlignment HorizontalAlignment.Center
                          Grid.width 240
                          Grid.height 140
                          Grid.background Color.apprentice
                          Grid.children
                              [ TextBlock.create
                                    [ Grid.row 0
                                      Grid.column 0
                                      Grid.columnSpan 4
                                      TextBlock.verticalAlignment VerticalAlignment.Center
                                      TextBlock.horizontalAlignment HorizontalAlignment.Center
                                      TextBlock.fontSize 28
                                      TextBlock.text (
                                          apprentice |> Option.map (Array.length >> string) |> Option.defaultValue ""
                                      ) ]

                                subCountText 1 0 $"I: {apprenticeI}"
                                subCountText 1 1 $"II: {apprenticeII}"
                                subCountText 1 2 $"III: {apprenticeIII}"
                                subCountText 1 3 $"IV: {apprenticeIV}"

                                stageName 2 0 4 "Apprentice" ] ]

                    // Guru
                    Grid.create
                        [ Grid.rowDefinitions "* 35 25"
                          Grid.columnDefinitions "60 60 60 60"
                          Grid.width 240
                          Grid.height 140
                          Grid.background Color.guru
                          Grid.children
                              [ TextBlock.create
                                    [ Grid.row 0
                                      Grid.column 0
                                      Grid.columnSpan 4
                                      TextBlock.verticalAlignment VerticalAlignment.Center
                                      TextBlock.horizontalAlignment HorizontalAlignment.Center
                                      TextBlock.fontSize 28
                                      TextBlock.text (
                                          guru |> Option.map (Array.length >> string) |> Option.defaultValue ""
                                      ) ]

                                subCountText 1 1 $"I: {guruI}"
                                subCountText 1 2 $"II: {guruII}"

                                stageName 2 0 4 "Guru"

                                ] ]

                    // Master
                    Grid.create
                        [ Grid.rowDefinitions "* 35 25"
                          Grid.width 240
                          Grid.height 140
                          Grid.background Color.master
                          Grid.children
                              [ TextBlock.create
                                    [ Grid.row 0
                                      TextBlock.verticalAlignment VerticalAlignment.Center
                                      TextBlock.horizontalAlignment HorizontalAlignment.Center
                                      TextBlock.fontSize 28
                                      TextBlock.text (
                                          master |> Option.map (Array.length >> string) |> Option.defaultValue ""
                                      ) ]
                                stageName 2 0 1 "Master" ] ]

                    // Enlightened
                    Grid.create
                        [ Grid.rowDefinitions "* 35 25"
                          Grid.width 240
                          Grid.height 140
                          Grid.background Color.enlightened
                          Grid.children
                              [ TextBlock.create
                                    [ Grid.row 0
                                      TextBlock.verticalAlignment VerticalAlignment.Center
                                      TextBlock.horizontalAlignment HorizontalAlignment.Center
                                      TextBlock.fontSize 28
                                      TextBlock.text (
                                          enlightened |> Option.map (Array.length >> string) |> Option.defaultValue ""
                                      ) ]

                                stageName 2 0 1 "enlightened" ] ]

                    // Burned
                    Grid.create
                        [ Grid.rowDefinitions "* 35 25"
                          Grid.width 240
                          Grid.height 140
                          Grid.background Color.burned
                          Grid.children
                              [ TextBlock.create
                                    [ Grid.row 0
                                      TextBlock.verticalAlignment VerticalAlignment.Center
                                      TextBlock.horizontalAlignment HorizontalAlignment.Center
                                      TextBlock.fontSize 28
                                      TextBlock.text (
                                          burned |> Option.map (Array.length >> string) |> Option.defaultValue ""
                                      ) ]

                                stageName 2 0 1 "Burned" ] ]

                    ] ]

    let countView (assignments: AsyncOp<Resource<Assignment>[]>) =
        match assignments with
        | Finished assignments ->
            let stages =
                assignments
                |> Array.map (
                    _.data
                    >> _.srs_stage
                    >> function
                        | SrsStage.Initiate ->
                            { name = "Initiate"
                              subStageName = None }
                        | SrsStage.Apprentice1 ->
                            { name = "Apprentice"
                              subStageName = Some "I" }
                        | SrsStage.Apprentice2 ->
                            { name = "Apprentice"
                              subStageName = Some "II" }
                        | SrsStage.Apprentice3 ->
                            { name = "Apprentice"
                              subStageName = Some "III" }
                        | SrsStage.Apprentice4 ->
                            { name = "Apprentice"
                              subStageName = Some "IV" }
                        | SrsStage.Guru1 ->
                            { name = "Guru"
                              subStageName = Some "I" }
                        | SrsStage.Guru2 ->
                            { name = "Guru"
                              subStageName = Some "II" }
                        | SrsStage.Master -> { name = "Master"; subStageName = None }
                        | SrsStage.Enlightened ->
                            { name = "Enlightened"
                              subStageName = None }
                        | SrsStage.Burned -> { name = "Burned"; subStageName = None }
                        | s -> failwithf "Invalid stage: %A" s
                )
                |> Array.groupBy _.name
                |> Array.filter (fst >> (<>) "Initiate")
                |> dict

            stagePanels
                (Some stages["Apprentice"])
                (Some stages["Guru"])
                (Some stages["Master"])
                (Some stages["Enlightened"])
                (Some stages["Burned"])

        | _ -> stagePanels None None None None None
