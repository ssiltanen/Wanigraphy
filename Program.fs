module Wanigraphy

open FsHttp
open Wanikani
open Database

GlobalConfig.Json.defaultJsonSerializerOptions <- Utility.serializerOptions

[<EntryPoint>]
let main argv =

    async {
        // Connect and initialize db
        use! conn = Database.connection
        do! Database.createIfNotExist conn

        // Get user access token from either db or the user
        let! token = AccessToken.get conn

        // Fetch data from the Wanikani API
        let! user = User.request token
        let! summary = Summary.request token

        do!
            [ ReviewStatistics.refresh conn token
              Assignment.refresh conn token
              LevelProgression.refresh conn token
              Review.refresh conn token ]
            |> Async.Parallel
            |> Async.Ignore

        return 0
    }
    |> Async.RunSynchronously
