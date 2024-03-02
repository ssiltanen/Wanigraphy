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

        // Fetch the resources not stored in cache straight from the Wanikani API
        let! user = User.request token
        let! summary = Summary.request token

        // Fetch and save latest changes of resources resources that are stored in cache
        do!
            [ ReviewStatistics.refresh conn token
              Assignment.refresh conn token
              LevelProgression.refresh conn token
              Review.refresh conn token ]
            |> Async.Parallel
            |> Async.Ignore

        // Read the updated resources from cache
        let! reviewStatistics = ReviewStatistics.getCached conn
        let! assignments = Assignment.getCached conn
        let! levelProgression = LevelProgression.getCached conn
        let! review = Review.getCached conn

        return 0
    }
    |> Async.RunSynchronously
