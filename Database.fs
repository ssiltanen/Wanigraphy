module Database

open System
open System.Data
open Microsoft.Data.Sqlite
open Dapper.FSharp.SQLite

open Utility

let schema =
    """
CREATE TABLE IF NOT EXISTS AccessToken (
    token TEXT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS Statistics (
    id INTEGER PRIMARY KEY,
    subject_id INTEGER,
    subject_type TEXT,
    created_at DATETIME,
    updated_at DATETIME,
    data JSONB
);
"""

// Map nulls to options
OptionTypes.register ()

let connection () =
    async {
        let conn = new SqliteConnection("Data Source=turtles.db")
        do! conn.OpenAsync() |> Async.AwaitTask
        return conn
    }

let createIfNotExist (conn: SqliteConnection) =
    use cmd = conn.CreateCommand()
    cmd.CommandText <- schema
    cmd.ExecuteNonQueryAsync() |> Async.AwaitTask |> Async.Ignore

module DB =

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
                max "updated_at" "latest_update"
        }
        |> conn.SelectAsync<{| latest_update: DateTime |}>
        |> Async.AwaitTask
        |> Async.map (Seq.tryHead >> (Option.map _.latest_update))

module AccessToken =

    type AccessToken = { token: string }

    let tryGet = DB.firstOrDefault<AccessToken>

    let save (conn: IDbConnection) (token: string) =
        DB.insertOrReplace conn { token = token }

module Review =

    type Statistics =
        { id: uint
          subject_id: uint
          subject_type: string
          created_at: DateTime
          updated_at: DateTime
          data: string }

    let saveStats (conn: IDbConnection) (stats: Statistics[]) = DB.insertOrReplaceMultiple conn stats

    let tryGetLatestStatisticsUpdateTime (conn: IDbConnection) =
        DB.tryGetLatestUpdateTime<Statistics> conn
