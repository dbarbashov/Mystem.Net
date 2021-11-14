module internal Mystem.Net.Installer

open System.Runtime.CompilerServices
open CliWrap

[<assembly: InternalsVisibleTo("Mystem.Net.Tests")>]
do()

open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open FSharp.Control.Tasks.V2.ContextInsensitive
open SharpCompress.Common
open SharpCompress.Readers

type MystemInstaller(mystemCustomPath, httpClient: HttpClient) =
    
    [<Literal>]
    let MystemPathEnvVariableName = "MYSTEM3_PATH"
    
    let mystemExe =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            "mystem.exe"
        else
            "mystem"
    
    let mystemBin =
        if String.IsNullOrWhiteSpace mystemCustomPath then 
            let mystemEnvPath = Environment.GetEnvironmentVariable(MystemPathEnvVariableName)
            if String.IsNullOrWhiteSpace mystemEnvPath then
                let dir = Path.GetFullPath("local/bin")
                // TODO: wtf (spans)?
                let fullPath = Path.Join(ReadOnlySpan(dir |> Seq.toArray), ReadOnlySpan(mystemExe |> Seq.toArray))
                fullPath
            else 
                mystemEnvPath
        else
            mystemCustomPath
    
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

    let mutable isInstalledCache = false 
    
    let mystemIsInstalled() = task {
        if isInstalledCache then
            return true
        else 
            if File.Exists mystemBin then
                let fileInfo = FileInfo(mystemBin)
                if fileInfo.Length > 1024L then
                    let! result =
                         Cli.Wrap(mystemBin)
                             .WithArguments("--help")
                             .WithValidation(CommandResultValidation.None)
                             .ExecuteAsync()
                    if result.ExitCode = 0 then
                        isInstalledCache <- true
                        return true
                    else
                        return false 
                else
                    return false
            else
                return false
    }
    
    let install() = task {
        printfn $"Installing mystem to %s{mystemBin} from %s{tarballUrl}"
        
        let mystemDir = Path.GetDirectoryName(mystemBin)
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
    
    member val InstalledPath = mystemBin with get
    
    member x.Install() = task {
        let! mystemIsInstalled = mystemIsInstalled()
        if not mystemIsInstalled then
            do! install()
    }