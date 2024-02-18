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
            [ ReviewStatistics.tryGetLatestUpdateTime conn
              |> Async.bind (ReviewStatistics.request token)
              |> Async.bind (ReviewStatistics.save conn)

              Assignment.tryGetLatestUpdateTime conn
              |> Async.bind (Assignment.request token)
              |> Async.bind (Assignment.save conn)

              LevelProgression.tryGetLatestUpdateTime conn
              |> Async.bind (LevelProgression.request token)
              |> Async.bind (Option.iterAsync (LevelProgression.save conn))

              Review.tryGetLatestUpdateTime conn
              |> Async.bind (Review.request token)
              |> Async.bind (Review.save conn) ]
            |> Async.Parallel
            |> Async.Ignore

        return 0
    }
    |> Async.RunSynchronously
