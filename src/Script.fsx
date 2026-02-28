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

type MergeCrossFadeOptions = {
    /// Это то, за сколько секунд делается переход.
    Duration: float32
    /// Это то, с какой секунды начать переход.
    Offset: float32
}

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MergeCrossFadeOptions =
    let toArgs (options: MergeCrossFadeOptions) =
        let filter =
            String.concat ";" [
                "[0:v]format=yuv420p,setsar=1,fps=30[v0]"
                "[1:v]format=yuv420p,setsar=1,fps=30[v1]"
                $"[v0][v1]xfade=transition=fade:duration={options.Duration}:offset={options.Offset}[vid]"
                "[0:a][1:a]acrossfade=duration=2[aud]"
            ]
        [
            "-y"
            $"-filter_complex \"{filter}\""
            "-map \"[vid]\""
            "-map \"[aud]\""
            "-c:v libx264"
            "-crf 23"
            "-preset medium"
        ]


let mergeCrossFade (input1: string) (input2: string) (options: MergeCrossFadeOptions) (output: string) =
    let args =
        String.concat " " [
            $"-i {input1}"
            $"-i {input2}"
            yield! MergeCrossFadeOptions.toArgs options
            output
        ]
    FfMpeg.startProc args

let run (exitCode, stdout, stderr) =
    printfn "%s" stdout
    eprintfn "%s" stderr
    exit exitCode

// do
//     "white_on_black1.mp4"
//     |> generateText {
//         Size = 1368, 768
//         DurationSeconds = 5
//         Text = "Hello, мир!"
//     }
//     |> run
//     "white_on_black2.mp4"
//     |> generateText {
//         Size = 1368, 768
//         DurationSeconds = 5
//         Text = "It's so easy"
//     }
//     |> run

do
    "result.mp4"
    |> mergeCrossFade "white_on_black1.mp4" "/home/user/Videos/simplescreenrecorder-2026-02-28_04.37.05.mp4" {
    // |> mergeCrossFade "white_on_black1.mp4" "white_on_black2.mp4" {
        Duration = 2.5f
        Offset = 2.5f
    }
    |> run
