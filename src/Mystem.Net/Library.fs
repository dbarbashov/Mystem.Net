namespace Mystem.Net

open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Threading.Tasks
open CliWrap
open CliWrap.EventStream
open FSharp.Control
open Mystem.Net.Installer
open Nerdbank.Streams

[<Struct>]
[<CLIMutable>]
type MystemAnalysisResult = {
    [<JsonPropertyName("lex")>]
    /// Infinitive form of a word
    Lexeme: string
    [<JsonPropertyName("gr")>]
    /// Grammeme of a word (e.g. "S,жен,неод=(вин,мн|род,ед|им,мн)")
    Grammeme: string
    [<JsonPropertyName("qual")>]
    /// Word qualifier (e.g. "bastard")
    Qualifier: string
}

[<Struct>]
[<CLIMutable>]
type MystemLemma = {
    [<JsonPropertyName("text")>]
    /// Original text from input
    Text: string
    [<JsonPropertyName("analysis")>]
    /// Results of analysis
    AnalysisResults: MystemAnalysisResult[]
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
    
type internal MystemProcess(installer: MystemInstaller, args: string[]) =
    let stdin = new SimplexStream()
    let streamWriter = new StreamWriter(stdin)
    let cts = new CancellationTokenSource()
    let mutable mystemCommand = null
    
    let mailbox = MailboxProcessor.Start(fun (mb: MailboxProcessor<MystemMailboxMessage>) -> async {
        try 
            while true do
                match! mb.Receive() with
                | Send (text, replyChannel) ->
                    do! streamWriter.WriteLineAsync(text) |> Async.AwaitTask
                    do! streamWriter.FlushAsync() |> Async.AwaitTask
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
                | CliEvent event ->
                    match event with
                    | :? ExitedCommandEvent as exited ->
                        printfn "Process has exited: %A" exited.ExitCode
                        cts.Cancel()
                    | _ -> ()
        with e ->
            printfn "Exception has occured %A" e
            cts.Cancel()
    }, cancellationToken=cts.Token)

    let startMystem() = async {
        if mystemCommand = null then
            if not <| RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                do! Cli.Wrap("chmod")
                        .WithArguments([| "+x"; installer.InstalledPath |])
                        .ExecuteAsync().Task
                    |> Async.AwaitTask
                    |> Async.Ignore
            
            let command =
                if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                    let concattedArgs = String.concat " " args
                    Cli.Wrap("cmd")
                        .WithArguments($"/c chcp 65001 > null && {installer.InstalledPath} {concattedArgs}")
                else 
                    Cli.Wrap(installer.InstalledPath)
                        .WithArguments(args)
                        
            let command =
                command
                    .WithValidation(CommandResultValidation.ZeroExitCode)
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
        let computation = mailbox.PostAndAsyncReply(fun chan -> Send (str, chan)) 
        return! Async.StartAsTask(computation, cancellationToken=cts.Token)
    }
    
    static member ParseFile(mystemInstalledPath, args, filePath: string) = task {
        let stdout = StringBuilder()
        let stderr = StringBuilder()
        
        try 
            do!
                Cli.Wrap(mystemInstalledPath)
                    .WithArguments(Array.append args [| filePath |])
                    .WithValidation(CommandResultValidation.ZeroExitCode)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder stdout)
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder stderr)
                    .ExecuteAsync()
                    .Task :> Task
        finally
            printfn $"Mystem error: {stderr.ToString()}"
        return stdout.ToString()
    }
    
    interface IDisposable with
        member x.Dispose() =
            stdin.Dispose()
            streamWriter.Dispose()
            cts.Cancel()
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
    let installer = MystemInstaller(settings.MystemBinaryPath, httpClient)
    let mystemProcess = new MystemProcess(installer, mystemArgs)
    let jsonOptions =
        JsonSerializerOptions(IgnoreNullValues=true)
        
    let ensureMystem() = task {
        do! installer.Install()
    }
    
    let analyze (text: string) = task {
        do! ensureMystem()
        
        let! response = mystemProcess.SendStringAndWaitForResponse(text)
        return JsonSerializer.Deserialize<MystemLemma[]>(response, jsonOptions)
    }
    
    let analyzeFile (filePath: string) = task {
        do! ensureMystem()
        
        let! response = MystemProcess.ParseFile(installer.InstalledPath, mystemArgs, filePath)
        return JsonSerializer.Deserialize<MystemLemma[]>(response, jsonOptions)
    }
    
    let analysisResultsToLemmas (lemmas: MystemLemma[]) =
        lemmas
        |> Array.map (fun lemma ->
            if lemma.AnalysisResults = null || lemma.AnalysisResults.Length = 0 then
                lemma.Text
            else
                lemma.AnalysisResults.[0].Lexeme
        )
    
    let lemmatizeFile (filePath: string) = task {
        let! analysisResult = analyzeFile filePath
        return analysisResultsToLemmas analysisResult
    }
    
    let lemmatize (text: string) = task {
        let! analysisResult = analyze text
        return analysisResultsToLemmas analysisResult
    }
    
    member val Mystem: IMystem = x :> IMystem
    
    member x.EnsureMystem() = ensureMystem()
    
    new() = new Mystem(MystemSettings())
    
    interface IDisposable with
        member x.Dispose() =
            httpClient.Dispose()
            (mystemProcess :> IDisposable).Dispose()
            
    interface IMystem with
        member this.Analyze(text) = analyze text
        member this.AnalyzeFile(filePath) = analyzeFile filePath
        member this.Lemmatize(text) = lemmatize text
        member this.LemmatizeFile(filePath) = lemmatizeFile filePath