module Mystem.Net

open System
open System.Collections.Generic
open System.Threading.Tasks

type IMystem =
    /// Make morphology analysis for a text
    abstract Analyze: text: string -> Task<Dictionary<string, obj>>
    
    /// Make morphology analysis for a UTF-8 text file
    abstract AnalyzeFile: text: string -> Task<Dictionary<string, obj>>
    
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
    
type Mystem(settings: MystemSettings) =
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
        
    new() = Mystem(MystemSettings())
    
    interface IDisposable with
        member x.Dispose() = ()