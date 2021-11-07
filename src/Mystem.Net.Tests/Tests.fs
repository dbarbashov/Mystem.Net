module Mystem.Net.Tests

open System.IO
open System.Net.Http
open NUnit.Framework
open Mystem.Net.Installer
open FSharp.Control.Tasks.V2.ContextInsensitive

[<Test>]
let ``Should analyze`` () = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=Path.GetTempFileName()))
    let mystem = mystem :> IMystem
    let! _ = mystem.Analyze("Мама мыла раму")
    Assert.Pass()
}

[<Test>]
let ``Should analyze in row`` () = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=Path.GetTempFileName()))
    let mystem = mystem :> IMystem
    let! result = mystem.Analyze("Мама мыла раму")
    Assert.AreEqual(result.Length, 6)

    let! result = mystem.Analyze("Брат ел яблоко")
    Assert.AreEqual(result.Length, 6)
}

[<Test>]
let ``Should lemmatize`` () = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=Path.GetTempFileName()))
    let mystem = mystem :> IMystem
    let! actual = mystem.Lemmatize("Мама мыла раму")
    Assert.AreEqual([| "мама"; " "; "мыть"; " "; "рама"; "\n" |], actual)
}

[<Test>]
let ``Should lemmatize in row`` () = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=Path.GetTempFileName()))
    let mystem = mystem :> IMystem
    let! actual = mystem.Lemmatize("Мама мыла раму")
    Assert.AreEqual([| "мама"; " "; "мыть"; " "; "рама"; "\n" |], actual)
    
    let! actual = mystem.Lemmatize("Брат ел яблоко")
    Assert.AreEqual([| "брат"; " "; "есть"; " "; "яблоко"; "\n" |], actual)
}
    
[<Test>]
let ``Should install``() = task {
    use httpClient = new HttpClient()
    let installer = MystemInstaller(httpClient)
    
    do! installer.Install(Path.GetTempFileName())
}