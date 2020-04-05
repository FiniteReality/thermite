# Thermite #

A cross-platform C# voice framework for Discord.

Feel free to join my Discord server: https://discord.gg/Y4d9ZWJ

## Goals ##

- Low memory usage
  - Using `Span<T>` where possible to avoid allocations
  - Pooling resources where appropriate
  - Using Pipelines to simplify implementation
- Low CPU usage
  - Doing as little work as possible to get an audio stream to Discord
- High quality audio
  - Using the highest quality input steam and codec settings reasonable for
    Discord
- Simplicity
  - API should be easy to use
  - Internal code should be easy to follow

## Features ##

- Pooled UDP clients to saturate I/O
- Fully async internal API to prevent thread blocking
- ~~Low~~ Zero(?) allocation audio implementation
- Vectorised code-paths for transcoding

## Platform Support ##

Thermite will currently **ONLY** run on processors supporting at least one of
these instruction sets:
- SSE2
- SSSE3
- AVX2

This means **NO** ARM support - your system **MUST** run an Intel or AMD
processor purchased after 2000.

## Documentation ##

- Supported [Codecs and Container Formats](audio/codecs.md)
- Supported [Sources](audio/sources.md)

## Contributing ##

Please read the [contributor](CONTRIBUTING.md) documentation for more
information.
