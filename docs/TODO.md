# TODO #

- Add a way to track process of enqueueing audio files in EnqueueAsync
  - `IProgress`?
- Add further error handling for the YouTube source
  - Getting AudioLocation in YouTubeStreamParser may fail sporadically
- Add Soundcloud source
- Add Bandcamp source
- Support more container formats
  - mp4
- Support more codecs
  - mp4a
  - vorbis
- Support resampling audio where necessary (Using vectorisation where possible)

## Potential features ##

- Audio Filters
  - Volume (Using vectorisation where possible)
  - Equalisation
  - Channel swapping
