
open Mystem.Net

let mystem = new Mystem()
let lemmas = mystem.Mystem.Lemmatize("мама мыла раму").Result
printfn "%A" lemmas