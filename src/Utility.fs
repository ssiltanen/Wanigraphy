[<AutoOpen>]
module Utility

open System.Text.Json
open System.Text.Json.Serialization

let serializerOptions =
    JsonFSharpOptions
        .Default()
        .WithAllowNullFields()
        .WithUnionTagCaseInsensitive()
        .WithUnionExternalTag()
        .WithUnionUnwrapFieldlessTags()
        .WithUnionUnwrapSingleFieldCases()
        .ToJsonSerializerOptions()

let serialize data =
    JsonSerializer.Serialize(data, serializerOptions)

let deserialize<'T> (data: string) =
    JsonSerializer.Deserialize<'T>(data, serializerOptions)

module Async =
    let bind f computation = async.Bind(computation, f)
    let map f = bind (f >> async.Return)

[<RequireQualifiedAccess>]
module Color =

    open Avalonia.Media

    let internal b (i: uint) = System.Convert.ToByte(i)

    let background = Color.FromRgb(b 44u, b 51u, b 51u)
    let primary = Color.FromRgb(b 46u, b 79u, b 79u)
    let secondary = Color.FromRgb(b 14u, b 131u, b 136u)
    let accent = Color.FromRgb(b 203u, b 228u, b 222u)

[<RequireQualifiedAccess>]
module Icons =

    open Avalonia.Svg.Skia

    let user =
        lazy new SvgImage(Source = SvgSource.Load("avares://Wanigraphy/Assets/Icons/turtle.svg", null))
