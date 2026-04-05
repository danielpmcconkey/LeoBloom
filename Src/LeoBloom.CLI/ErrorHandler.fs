module LeoBloom.CLI.ErrorHandler

open System
open Argu

/// Custom Argu IExiter that writes to stderr and uses our exit codes.
type LeoBloomExiter() =
    interface IExiter with
        member _.Name = "LeoBloom CLI"
        member _.Exit(msg, errorCode) =
            match errorCode with
            | ErrorCode.HelpText ->
                Console.Out.WriteLine(msg)
                exit ExitCodes.success
            | _ ->
                Console.Error.WriteLine(msg)
                exit ExitCodes.systemError
