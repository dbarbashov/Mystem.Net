module Mystem.Net.Tests

open System.IO
open System.Net.Http
open NUnit.Framework
open Mystem.Net.Installer

let testRunMystemPath = Path.GetTempFileName()

[<Test>]
let ``Should analyze`` () = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=testRunMystemPath))
    let mystem = mystem :> IMystem
    let! _ = mystem.Analyze("Мама мыла раму")
    Assert.Pass()
}

[<Test>]
let ``Should analyze in row`` () = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=testRunMystemPath))
    let mystem = mystem :> IMystem
    let! result = mystem.Analyze("Мама мыла раму")
    Assert.AreEqual(result.Length, 6)

    let! result = mystem.Analyze("Брат ел яблоко")
    Assert.AreEqual(result.Length, 6)
}

[<Test>]
let ``Should analyze file``() = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=testRunMystemPath))
    let mystem = mystem :> IMystem
    let! _ = mystem.AnalyzeFile("input.txt")
    Assert.Pass()
}

[<Test>]
let ``Should lemmatize`` () = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=testRunMystemPath))
    let mystem = mystem :> IMystem
    let! actual = mystem.Lemmatize("Мама мыла раму")
    Assert.AreEqual([| "мама"; " "; "мыть"; " "; "рама"; "\n" |], actual)
}

[<Test>]
let ``Should lemmatize in row`` () = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=testRunMystemPath))
    let mystem = mystem :> IMystem
    let! actual = mystem.Lemmatize("Мама мыла раму")
    Assert.AreEqual([| "мама"; " "; "мыть"; " "; "рама"; "\n" |], actual)
    
    let! actual = mystem.Lemmatize("Брат ел яблоко")
    Assert.AreEqual([| "брат"; " "; "есть"; " "; "яблоко"; "\n" |], actual)
}

[<Test>]
let ``Should lemmatize file``() = task {
    use mystem = new Mystem(MystemSettings(MystemBinaryPath=testRunMystemPath))
    let mystem = mystem :> IMystem
    let! actual = mystem.LemmatizeFile("input.txt")
    Assert.AreEqual([| "мама"; " "; "мыть"; " "; "рама"; "\n" |], actual)
}
    
[<Test>]
let ``Should install``() = task {
    use httpClient = new HttpClient()
    let installer = MystemInstaller(testRunMystemPath, httpClient)
    
    do! installer.Install()
}

[<Test>]
let ``If mystem path does not contain mystem - should throw an exception`` () = task {
    let fakePath = Path.GetTempFileName()
    try 
        use mystem = new Mystem(MystemSettings(MystemBinaryPath=fakePath))
        let bigContent = [|
            for i in [0..2048] do
                66uy
        |]
        File.WriteAllBytes(fakePath, bigContent)
        
        let mystem = mystem.Mystem
        try 
            let! _ = mystem.Lemmatize("Мама мыла раму")
            Assert.Fail()
        with
        | e ->
            Assert.Pass()
    finally
        File.Delete(fakePath)
}

[<OneTimeTearDown>]
let deleteMystemFile() =
    File.Delete(testRunMystemPath)