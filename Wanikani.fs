module Wanikani

open System
open System.Net
open System.Text.Json
open System.Text.Json.Serialization
open FsHttp
open FsHttp.Operators

type ObjectType =
    | Collection
    | Report
    | Assignment
    | Kanji
    | Radical
    | Reset
    | Review
    | User
    | Vocabulary
    | [<JsonName "kana_vocabulary">] KanaVocabulary
    | [<JsonName "level_progression">] LevelProgression
    | [<JsonName "review_statistic">] ReviewStatistic
    | [<JsonName "spaced_repetition_system">] SpacedRepetitionSystem
    | [<JsonName "study_material">] StudyMaterial
    | [<JsonName "voice_actor">] VoiceActor

(*
 * Metadata wrapper types for all content types
 *)
type Resource<'Data> =
    { id: uint
      object: ObjectType
      url: Uri
      data_updated_at: DateTime
      data: 'Data }

type Object<'Data> =
    { object: ObjectType
      url: Uri
      data_updated_at: DateTime
      data: 'Data }

type Collection<'Data> =
    { object: ObjectType
      url: Uri
      pages:
          {| previous_url: Uri option
             next_url: Uri option
             per_page: uint |}
      total_count: uint
      data_updated_at: DateTime option
      data: 'Data[] }

(*
 * Content types
 *)
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

type Assignment =
    { available_at: DateTime option
      burned_at: DateTime option
      created_at: DateTime
      passed_at: DateTime option
      resurrected_at: DateTime option
      srs_stage: uint
      started_at: DateTime
      subject_id: uint
      subject_type: SubjectType
      unlocked_at: DateTime option }

type Review =
    { assignment_id: uint
      created_at: DateTime
      ending_srs_stage: uint // 1..9
      incorrect_meaning_answers: uint
      incorrect_reading_answers: uint
      spaced_repetition_system_id: uint
      starting_srs_stage: uint // 1..8
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


let serialize data =
    JsonSerializer.Serialize(data, serializerOptions)

module API =

    let request token =
        let baseUrl = "https://api.wanikani.com"

        http {
            config_transformHeader (fun (header: Header) ->
                { header with
                    target.address = Some(baseUrl </> header.target.address.Value)
                    headers =
                        header.headers
                        |> Map.add "Cache-Control" "no-cache"
                        |> Map.add "Wanikani-Revision" "20170710"
                        |> Map.add "Authorization" $"Bearer {token}" })

        }

    let rec paged<'Data> token path since (pages: Collection<Resource<'Data>>[]) =
        request token {
            GET path
            IfModifiedSince(since |> Option.defaultValue DateTime.MinValue)
        }
        |> Request.sendAsync
        |> Async.map (Response.expectHttpStatusCode HttpStatusCode.OK)
        |> Async.bind (function
            | Ok res ->
                async {
                    let! data = Response.deserializeJsonAsync<Collection<Resource<'Data>>> res
                    let pages' = Array.append pages [| data |]

                    match data.pages.next_url with
                    | Some nextUrl -> return! paged<'Data> token nextUrl.PathAndQuery since pages'
                    | None -> return pages'
                }
            | Error expectation -> async.Return pages)

open Database
open System.Data

module User =

    let request (token: string) =
        API.request token { GET "/v2/user" }
        |> Request.sendAsync
        |> Async.bind Response.deserializeJsonAsync<Object<User>>

module Summary =

    let request (token: string) =
        API.request token { GET "/v2/summary" }
        |> Request.sendAsync
        |> Async.bind Response.deserializeJsonAsync<Object<Summary>>

module Assignment =

    let request (token: string) (since: DateTime option) =
        API.paged<Assignment> token "/v2/assignments" since [||]

    let internal mapToDb (stats: Collection<Resource<Assignment>>[]) : Table.Assignment[] =
        stats
        |> Array.collect _.data
        |> Array.map (fun stat ->
            { id = stat.id
              subject_id = stat.data.subject_id
              subject_type = serialize stat.data.subject_type
              created_at = stat.data.created_at
              updated_at = stat.data_updated_at
              data = serialize stat.data })

    let save (conn: IDbConnection) = mapToDb >> insertOrReplaceMultiple conn

    let tryGetLatestUpdateTime (conn: IDbConnection) =
        tryGetLatestUpdateTime<Table.Assignment> conn

module Review =

    let request (token: string) (since: DateTime option) =
        API.paged<Review> token "/v2/reviews" since [||]

    let internal mapToDb (stats: Collection<Resource<Review>>[]) : Table.Review[] =
        stats
        |> Array.collect _.data
        |> Array.map (fun stat ->
            { id = stat.id
              assignment_id = stat.data.assignment_id
              subject_id = stat.data.subject_id
              created_at = stat.data.created_at
              updated_at = stat.data_updated_at
              data = serialize stat.data })

    let save (conn: IDbConnection) = mapToDb >> insertOrReplaceMultiple conn

    let tryGetLatestUpdateTime (conn: IDbConnection) =
        tryGetLatestUpdateTime<Table.Review> conn

module ReviewStatistics =

    let request (token: string) (since: DateTime option) =
        API.paged<ReviewStatistics> token "/v2/review_statistics" since [||]


    let internal mapToDb (stats: Collection<Resource<ReviewStatistics>>[]) : Table.ReviewStatistics[] =
        stats
        |> Array.collect _.data
        |> Array.map (fun stat ->
            { id = stat.id
              subject_id = stat.data.subject_id
              subject_type = serialize stat.data.subject_type
              created_at = stat.data.created_at
              updated_at = stat.data_updated_at
              data = serialize stat.data })

    let save (conn: IDbConnection) = mapToDb >> insertOrReplaceMultiple conn

    let tryGetLatestUpdateTime (conn: IDbConnection) =
        tryGetLatestUpdateTime<Table.ReviewStatistics> conn

module LevelProgression =

    let request (token: string) (since: DateTime option) =
        API.request token {
            GET "/v2/level_progressions"
            IfModifiedSince(since |> Option.defaultValue DateTime.MinValue)
        }
        |> Request.sendAsync
        |> Async.map (Response.expectHttpStatusCode HttpStatusCode.OK)
        |> Async.bind (function
            | Ok res ->
                Response.deserializeJsonAsync<Collection<Resource<LevelProgression>>> res
                |> Async.map Some
            | Error expectation -> async.Return None)

    let internal mapToDb (stats: Collection<Resource<LevelProgression>>) : Table.LevelProgression[] =
        stats.data
        |> Array.map (fun stat ->
            { id = stat.id
              created_at = stat.data.created_at
              updated_at = stat.data_updated_at
              data = serialize stat.data })

    let save (conn: IDbConnection) = mapToDb >> insertOrReplaceMultiple conn

    let tryGetLatestUpdateTime (conn: IDbConnection) =
        tryGetLatestUpdateTime<Table.LevelProgression> conn

module AccessToken =

    let get (conn: IDbConnection) =
        firstOrDefault<Table.AccessToken> conn
        |> Async.bind (function
            | Some { token = token } -> async.Return token
            | None ->
                // Temporary solution to get the token if it is not in DB
                async {
                    Console.WriteLine "Input Wanikani Access Token:"
                    let token = Console.ReadLine()
                    do! insertOrReplace conn { Table.token = token }
                    return token
                })
