module Wanikani

open System
open System.Net
open System.Text.Json.Serialization
open FsHttp
open FsHttp.Operators
open MetaType

type LessonPresentationOrder =
    | Shuffled
    | [<JsonName "ascending_level_then_subject">] AscendingLevelThenSubject
    | [<JsonName "ascending_level_then_shuffled">] AscendingLevelThenShuffled

type ReviewPresentationOrder =
    | Shuffled
    | [<JsonName "lower_levels_first">] LowerLevelsFirst

type SubscriptionType =
    | Free
    | Recurring
    | Lifetime

type User =
    { id: string
      username: string
      level: uint
      profile_url: Uri
      started_at: DateTime
      current_vacation_started_at: DateTime option
      subscription:
          {| ``type``: SubscriptionType
             max_level_granted: uint
             period_ends_at: DateTime
             active: bool |}
      preferences:
          {| extra_study_autoplay_audio: bool
             lessons_autoplay_audio: bool
             lessons_batch_size: uint
             lessons_presentation_order: LessonPresentationOrder
             reviews_autoplay_audio: bool
             reviews_display_srs_indicator: bool
             reviews_presentation_order: ReviewPresentationOrder
             default_voice_actor_id: uint |} }

type LessonSummary =
    { available_at: DateTime
      subject_ids: uint[] }

type ReviewSummary =
    { available_at: DateTime
      subject_ids: uint[] }

type Summary =
    { lessons: LessonSummary[]
      next_reviews_at: DateTime option
      reviews: ReviewSummary[] }

type SubjectType =
    | Kanji
    | Radical
    | Vocabulary
    | [<JsonName "kana_vocabulary">] KanaVocabulary

type SrsStage =
    | Initiate = 0u
    | Apprentice1 = 1u
    | Apprentice2 = 2u
    | Apprentice3 = 3u
    | Apprentice4 = 4u
    | Guru1 = 5u
    | Guru2 = 6u
    | Master = 7u
    | Enlightened = 8u
    | Burned = 9u

type ReviewStartingSrsStage =
    | Apprentice1 = 1u
    | Apprentice2 = 2u
    | Apprentice3 = 3u
    | Apprentice4 = 4u
    | Guru1 = 5u
    | Guru2 = 6u
    | Master = 7u
    | Enlightened = 8u

type ReviewEndingSrsStage =
    | Apprentice1 = 1u
    | Apprentice2 = 2u
    | Apprentice3 = 3u
    | Apprentice4 = 4u
    | Guru1 = 5u
    | Guru2 = 6u
    | Master = 7u
    | Enlightened = 8u
    | Burned = 9u

type Assignment =
    { available_at: DateTime option
      burned_at: DateTime option
      created_at: DateTime
      passed_at: DateTime option
      resurrected_at: DateTime option
      srs_stage: SrsStage
      started_at: DateTime
      subject_id: uint
      subject_type: SubjectType
      unlocked_at: DateTime option }

type Review =
    { assignment_id: uint
      created_at: DateTime
      ending_srs_stage: ReviewEndingSrsStage
      incorrect_meaning_answers: uint
      incorrect_reading_answers: uint
      spaced_repetition_system_id: uint
      starting_srs_stage: ReviewStartingSrsStage
      subject_id: uint }

type ReviewStatistics =
    { created_at: DateTime
      hidden: bool
      meaning_correct: uint
      meaning_current_streak: uint
      meaning_incorrect: uint
      meaning_max_streak: uint
      percentage_correct: uint
      reading_correct: uint
      reading_current_streak: uint
      reading_incorrect: uint
      reading_max_streak: uint
      subject_id: uint
      subject_type: SubjectType }


type LevelProgression =
    { abandoned_at: DateTime option
      created_at: DateTime
      completed_at: DateTime option
      level: uint // 1..60
      passed_at: DateTime option
      started_at: DateTime option
      unlocked_at: DateTime option }

