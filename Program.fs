module Wanigraphy

open System
open System.Data
open System.Text.Json
open System.Text.Json.Serialization
open FsHttp
open Wanikani
open Database

let serializerOptions =
    JsonFSharpOptions
        .Default()
        .WithAllowNullFields()
        .WithUnionTagCaseInsensitive()
        .WithUnionExternalTag()
        .WithUnionUnwrapFieldlessTags()
        .WithUnionUnwrapSingleFieldCases()
        .ToJsonSerializerOptions()

GlobalConfig.Json.defaultJsonSerializerOptions <- serializerOptions

let getAccessToken (conn: IDbConnection) =
    Database.AccessToken.tryGet conn
    |> Async.bind (function
        | Some { token = token } -> async.Return token
        | None ->
            async {
                Console.WriteLine "Input Wanikani Access Token:"
                let token = Console.ReadLine()
                do! Database.AccessToken.save conn token
                return token
            })

let mapToDb (stats: Collection<Resource<Wanikani.Review.Statistics>>[]) : Database.Review.Statistics[] =
    let serialize data =
        JsonSerializer.Serialize(data, serializerOptions)

    stats
    |> Array.collect _.data
    |> Array.map (fun stat ->
        { id = stat.id
          subject_id = stat.data.subject_id
          subject_type = serialize stat.data.subject_type
          created_at = stat.data.created_at
          updated_at = stat.data_updated_at
          data = serialize stat.data })


[<EntryPoint>]
let main argv =

    async {
        // Connect and initialize db
        use! conn = Database.connection ()
        do! Database.createIfNotExist conn

        // Get user access token
        let! token = getAccessToken conn

        // Fetch data from API
        let! user = User.get token
        let! summary = Summary.get token

        // Fetch and save latest statistic data
        do!
            Database.Review.tryGetLatestStatisticsUpdateTime conn
            |> Async.bind (Review.getAllStatistics token)
            |> Async.bind (mapToDb >> Database.Review.saveStats conn)

        return 0
    }
    |> Async.RunSynchronously
