# Jellyfin Optimal Versions Plugin — Prototype Specification

## Objective

Prototype a Jellyfin server plugin that causes stock Jellyfin clients to default to the media version that can be played most efficiently by that client's actual `DeviceProfile`.

The primary target behavior is:

1. Prefer full Direct Play.
2. Then prefer Direct Stream/remux.
3. Then prefer sources requiring transcoding.
4. Within equivalent playback classes, prefer the higher-quality source.
5. Preserve explicit user version selections wherever possible.

Do not patch or fork Jellyfin clients.

## Observed client behavior

Moonfin 2.5.1 was observed making a request of the form:

POST /Items/{itemId}/PlaybackInfo
?userId={userId}
&mediaSourceId={mediaSourceId}
&maxStreamingBitrate=...

In the observed default-selection case:

itemId == mediaSourceId

Example:

itemId:
fb5893bacabafc2854c3bed2b2283442

mediaSourceId:
fb5893bacabafc2854c3bed2b2283442

Because `mediaSourceId` is already pinned, Jellyfin evaluates only that media source rather than all merged versions.

## Relevant Moonfin behavior

Moonfin fetches the item including its `MediaSources` before playback, then chooses a media source and sends that ID in the subsequent `PlaybackInfo` request.

The plugin should not require modifications to Moonfin or any other client.

## Relevant Jellyfin behavior

Investigated code paths include:

* `Jellyfin.Api.Controllers.UserLibraryController`
* `Emby.Server.Implementations.Dto.DtoService`
* `Emby.Server.Implementations.Library.MediaSourceManager`
* `Jellyfin.Api.Controllers.MediaInfoController`
* `Jellyfin.Api.Helpers.MediaInfoHelper`
* `MediaBrowser.Model.Dlna.StreamBuilder`

Before implementation, verify all described behavior against the current Jellyfin source revision being targeted.

### Item DTO path

The item-detail endpoint eventually populates:

`BaseItemDto.MediaSources`

using `IDtoService` / `DtoService` and `MediaSourceManager`.

This path does not have the requesting client's full playback `DeviceProfile`, so do not implement codec-capability logic here unless later investigation shows a clean supported mechanism.

### PlaybackInfo path

`MediaInfoController` receives an optional `mediaSourceId`.

`MediaInfoHelper.ResolvePlaybackMediaSources()` effectively behaves as follows:

* if `mediaSourceId` is present, only the matching source is evaluated;
* if it is absent, all available media sources are evaluated.

When all sources are evaluated, Jellyfin already applies the supplied `DeviceProfile` and populates playback-specific properties such as:

* `SupportsDirectPlay`
* `SupportsDirectStream`
* `SupportsTranscoding`
* transcoding information / reasons

This existing capability evaluation should be reused. Do not create a separate codec compatibility database.

## Proposed prototype

Implement the behavior as ASP.NET middleware registered from a normal Jellyfin plugin.

Investigate `IPluginServiceRegistrator` plus `IStartupFilter` as the preferred mechanism.

Do not use Harmony/runtime patching unless normal plugin middleware proves impossible.

### Request phase

Intercept:

`POST /Items/{itemId}/PlaybackInfo`

Determine:

* `itemId`
* query-string `mediaSourceId`
* body `MediaSourceId`, if present

For the prototype, intervene only when the source appears to be an implicit/default selection:

`mediaSourceId == itemId`

For such requests:

1. Remove the pinned `mediaSourceId` from the request seen by Jellyfin.
2. Also remove or null the body `MediaSourceId` if present.
3. Preserve the complete `DeviceProfile` and all other request fields.
4. Forward the modified request through the normal Jellyfin endpoint.

If:

`mediaSourceId != itemId`

leave the request untouched, because it may represent an explicit alternate-version selection.

Known limitation:

A user explicitly selecting the primary version may be indistinguishable from accepting the client's default primary version.

Document this limitation rather than attempting to infer user intent without evidence.

### Response phase

For an intercepted request, buffer the `PlaybackInfoResponse`.

Reorder `MediaSources`.

Initial ranking:

1. `SupportsDirectPlay == true`
2. `SupportsDirectStream == true`
3. sources requiring transcoding

Within the same playback class:

1. higher video resolution
2. higher video bitrate
3. original Jellyfin ordering as final tie-breaker

Do not otherwise modify `MediaSourceInfo`.

The purpose of the first prototype is to establish whether a stock client uses the first returned `MediaSourceInfo` after this modified PlaybackInfo exchange.

## Prototype logging

Add debug/information logging sufficient to observe:

* request item ID
* incoming media source ID
* whether the plugin classified the request as implicit/default
* whether query/body media source IDs were removed
* every media source returned by Jellyfin
* source ID
* resolution
* video codec
* bitrate
* `SupportsDirectPlay`
* `SupportsDirectStream`
* `SupportsTranscoding`
* original position
* new position

Avoid logging authentication tokens or unnecessary personal information.

## Scope

For the first prototype:

* Jellyfin only
* movies and episodes with multiple media versions
* no configuration UI required
* no plugin catalog packaging required
* no attempt to hide media versions
* no persistent client capability cache
* no changes to resume-state behavior
* no Harmony/runtime method patches unless required to prove feasibility

## Success criterion

Given a merged item containing:

* 2160p source requiring video transcoding on the requesting client
* 1080p source capable of Direct Play

the client should ultimately play the 1080p source without the user manually selecting it.

Given a client capable of Direct Playing both versions, the 2160p version should remain preferred.

## Development methodology

Before coding:

1. Inspect the current Jellyfin plugin template.
2. Inspect the exact current implementations of all Jellyfin classes listed above.
3. Confirm that `IStartupFilter` middleware registration still works from a plugin.
4. Confirm how ASP.NET request-body buffering and response buffering should be done in Jellyfin's current target framework.
5. Confirm whether `PlaybackInfoDto.MediaSourceId` is serialized in the body by clients such as Moonfin.

Then implement the smallest diagnostic prototype possible.

Do not prematurely add configuration pages, plugin repositories, installers, or generalized policy systems.

Run formatting, build, analyzers, and tests before considering the prototype complete.

## Target platform

* Jellyfin server: 12.x
* Primary development target: current Jellyfin 12 release
* Client compatibility: stock clients; no client modifications required