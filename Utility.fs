[<AutoOpen>]
module Utility

module Async =
    let bind f computation = async.Bind(computation, f)
    let map f = bind (f >> async.Return)
