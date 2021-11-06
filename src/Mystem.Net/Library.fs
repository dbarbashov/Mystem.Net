namespace Mystem.Net

open System
open System.Collections.Generic
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open CliWrap
open CliWrap.EventStream
open FSharp.Control
open Mystem.Net.Installer
open FSharp.Control.Tasks.V2.ContextInsensitive
open Nerdbank.Streams

[<Struct>]
[<CLIMutable>]
type MystemAnalysisResult = {
    lex: string
    gr: string
}
    
[<Struct>]
[<CLIMutable>]
type MystemLemma = {
    text: string
    analysis: MystemAnalysisResult[]
}

type IMystem =
    /// Make morphology analysis for a text
    abstract Analyze: text: string -> Task<MystemLemma[]>
    
    /// Make morphology analysis for a UTF-8 text file
    abstract AnalyzeFile: filePath: string -> Task<MystemLemma[]>
    
    /// Make morphology analysis for a text and return list of lemmas
    abstract Lemmatize: text: string -> Task<string[]>
    
    /// Make morphology analysis for a UTF-8 text file and return list of lemmas
    abstract LemmatizeFile: filePath: string -> Task<string[]>

type MystemSettings() =
    /// Path to mystem binary
    member val MystemBinaryPath: string = null with get, set
    /// Print grammatical information (-i)
    member val PrintGrammarInfo = true with get, set
    /// Apply disambiguation (-d)
    member val ApplyDisambiguation = true with get, set
    /// Copy entire input to output (-c)
    member val CopyEntireInput = true with get, set
    /// Glue grammatical information for same lemmas in output (works only with PrintGrammarInfo=true) (-g)
    member val GlueGrammarInfo = true with get, set
    /// Print context-independent lemma weight (--weight)
    member val Weight = false with get, set
    /// Generate all possible hypotheses for non-dictionary words (--generate-all)
    member val GenerateAll = false with get, set
    /// Print only dictionary words (-w)
    member val NoBastards = false with get, set
    /// Print end of sentence mark (works only with CopyEntireInput=true) (-s)
    member val EndOfSentence = false with get, set
    /// Path to a custom dictionary to use for analysis (--fixlist)
    member val FixList: string = null with get, set
    /// English names of grammemes (--eng-gr)
    member val UseEnglishNames = false with get, set
    
type internal MystemMailboxMessage =
    | CliEvent of Event: CommandEvent
    | Send of Text: string * ReplyChannel: AsyncReplyChannel<string>
    
type MystemProcess(args: string[]) =
    let stdin = new SimplexStream()
    let sr = new StreamWriter(stdin)
    let cts = new CancellationTokenSource()
    let mutable mystemCommand = null
    
    let mystemTimeout = TimeSpan.FromSeconds(10.0)
    
    let mailbox = MailboxProcessor.Start(fun (mb: MailboxProcessor<MystemMailboxMessage>) -> async {
        while true do 
            match! mb.Receive() with
            | CliEvent event ->
                match event with 
                | :? StartedCommandEvent as started ->
                    printf $"Started process with id {started.ProcessId}"
                | :? StandardOutputCommandEvent as stdout ->
                    printf $"stdout: {stdout.Text}"
                | :? StandardErrorCommandEvent as stderr ->
                    printf $"stderr: {stderr.Text}"
                | :? ExitedCommandEvent as exited ->
                    printf $"process exited: {exited.ExitCode}"
                | _ ->
                    failwith $"Unknown event: {event.GetType()}"
            | Send (text, replyChannel) ->
                do! sr.WriteLineAsync(text) |> Async.AwaitTask
                do! sr.FlushAsync() |> Async.AwaitTask
                let! response =
                    mb.Scan(fun message -> 
                        match message with
                        | CliEvent event ->
                            match event with
                            | :? StandardOutputCommandEvent as str ->
                                Some (async.Return str.Text)
                            | _ -> None
                        | _ -> None)
                replyChannel.Reply(response)
    })
        
    let startMystem() = async {
        if mystemCommand = null then
            if not <| RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                do! Cli.Wrap("chmod")
                        .WithArguments([| "+x"; mystemBin |])
                        .ExecuteAsync().Task
                    |> Async.AwaitTask
                    |> Async.Ignore
            
            let command =
                Cli.Wrap(mystemBin)
                    .WithArguments(args)
                    .WithValidation(CommandResultValidation.ZeroExitCode)
                    .WithWorkingDirectory(Path.GetTempPath())
                    .WithStandardInputPipe(PipeSource.FromStream stdin)
                    
            let eventStream = command.ListenAsync()
            let awaitable = 
                eventStream
                |> AsyncSeq.ofAsyncEnum
                |> AsyncSeq.map (fun event -> mailbox.Post(CliEvent event))
                |> AsyncSeq.toArrayAsync
                |> Async.Ignore
            
            Async.Start(awaitable, cancellationToken=cts.Token)
            mystemCommand <- command
    }
            
    member x.SendStringAndWaitForResponse(str: string) = task {
        do! startMystem()
        return! mailbox.PostAndAsyncReply(fun chan -> Send (str, chan)) |> Async.StartAsTask
    }
    
    interface IDisposable with
        member x.Dispose() =
            stdin.Dispose()
            sr.Dispose()
            cts.Dispose()

type Mystem(settings: MystemSettings) as x =
    let mystemArgs =
        [|
            "--format"
            "json"
            
            if settings.PrintGrammarInfo then "-i"
            if settings.GlueGrammarInfo then "-g"
            
            if settings.ApplyDisambiguation then "-d"
            
            if settings.CopyEntireInput then "-c"
            if settings.NoBastards then "-w"
            if settings.EndOfSentence then "-s"
            
            if settings.Weight then "--weight"
            
            if settings.GenerateAll then "--generate-all"
            
            if not <| String.IsNullOrWhiteSpace settings.FixList then
                "--fixlist"
                settings.FixList
                
            if settings.UseEnglishNames then "--eng-gr"
        |]
    
    let httpClient = new HttpClient()
    let installer = MystemInstaller(httpClient)
    let mystemProcess = new MystemProcess(mystemArgs)
    let jsonOptions =
        JsonSerializerOptions(IgnoreNullValues=true)
        
    let ensureMystem() = task {
        if not <| File.Exists(mystemBin) then 
            do! installer.Install()
    }
            
    let analyze (text: string) = task {
        do! ensureMystem()
        
        let! response = mystemProcess.SendStringAndWaitForResponse(text)
        return JsonSerializer.Deserialize<MystemLemma[]>(response, jsonOptions)
    }
        
    new() = new Mystem(MystemSettings())
    
    interface IDisposable with
        member x.Dispose() =
            httpClient.Dispose()
            (mystemProcess :> IDisposable).Dispose()
            
    interface IMystem with
        member this.Analyze(text) = analyze text
        member this.AnalyzeFile(filePath) = failwith "todo"
        member this.Lemmatize(text) = failwith "todo"
        member this.LemmatizeFile(filePath) = failwith "todo" 