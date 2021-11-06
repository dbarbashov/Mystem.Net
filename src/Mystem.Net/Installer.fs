module Mystem.Net.Installer

open System
open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open FSharp.Control.Tasks.V2.ContextInsensitive
open SharpCompress.Common
open SharpCompress.Readers

[<Literal>]
let MystemPathEnvVariableName = "MYSTEM3_PATH" 

let mystemExe =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
        "mystem.exe"
    else
        "mystem"

let mystemDir, mystemBin =
    let mystemEnvPath = Environment.GetEnvironmentVariable(MystemPathEnvVariableName)
    if not <| String.IsNullOrWhiteSpace mystemEnvPath && File.Exists(mystemEnvPath) then 
        Path.GetDirectoryName(mystemEnvPath), mystemEnvPath
    else
        let dir = Path.GetFullPath("local/bin")
        // TODO: wtf (spans)?
        let fullPath = Path.Join(ReadOnlySpan(dir |> Seq.toArray), ReadOnlySpan(mystemExe |> Seq.toArray))
        dir, fullPath

type MystemInstaller(httpClient: HttpClient) =
    let tarballUrl =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            match RuntimeInformation.OSArchitecture with
            | Architecture.X64 -> "http://download.cdn.yandex.net/mystem/mystem-3.1-win-64bit.zip"
            | Architecture.X86 -> "http://download.cdn.yandex.net/mystem/mystem-3.0-win7-32bit.zip"
            | arch -> failwith $"Architecture windows-%A{arch} is not supported"
        elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then 
            match RuntimeInformation.OSArchitecture with
            | Architecture.X64 -> "http://download.cdn.yandex.net/mystem/mystem-3.1-linux-64bit.tar.gz"
            | Architecture.X86 -> "http://download.cdn.yandex.net/mystem/mystem-3.0-linux3.5-32bit.tar.gz"
            | arch -> failwith $"Architecture linux-%A{arch} is not supported"
        elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            "http://download.cdn.yandex.net/mystem/mystem-3.1-macosx.tar.gz"
        else
            failwith "Unsupported platform"
         
    let install() = task {
        printf $"Installing mystem to %s{mystemBin} from %s{tarballUrl}"
        Directory.CreateDirectory(mystemDir) |> ignore
        
        let tempPath = Path.GetTempFileName()
        use tempFileStream = new FileStream(tempPath, FileMode.Create)
            
        try 
            let! stream = httpClient.GetStreamAsync(tarballUrl)
            do! stream.CopyToAsync(tempFileStream)
            do! tempFileStream.FlushAsync()
            tempFileStream.Close()
            
            use stream = File.OpenRead(tempPath)
            use reader = ReaderFactory.Open(stream)
            while reader.MoveToNextEntry() do
                if not reader.Entry.IsDirectory && reader.Entry.Key.Contains("mystem") then
                    reader.WriteEntryToFile(mystemBin, ExtractionOptions(Overwrite=true))
        finally
            File.Delete(tempPath)
    }
         
    member x.Install() = task {
        if not <| File.Exists mystemBin then
            do! install()
    }