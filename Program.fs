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

        // Get user access token
        let! token = AccessToken.get conn

        // Fetch data from API
        let! user = User.request token
        let! summary = Summary.request token

        // Fetch and save latest statistic data
        do!
            ReviewStatistics.getLatestUpdateTime conn
            |> Async.bind (ReviewStatistics.request token)
            |> Async.bind (ReviewStatistics.save conn)

        return 0
    }
    |> Async.RunSynchronously
