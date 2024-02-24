[<AutoOpen>]
module Utility

module Async =
    let bind f computation = async.Bind(computation, f)
    let map f = bind (f >> async.Return)

module Result =
    let mapAsync f =
        function
        | Ok value -> async { return! f value }
        | Error e -> async.Return e

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
