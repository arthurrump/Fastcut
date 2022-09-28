# Fastcut

A simple program to quickly create short videos from a long one. You could use this to extract clips from a livestream, for example. This can be done extremely fast without re-encoding the video by using FFmpeg's [stream copy](https://ffmpeg.org/ffmpeg.html#Stream-copy) and [concat demuxer](https://trac.ffmpeg.org/wiki/Concatenate#demuxer) features.

System requirements:
- .NET 6 or later
- A copy of [FFmpeg](https://ffmpeg.org/) in your PATH

All video formats supported by FFmpeg should work.

## Usage

Create a yaml file with the following format:

```yaml
input: path/to/input.mkv
videos:
  - name: "part1"
    cuts:
      - start: 00:08:20
        end: 00:11:30
      - start: 00:12:00
        end: 00:15:00
  - name: "part2"
    cuts:
      - start: 00:15:00
        end: 00:25:00
```

Then run `fastcut path/to/your/file.yaml`. This will create the files `part1.mkv` and `part2.mkv` in the `path/to/your/file/` directory.

## Install

```
dotnet pack
dotnet tool install --global --add-source ./tool fastcut
```
