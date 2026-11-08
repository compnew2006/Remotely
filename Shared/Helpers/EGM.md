# Helpers

## Overview

Reusable process-local helpers for throttling, chunked DTO transport, path-name sanitization, timing, and disposal.

## Interface

- DtoChunker.ChunkDto serializes a value with MessagePack and yields sequenced DtoWrapper chunks [Shared/Helpers/DtoChunker.cs:ChunkDto].
- DtoChunker.TryComplete collects chunks in a MemoryCache and deserializes the completed value [Shared/Helpers/DtoChunker.cs:TryComplete].
- PathSanitizer.TrySanitizeFileName reduces an input to a file name and rejects empty names, invalid characters, dot entries, and Windows device names [Shared/Helpers/PathSanitizer.cs:TrySanitizeFileName].
- RateLimiter.Throttle runs a callback at most once per cache key and duration; Reset clears its cache [Shared/Helpers/RateLImiter.cs:Throttle/Reset].
- Debouncer.Debounce schedules an Action after a delay keyed by caller name [Shared/Helpers/Debouncer.cs:Debounce].
- RandomGenerator.GenerateString and GenerateAccessKey create random strings [Shared/Helpers/RandomGenerator.cs:GenerateString/GenerateAccessKey].
- Disposer.TryDisposeAll and WaitHelper.WaitFor/WaitForAsync provide cleanup and polling helpers [Shared/Helpers/Disposer.cs:TryDisposeAll; Shared/Helpers/WaitHelper.cs:WaitFor/WaitForAsync].

## Invariants

- DtoChunker uses a default chunk size of 50,000 bytes and gives each chunk set an InstanceId; cached incomplete sets expire after one minute [Shared/Helpers/DtoChunker.cs:ChunkDto/TryComplete].
- PathSanitizer returns false rather than a usable name when input is blank, resolves to a dot entry, contains invalid file-name characters, or is a reserved Windows device name [Shared/Helpers/PathSanitizer.cs:TrySanitizeFileName].
- RateLimiter serializes calls through a SemaphoreSlim and caches a key only after its callback completes [Shared/Helpers/RateLImiter.cs:Throttle].

## Side Effects

- DtoChunker and RateLimiter retain temporary state in process-local MemoryCache instances; Debouncer creates and disposes timers [Shared/Helpers/DtoChunker.cs:_cache; Shared/Helpers/RateLImiter.cs:_cache; Shared/Helpers/Debouncer.cs:_timers].
- RateLimiter.Throttle invokes the supplied asynchronous callback [Shared/Helpers/RateLImiter.cs:Throttle].
- RandomGenerator uses RandomNumberGenerator.GetBytes [Shared/Helpers/RandomGenerator.cs:GenerateString].
- WaitHelper.WaitFor sleeps the calling thread; WaitForAsync uses Task.Delay [Shared/Helpers/WaitHelper.cs:WaitFor/WaitForAsync].
- Disposer.TryDisposeAll calls Dispose on each supplied object and suppresses exceptions [Shared/Helpers/Disposer.cs:TryDisposeAll].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```

