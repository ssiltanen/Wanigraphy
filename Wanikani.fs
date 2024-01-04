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

let internal wanikaniAPI token =
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

let rec internal fetchPages<'Data> token path since (pages: Collection<Resource<'Data>>[]) =
    wanikaniAPI token {
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
                | Some nextUrl -> return! fetchPages<'Data> token nextUrl.PathAndQuery since pages'
                | None -> return pages'
            }
        | Error expectation ->
            printfn "%A" expectation
            async.Return pages)


module User =

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

    let get (token: string) =
        wanikaniAPI token { GET "/v2/user" }
        |> Request.sendAsync
        |> Async.bind Response.deserializeJsonAsync<Object<User>>

module Summary =

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

    let get (token: string) =
        wanikaniAPI token { GET "/v2/summary" }
        |> Request.sendAsync
        |> Async.bind Response.deserializeJsonAsync<Object<Summary>>

module Review =

    type SubjectType =
        | Kanji
        | Radical
        | Vocabulary
        | [<JsonName "kana_vocabulary">] KanaVocabulary

    type Statistics =
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

    let getStatistics (token: string) (since: DateTime option) (etag: string option) =
        wanikaniAPI token {
            GET "/v2/review_statistics"
            IfModifiedSince(since |> Option.defaultValue DateTime.MinValue)
            IfNoneMatch(etag |> Option.defaultValue "")
        }
        |> Request.sendAsync
        |> Async.bind Response.deserializeJsonAsync<Collection<Resource<Statistics>>>

    let getAllStatistics (token: string) (since: DateTime option) =
        fetchPages<Statistics> token "/v2/review_statistics" since [||]
