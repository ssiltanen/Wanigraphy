module Database

open System
open System.Data
open Microsoft.Data.Sqlite
open Dapper.FSharp.SQLite

let schema =
    """
CREATE TABLE IF NOT EXISTS AccessToken (
    token TEXT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS Assignment (
    id INTEGER PRIMARY KEY,
    object TEXT,
    url TEXT,
    data_updated_at DATETIME,
    data JSONB
);

CREATE TABLE IF NOT EXISTS Review (
    id INTEGER PRIMARY KEY,
    object TEXT,
    url TEXT,
    data_updated_at DATETIME,
    data JSONB
);

CREATE TABLE IF NOT EXISTS ReviewStatistics (
    id INTEGER PRIMARY KEY,
    object TEXT,
    url TEXT,
    data_updated_at DATETIME,
    data JSONB
);

CREATE TABLE IF NOT EXISTS LevelProgression (
    id INTEGER PRIMARY KEY,
    object TEXT,
    url TEXT,
    data_updated_at DATETIME,
    data JSONB
);
"""

module Table =
    type AccessToken = { token: string }

type ObjectTypeHandler() =
    inherit Dapper.SqlMapper.TypeHandler<MetaType.ObjectType>()

    override __.Parse(value) =
        string value |> deserialize<MetaType.ObjectType>

    override __.SetValue(p, value) =
        p.DbType <- Data.DbType.String
        p.Value <- serialize value

type UriHandler() =
    inherit Dapper.SqlMapper.TypeHandler<Uri>()

    override __.Parse(value) = Uri(string value)

    override __.SetValue(p, value) =
        p.DbType <- Data.DbType.String
        p.Value <- value.ToString()


// Add special type handlers
Dapper.SqlMapper.AddTypeHandler(typeof<MetaType.ObjectType>, ObjectTypeHandler())
Dapper.SqlMapper.AddTypeHandler(typeof<Uri>, UriHandler())

// Map database nulls with Options
Dapper.FSharp.SQLite.OptionTypes.register ()

let connection =
    async {
        let conn = new SqliteConnection("Data Source=turtles.db")
        do! conn.OpenAsync() |> Async.AwaitTask
        return conn
    }

let createIfNotExist (conn: SqliteConnection) =
    use cmd = conn.CreateCommand()
    cmd.CommandText <- schema
    cmd.ExecuteNonQueryAsync() |> Async.AwaitTask |> Async.Ignore

let insertOrReplace<'table> (conn: IDbConnection) (insertValue: 'table) =
    insert {
        into table<'table>
        value insertValue
    }
    |> conn.InsertOrReplaceAsync
    |> Async.AwaitTask
    |> Async.Ignore

let insertOrReplaceMultiple<'table> (conn: IDbConnection) (tableName: string) (insertValues: 'table[]) =
    if Array.isEmpty insertValues then
        async.Return()
    else
        insert {
            into (table'<'table> tableName)
            values (List.ofArray insertValues)
        }
        |> conn.InsertOrReplaceAsync
        |> Async.AwaitTask
        |> Async.Ignore

let firstOrDefault<'table> (conn: IDbConnection) =
    select {
        for row in table<'table> do
            skipTake 0 1
    }
    |> conn.SelectAsync<'table>
    |> Async.AwaitTask
    |> Async.map Seq.tryHead

let tryGetLatestUpdateTime<'table> (conn: IDbConnection) (tableName: string) =
    select {
        for row in (table'<'table> tableName) do
            max "data_updated_at" "latest"
    }
    |> conn.SelectAsync<{| latest: DateTime option |}>
    |> Async.AwaitTask
    |> Async.map (Seq.tryHead >> (Option.bind _.latest))

let getAll<'table> (conn: IDbConnection) (tableName: string) : Async<seq<'table>> =
    select {
        for row in (table'<'table> tableName) do
            selectAll
    }
    |> conn.SelectAsync<'table>
    |> Async.AwaitTask
