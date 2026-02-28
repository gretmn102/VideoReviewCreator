#!/usr/bin/env -S dotnet fsi
#load @"../TimecodeMediaSplitter/src/ffmpegApi.fsx"
open FfmpegApi

type GenerateTextOptions = {
    Size: int * int
    DurationSeconds: int
    Text: string
}

module GenerateTextOptions =
    let escapeText =
        String.collect (function
            | '"' -> "\\\""
            | '\'' -> @"'\\\''" // это было очень сложно. Спасибо https://stackoverflow.com/a/74390947 за подсказку
            | '\\' -> @"\\\\"
            | '\n' -> @"NEWLINE NOT WORKING!" // todo: см https://stackoverflow.com/questions/8213865/ffmpeg-drawtext-over-multiple-lines
            | c -> string c
        )

    let toArgs (options: GenerateTextOptions) =
        let size =
            let w, h = options.Size
            sprintf "%dx%d" w h
        [
            "-y"
            "-f lavfi"
            $"-i color=c=black:s={size}:d={options.DurationSeconds}"
            $"-vf \"drawtext=text='{escapeText options.Text}':fontcolor=white:fontsize=48:x=(w-text_w)/2:y=(h-text_h)/2\""
            "-c:v libx264"
            "-pix_fmt yuv420p"
        ]

let generateText (options: GenerateTextOptions) (output: string) =
    let args =
        String.concat " " [
            yield! GenerateTextOptions.toArgs options
            output
        ]
    FfMpeg.startProc args

// do
//     "white_on_black.mp4"
//     |> generateText {
//         Size = 1368, 768
//         DurationSeconds = 5
//         Text = "It's so easy"
//     }
//     |> printfn "%A"
