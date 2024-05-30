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

    let internal byte (i: int) = System.Convert.ToByte(i)
    let rgb r g b = Color.FromRgb(byte r, byte g, byte b)

    let background = rgb 44 51 51
    let primary = rgb 46 79 79
    let secondary = rgb 14 131 136
    let accent = rgb 203 228 222

    let apprentice = rgb 221 0 147
    let guru = rgb 136 45 158
    let master = rgb 41 77 219
    let enlightened = rgb 0 147 221
    let burned = rgb 67 67 67

[<RequireQualifiedAccess>]
module Icons =

    open Avalonia.Svg.Skia

    let user =
        lazy new SvgImage(Source = SvgSource.Load("avares://Wanigraphy/Assets/Icons/turtle.svg", null))
