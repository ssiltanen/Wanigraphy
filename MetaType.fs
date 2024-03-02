module MetaType

open System
open System.Text.Json.Serialization

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
