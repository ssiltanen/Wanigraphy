module Wanikani

open System
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

type Lesson =
    { available_at: DateTime
      subject_ids: uint[] }

type Review =
    { available_at: DateTime
      subject_ids: uint[] }

type Summary =
    { lessons: Lesson[]
      next_reviews_at: DateTime option
      reviews: Review[] }

type SubjectType =
    | Kanji
    | Radical
    | Vocabulary
    | [<JsonName "kana_vocabulary">] KanaVocabulary

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
        |> Async.map (Response.expectHttpStatusCode System.Net.HttpStatusCode.OK)
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
open System.Text.Json

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

module ReviewStatistics =

    let request (token: string) (since: DateTime option) =
        API.paged<ReviewStatistics> token "/v2/review_statistics" since [||]


    let internal mapToDb (stats: Collection<Resource<ReviewStatistics>>[]) : Statistics[] =
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

    let save (conn: IDbConnection) = mapToDb >> insertOrReplaceMultiple conn

    let getLatestUpdateTime (conn: IDbConnection) = tryGetLatestUpdateTime<Statistics> conn

module AccessToken =

    let get (conn: IDbConnection) =
        firstOrDefault<AccessToken> conn
        |> Async.bind (function
            | Some { token = token } -> async.Return token
            | None ->
                async {
                    Console.WriteLine "Input Wanikani Access Token:"
                    let token = Console.ReadLine()
                    do! insertOrReplace conn { token = token }
                    return token
                })
