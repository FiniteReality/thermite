# Thermite #

A cross-platform C# voice framework for Discord.

Feel free to join my Discord server: https://discord.gg/Y4d9ZWJ

## Goals ##

- Low memory usage
  - Using `Span<T>` where possible to avoid allocations
  - Pooling resources where appropriate
- Low CPU usage
  - Doing as little work as possible to get an audio stream
- High quality audio
  - Using the highest quality input steam and codec settings reasonable for
    Discord
- Simplicity
  - API should be easy to use
  - Internal code should be easy to follow

## Features ##

- Pooled UDP clients to saturate I/O
- Fully-async API to reduce thread blocking
- Low allocation audio implementation

# Formats and Providers #

Not everything is supported yet. The following tables show current
implementation status of various providers and formats. If it's not listed,
assume it is unsupported.

|   Provider | Implemented? | Usable? |
|------------|--------------|---------|

| Codec | Supported Formats  |
|-------|--------------------|

# Contributing #

Contributors welcome!

## Dependencies ##
- libopus
- libsodium
- .NET Core >= 3.0 (recommended for best performance)

## Building ##

```
$ git clone https://github.com/FiniteReality/thermite.git
$ cd thermite
$ dotnet build
```

## TODO ##

- Implement more audio providers
  - Finish YouTube provider
    - Add caching to avoid HTTP requests
  - Add SoundCloud, Bandcamp, etc.
    - Use the APIs for this
- API review
  - Usage is fairly simple right now, but any assistance would be great
  - ThermiteStream implementation is strange to say the least, I want to look
    into simplifying it
- Perform benchmarks
  - Preliminary benchmarks place average CPU usage at <1% per client

# Usage #

These steps aren't 100% complete yet - I aim to make the API a bit easier

- Create an `AudioSource` passing a list of providers and a `HttpClient`
  - `YoutubeProvider` can be passed here, using the same `HttpClient`. It
    doesn't use the `YoutubeConfiguration` or `ICache` objects yet.
    *(NOTE: `YoutubeProvider` is not complete and will produce garbled audio!!)*
- Create a `PlayerManager` with the desired client user id
  - Implement `ITokenProvider` to provide voice tokens
  - Call `PlayerManager.RunAsync` with the token provider
- Call `PlayerManager.GetOrCreatePlayerAsync` to connect to a voice channel
  - If the client is already connected to this guild, it will not connect again
    *(NOTE: channel switching is not implemented yet)*
  - Add tracks to the queue using `player.Queue.AddAudioFile`
- Call `AudioSource.GetTracksAsync` to get the tracks given a url
  - Will throw if the URL is not supported
  - No range overload exists for `AddAudioFile`, you will need to loop manually