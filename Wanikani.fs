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

    let getObject<'data> token path =
        request token { GET path }
        |> Request.sendAsync
        |> Async.bind Response.deserializeJsonAsync<Object<'data>>
        |> Async.map _.data

    let getSinglePagedResource<'Data> token since path =
        request token {
            GET path
            IfModifiedSince(since |> Option.defaultValue DateTime.MinValue)
        }
        |> Request.sendAsync
        |> Async.map (Response.expectHttpStatusCode HttpStatusCode.OK)
        |> Async.bind (function
            | Ok res ->
                Response.deserializeJsonAsync<Collection<Resource<'Data>>> res
                |> Async.map _.data
            | Error expectation -> async.Return [||])

    let rec getPagedResource<'Data> token since path (pages: Resource<'Data>[]) =
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
                    let pages' = Array.append pages data.data

                    match data.pages.next_url with
                    | Some nextUrl -> return! getPagedResource<'Data> token since nextUrl.PathAndQuery pages'
                    | None -> return pages'
                }
            | Error expectation -> async.Return pages)

open Database
open System.Data

let fetchSaveDelta<'api, 'table>
    (conn: IDbConnection)
    (request: DateTime option -> Async<'api[]>)
    (save: IDbConnection -> 'api[] -> Async<unit>)
    =
    tryGetLatestUpdateTime<'table> conn
    |> Async.bind request
    |> Async.bind (save conn)

module User =

    let request token = API.getObject<User> token "/v2/user"

module Summary =

    let request token =
        API.getObject<Summary> token "/v2/summary"

module Assignment =

    let request token since =
        API.getPagedResource<Assignment> token since "/v2/assignments" [||]

    let mapToDb (assignment: Resource<Assignment>) : Table.Assignment =
        { id = assignment.id
          subject_id = assignment.data.subject_id
          subject_type = serialize assignment.data.subject_type
          created_at = assignment.data.created_at
          updated_at = assignment.data_updated_at
          data = serialize assignment.data }

    let save conn =
        Array.map mapToDb >> insertOrReplaceMultiple conn

    let refresh conn token =
        fetchSaveDelta<Resource<Assignment>, Table.Assignment> conn (request token) save

module Review =

    let request token since =
        API.getPagedResource<Review> token since "/v2/reviews" [||]

    let mapToDb (review: Resource<Review>) : Table.Review =
        { id = review.id
          assignment_id = review.data.assignment_id
          subject_id = review.data.subject_id
          created_at = review.data.created_at
          updated_at = review.data_updated_at
          data = serialize review.data }

    let save conn =
        Array.map mapToDb >> insertOrReplaceMultiple conn

    let refresh conn token =
        fetchSaveDelta<Resource<Review>, Table.Review> conn (request token) save

module ReviewStatistics =

    let request token since =
        API.getPagedResource<ReviewStatistics> token since "/v2/review_statistics" [||]

    let mapToDb (stats: Resource<ReviewStatistics>) : Table.ReviewStatistics =
        { id = stats.id
          subject_id = stats.data.subject_id
          subject_type = serialize stats.data.subject_type
          created_at = stats.data.created_at
          updated_at = stats.data_updated_at
          data = serialize stats.data }

    let save conn =
        Array.map mapToDb >> insertOrReplaceMultiple conn

    let refresh conn token =
        fetchSaveDelta<Resource<ReviewStatistics>, Table.ReviewStatistics> conn (request token) save

module LevelProgression =

    let request token since =
        API.getSinglePagedResource<LevelProgression> token since "/v2/level_progressions"

    let mapToDb (progression: Resource<LevelProgression>) : Table.LevelProgression =
        { id = progression.id
          created_at = progression.data.created_at
          updated_at = progression.data_updated_at
          data = serialize progression.data }

    let save conn =
        Array.map mapToDb >> insertOrReplaceMultiple conn

    let refresh conn token =
        fetchSaveDelta<Resource<LevelProgression>, Table.LevelProgression> conn (request token) save

module AccessToken =

    let get conn =
        firstOrDefault<Table.AccessToken> conn
        |> Async.bind (
            Option.map (_.token >> async.Return)
            >> Option.defaultWith (fun _ ->
                // Temporary solution to get the token if it is not in DB
                async {
                    Console.WriteLine "Input Wanikani Access Token:"
                    let token = Console.ReadLine()
                    do! insertOrReplace conn { Table.token = token }
                    return token
                })
        )
