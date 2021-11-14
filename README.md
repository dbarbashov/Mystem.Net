# Mystem.Net

This is a wrapper for great CLI tool [Yandex Mystem](https://yandex.ru/dev/mystem/). 
This library is hugely inspired by [pymystem3](https://github.com/nlpub/pymystem3)

The wrapper has MIT license, although one should consider that mystem itself is not open source and has [different license](https://yandex.ru/legal/mystem/).

# Usage

### Analyze
```fsharp
open Mystem.Net
// Implements IDisposable
let mystem = new Mystem()

let lemmas = mystem.Mystem.Analyze("мама мыла раму").Result

printfn "%A" lemmas
> [|{ Text = "Мама"
    AnalysisResults = [|{ Lexeme = "мама"
                          Grammeme = "S,жен,од=им,ед"
                          Qualifier = null }|] }; { Text = " "
                                                    AnalysisResults = null };
  { Text = "мыла"
    AnalysisResults = [|{ Lexeme = "мыть"
                          Grammeme = "V,несов,пе=прош,ед,изъяв,жен"
                          Qualifier = null }|] }; { Text = " "
                                                    AnalysisResults = null };
  { Text = "раму"
    AnalysisResults = [|{ Lexeme = "рама"
                          Grammeme = "S,жен,неод=вин,ед"
                          Qualifier = null }|] }; { Text = "
"
                                                    AnalysisResults = null }|]
```


### Lemmatize
```fsharp
open Mystem.Net
let mystem = new Mystem()

let lemmas = mystem.Mystem.Lemmatize("мама мыла раму").Result

printfn "%A" lemmas
> [|"мама"; " "; "мыть"; " "; "рама"; "\n"|]
```