module API =

    let request (token: string) =
        http {
            config_transformHeader (fun header ->
                { header with
                    target.address = Some("https://api.wanikani.com" </> header.target.address.Value)
                    headers =
                        header.headers
                        |> Map.add "Cache-Control" "no-cache"
                        |> Map.add "Wanikani-Revision" "20170710"
                        |> Map.add "Authorization" $"Bearer {token}" })

        }

    let getObject<'data> token path =
        request token { GET path }
        |> Request.sendAsync
        |> Async.map (Response.expectHttpStatusCode HttpStatusCode.OK)
        |> Async.bind (function
            | Ok res -> Response.deserializeJsonAsync<Object<'data>> res
            | Error expectation -> failwithf "API returned an error response: %A" expectation)

    let getResources<'data> token since path =
        let rec paged acc =
            function
            | None -> async.Return acc
            | Some path' ->
                request token {
                    GET path'
                    IfModifiedSince(since |> Option.defaultValue DateTime.MinValue)
                }
                |> Request.sendAsync
                |> Async.map (Response.expectHttpStatusCode HttpStatusCode.OK)
                |> Async.bind (function
                    | Ok res ->
                        Response.deserializeJsonAsync<Collection<Resource<'data>>> res
                        |> Async.bind (fun collection ->
                            collection.pages.next_url
                            |> Option.map (_.PathAndQuery)
                            |> paged (Array.append acc collection.data))
                    | Error expectation -> async.Return acc)

        paged [||] (Some path)

open Database
open System.Data

let fetchAndSaveChanges<'a>
    (conn: IDbConnection)
    (request: DateTime option -> Async<Resource<'a>[]>)
    (save: IDbConnection -> string -> Resource<string>[] -> Async<unit>)
    =
    let mapResourceToDb (r: Resource<'a>) =
        { id = r.id
          object = r.object
          url = r.url
          data_updated_at = r.data_updated_at
          data = serialize r.data }

    let table = typeof<'a>.Name

    tryGetLatestUpdateTime<'a> conn table
    |> Async.bind request
    |> Async.bind (Array.map mapResourceToDb >> save conn table)

let getAllSaved<'table> (conn: IDbConnection) =
    let mapResourceFromDb (r: Resource<string>) =
        { id = r.id
          object = r.object
          url = r.url
          data_updated_at = r.data_updated_at
          data = deserialize<'table> r.data }

    getAll<Resource<string>> conn typeof<'table>.Name
    |> Async.map (Seq.toArray >> Array.Parallel.map mapResourceFromDb)

module User =

    let request token = API.getObject<User> token "/v2/user"

module Summary =

    let request token =
        API.getObject<Summary> token "/v2/summary"

module Assignment =

    let request token since =
        API.getResources<Assignment> token since "/v2/assignments"

    let refresh conn token =
        fetchAndSaveChanges conn (request token) insertOrReplaceMultiple

    let getCached = getAllSaved<Assignment>

    let refreshAndRead conn token =
        refresh conn token |> Async.bind (fun _ -> getCached conn)

    let delete = deleteAllRows<Assignment>

module Review =

    let request token since =
        API.getResources<Review> token since "/v2/reviews"

    let refresh conn token =
        fetchAndSaveChanges conn (request token) insertOrReplaceMultiple

    let getCached = getAllSaved<Review>

    let refreshAndRead conn token =
        refresh conn token |> Async.bind (fun _ -> getCached conn)

    let delete = deleteAllRows<Review>

module ReviewStatistics =

    let request token since =
        API.getResources<ReviewStatistics> token since "/v2/review_statistics"

    let refresh conn token =
        fetchAndSaveChanges conn (request token) insertOrReplaceMultiple

    let getCached = getAllSaved<ReviewStatistics>

    let refreshAndRead conn token =
        refresh conn token |> Async.bind (fun _ -> getCached conn)

    let delete = deleteAllRows<ReviewStatistics>

module LevelProgression =

    let request token since =
        API.getResources<LevelProgression> token since "/v2/level_progressions"

    let refresh conn token =
        fetchAndSaveChanges conn (request token) insertOrReplaceMultiple

    let getCached = getAllSaved<LevelProgression>

    let refreshAndRead conn token =
        refresh conn token |> Async.bind (fun _ -> getCached conn)

    let delete = deleteAllRows<LevelProgression>

module AccessToken =

    let tryGet conn =
        firstOrDefault<Table.AccessToken> conn |> Async.map (Option.map (_.token))

    let save conn token =
        insertOrReplace conn { Table.token = token }

    let delete conn = deleteAllRows<Table.AccessToken> conn

let deleteStoredData conn =
    [ Assignment.delete
      Review.delete
      ReviewStatistics.delete
      LevelProgression.delete
      AccessToken.delete ]
    |> List.map (fun op -> op conn)
    |> Async.Parallel
