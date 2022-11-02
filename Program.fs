open System
open System.Diagnostics
open System.IO
open System.Threading.Tasks
open Legivel.Serialization

type Config = 
    { input: string
      videos: Video list }
and Video = 
    { name: string
      cuts: Cut list }
and Cut =
    { start: string
      ``end``: string }

let printTs (ts: TimeSpan) =
    ts.ToString("c")

let startFfmpegProcess label cmd =
    let p = new Process()
    p.StartInfo <- 
        ProcessStartInfo(
            "ffmpeg", "-hide_banner " + cmd, 
            UseShellExecute = false, 
            RedirectStandardOutput = true, 
            RedirectStandardError = true)
    p.OutputDataReceived.Add(fun args -> Console.WriteLine($"[{label}] {args.Data}"))
    p.ErrorDataReceived.Add(fun args -> Console.WriteLine($"[{label}] {args.Data}"))
    printfn "[RUN] [%s] %s" label cmd
    do p.Start() |> ignore
    do p.BeginOutputReadLine()
    do p.BeginErrorReadLine()
    task {
        do! p.WaitForExitAsync()
        return p.ExitCode
    }

let createCuts config video =
    let ext = config.input |> Path.GetExtension
    let cutNameCommands =
        video.cuts 
        |> List.indexed
        |> List.map (fun (i, cut) ->
            let startTs = TimeSpan.Parse(cut.start)
            let endTs = TimeSpan.Parse(cut.``end``) - startTs
            let cutFile = $"{video.name}_{i}{ext}"
            let command = $"-ss {printTs startTs} -i \"{config.input}\" -ss 00:00:00 -to {printTs endTs} -c copy -avoid_negative_ts 1 {cutFile}"
            cutFile, command)

    let cutFiles = cutNameCommands |> List.map fst
    
    task {
        let! resultCodes =
            cutNameCommands 
            |> List.map (fun (label, cmd) -> startFfmpegProcess label cmd)
            |> Task.WhenAll
        
        if resultCodes |> Array.exists ((<>) 0) then
            printfn "[ERROR] One of the cut processes for %s failed." video.name
            printfn "[ERROR] Cleaning up cut videos..."
            for file in cutFiles do File.Delete file
            return Result.Error ()
        else
            return Ok (video, cutFiles)
    }

let createConcat config video cutFiles =
    let ext = config.input |> Path.GetExtension
    let videoFile = $"{video.name}{ext}"
    let concatFile = $"{video.name}.txt"
    
    cutFiles 
    |> List.map (fun file -> $"file '{file}'") 
    |> String.concat Environment.NewLine
    |> fun text -> File.WriteAllText(concatFile, text)

    let command = $"-f concat -safe 0 -i {concatFile} -c copy {videoFile}"

    task {
        let! res = startFfmpegProcess video.name command
        for file in cutFiles do File.Delete file
        File.Delete concatFile
        if res <> 0 then
            printfn "[ERROR] Concat process for %s failed." video.name
            return Result.Error ()
        else
            return Ok ()
    }

let runEdit config =
    task {
        printfn "========== Creating cut videos =========="
        let! cutResults =
            config.videos 
            |> List.map (createCuts config)
            |> Task.WhenAll

        let cutSuccesses = 
            cutResults 
            |> Array.choose (function Ok cutFiles -> Some cutFiles | _ -> None)
        printfn "========== Finished cutting, %d succesful ==========" cutSuccesses.Length

        printfn "========== Concatenating videos =========="

        let! concatResults =
            cutSuccesses 
            |> Array.map (fun (video, cutFiles) -> createConcat config video cutFiles)
            |> Task.WhenAll

        let concatSuccessCount =
            concatResults
            |> Array.fold (fun cnt res -> match res with Ok _ -> cnt + 1 | _ -> cnt ) 0

        printfn "========== Finished concatenating, %d succesful ==========" concatSuccessCount
    }

[<EntryPoint>]
let main = function
| [| path |] when File.Exists path ->
    let configYaml = File.ReadAllText(path)
    match Deserialize<Config> configYaml with
    | [ Success { Data = config } ] ->
        let workingdir = Path.Combine (Path.GetDirectoryName path, Path.GetFileNameWithoutExtension path)
        Directory.CreateDirectory workingdir |> ignore
        Environment.CurrentDirectory <- workingdir

        let editTask = runEdit config
        editTask.Wait()

        0
    | failure ->
        printfn "Failed to parse config file: %A" failure
        1
| [| path |] ->
    printfn "File %s not found" path
    1 
| _ ->
    printfn "Usage: path-to-config.yaml"
    1
