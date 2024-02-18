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
    subject_id INTEGER,
    subject_type TEXT,
    created_at DATETIME,
    updated_at DATETIME,
    data JSONB
);

CREATE TABLE IF NOT EXISTS Review (
    id INTEGER PRIMARY KEY,
    assignment_id INTEGER,
    subject_id INTEGER,
    created_at DATETIME,
    updated_at DATETIME,
    data JSONB
);

CREATE TABLE IF NOT EXISTS ReviewStatistics (
    id INTEGER PRIMARY KEY,
    subject_id INTEGER,
    subject_type TEXT,
    created_at DATETIME,
    updated_at DATETIME,
    data JSONB
);


CREATE TABLE IF NOT EXISTS LevelProgression (
    id INTEGER PRIMARY KEY,
    subject_id INTEGER,
    subject_type TEXT,
    created_at DATETIME,
    updated_at DATETIME,
    data JSONB
);
"""

module Table =

    type AccessToken = { token: string }

    type Assignment =
        { id: uint
          subject_id: uint
          subject_type: string
          created_at: DateTime
          updated_at: DateTime
          data: string }

    type Review =
        { id: uint
          assignment_id: uint
          subject_id: uint
          created_at: DateTime
          updated_at: DateTime
          data: string }

    type ReviewStatistics =
        { id: uint
          subject_id: uint
          subject_type: string
          created_at: DateTime
          updated_at: DateTime
          data: string }


    type LevelProgression =
        { id: uint
          created_at: DateTime
          updated_at: DateTime
          data: string }

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

let insertOrReplace (conn: IDbConnection) (insertValue: 'a) =
    insert {
        into table<'a>
        value insertValue
    }
    |> conn.InsertOrReplaceAsync
    |> Async.AwaitTask
    |> Async.Ignore

let insertOrReplaceMultiple (conn: IDbConnection) (insertValues: 'a[]) =
    if Array.isEmpty insertValues then
        async.Return()
    else
        insert {
            into table<'a>
            values (List.ofArray insertValues)
        }
        |> conn.InsertOrReplaceAsync
        |> Async.AwaitTask
        |> Async.Ignore

let firstOrDefault<'a> (conn: IDbConnection) =
    select {
        for row in table<'a> do
            skipTake 0 1
    }
    |> conn.SelectAsync<'a>
    |> Async.AwaitTask
    |> Async.map Seq.tryHead

let tryGetLatestUpdateTime<'table> (conn: IDbConnection) =
    select {
        for row in table<'table> do
            max "updated_at" "latest"
    }
    |> conn.SelectAsync<{| latest: DateTime option |}>
    |> Async.AwaitTask
    |> Async.map (Seq.tryHead >> (Option.bind _.latest))
