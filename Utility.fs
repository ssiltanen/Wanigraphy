[<AutoOpen>]
module Utility

module Async =
    let bind f computation = async.Bind(computation, f)
    let map f = bind (f >> async.Return)

module Option =
    let iterAsync f =
        function
        | Some value -> async { do! f value }
        | None -> async.Return()

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
