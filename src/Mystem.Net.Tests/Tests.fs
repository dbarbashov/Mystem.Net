module Mystem.Net.Tests

open System.Net.Http
open NUnit.Framework
open Mystem.Net.Installer
open FSharp.Control.Tasks.V2.ContextInsensitive

[<Test>]
let ``Should create`` () =
    use mystem = new Mystem()
    
    Assert.Pass()
    
[<Test>]
let ``Should analyze`` () = task {
    use mystem = new Mystem()
    let mystem = mystem :> IMystem
    let! _ = mystem.Analyze("Мама мыла раму")
    Assert.Pass()
}

[<Test>]
let ``Should analyze in row`` () = task {
    use mystem = new Mystem()
    let mystem = mystem :> IMystem
    let! _ = mystem.Analyze("Мама мыла раму")
    let! _ = mystem.Analyze("Папа пил пиво")
    let! _ = mystem.Analyze("Брат ел яблоко")
    Assert.Pass()
}
    
[<Test>]
let ``Should install``() = task {
    use httpClient = new HttpClient()
    let installer = MystemInstaller(httpClient)
    
    do! installer.Install()
}