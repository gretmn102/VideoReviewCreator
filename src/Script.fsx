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
            "-f lavfi"
            "-i anullsrc=channel_layout=stereo:sample_rate=44100"
            $"-vf \"settb=1/15360,fps=30,drawtext=text='{escapeText options.Text}':fontcolor=white:fontsize=48:x=(w-text_w)/2:y=(h-text_h)/2\""
            "-c:v libx264"
            "-pix_fmt yuv420p"
            "-c:a aac -b:a 128k"
            "-shortest"
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
                $"[0:v][1:v]xfade=transition=fade:duration={options.Duration}:offset={options.Offset}[vid]"
                "[0:a][1:a]acrossfade=duration=2[aud]"
            ]
        [
            "-y"
            $"-filter_complex \"{filter}\""
            "-map \"[vid]\""
            "-map \"[aud]\""
        ]

let mergeCrossFade (options: MergeCrossFadeOptions) (output: string) (input1: string, input2: string) =
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

let toResult (exitCode, stdout, stderr) =
    match exitCode with
    | 0 -> Ok ()
    | _ -> Error (stdout, stderr)

let exit =
    function
    | Ok () -> 0
    | Error (stdout, stderr) ->
        printfn "%s" stdout
        eprintfn "%s" stderr
        1
    >> exit

let createVideoWithHeader text outputVideoPath inputVideoPath =
    let headerVideoPath = "headerVideoPath.mp4"
    headerVideoPath
    |> generateText {
        Size = 1368, 768
        DurationSeconds = 5
        Text = text
    }
    |> toResult
    |> Result.bind (fun () ->
        (headerVideoPath, inputVideoPath)
        |> mergeCrossFade
            {
                Duration = 2.5f
                Offset = 2.5f
            }
            outputVideoPath
        |> toResult
    )
    |> exit

"/home/user/Videos/simplescreenrecorder-2026-02-28_04.37.05.mp4"
|> createVideoWithHeader
    "Философ VS айтишник"
    "result.mp4"
