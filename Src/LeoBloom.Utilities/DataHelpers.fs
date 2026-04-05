namespace LeoBloom.Utilities

open System
open Npgsql

/// Shared data-access helper functions used by repository modules.
module DataHelpers =

    /// Add a nullable parameter to an NpgsqlCommand. When the option is None,
    /// DBNull.Value is used; when Some, the boxed value is passed directly.
    let optParam (name: string) (value: obj option) (cmd: NpgsqlCommand) =
        match value with
        | Some v -> cmd.Parameters.AddWithValue(name, v) |> ignore
        | None -> cmd.Parameters.AddWithValue(name, DBNull.Value) |> ignore
