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

## Documentation ##

- Supported [Codecs and Container Formats](audio/codecs.md)
- Supported [Providers](audio/providers.md)

## Contributing ##

Please read the [contributor](CONTRIBUTING.md) documentation for more
information.