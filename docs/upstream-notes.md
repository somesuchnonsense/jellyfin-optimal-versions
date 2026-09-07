# Jellyfin 12 upstream notes

Research date: 2026-09-06.

## Pinned upstream revisions and package versions

The current Jellyfin 12 release is still a preview release. The newest published server release inspected for this prototype is **12.0 RC7**, tag `v12.0-rc7`, commit [`4910aafa1a8227a65a037d3d2d299a32691e4de3`](https://github.com/jellyfin/jellyfin/commit/4910aafa1a8227a65a037d3d2d299a32691e4de3) (released 2026-08-31).

The matching packages are present on NuGet and are the versions this plugin references:

| Component | Version / target | Evidence |
| --- | --- | --- |
| Jellyfin server | `v12.0-rc7` | Git tag and release commit above |
| `Jellyfin.Controller` | `12.0.0-rc7` | Published NuGet package; source `MediaBrowser.Controller/MediaBrowser.Controller.csproj` has `VersionPrefix` `12.0.0` |
| `Jellyfin.Model` | `12.0.0-rc7` | Published NuGet package; source `MediaBrowser.Model/MediaBrowser.Model.csproj` has `VersionPrefix` `12.0.0` |
| Target framework | `net10.0` | `Jellyfin.Server/Jellyfin.Server.csproj`, `MediaBrowser.Controller/MediaBrowser.Controller.csproj`, and `MediaBrowser.Model/MediaBrowser.Model.csproj` |
| SDK baseline | .NET SDK 10 | `global.json` requests `10.0.0` with `latestMinor` roll-forward |
| Plugin target ABI | `12.0.0.0` | Server `SharedVersion.cs` uses assembly version `12.0.0`; `PluginManager.LoadManifest()` compares the server version with `PluginManifest.TargetAbi` |

Both Jellyfin package references use `ExcludeAssets="runtime"`, matching the plugin-template loading requirement and preventing server-owned Jellyfin assemblies from being copied beside the plugin.

## Method-by-method source inventory

All Jellyfin links in this table are pinned to commit `4910aafa1a8227a65a037d3d2d299a32691e4de3`, not a moving branch.

| Upstream path and method | Relevant behavior |
| --- | --- |
| [`Jellyfin.Api/Controllers/MediaInfoController.cs`, `GetPlaybackInfo()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Api/Controllers/MediaInfoController.cs#L72-L88) | GET variant. Loads the item/user and calls `MediaInfoHelper.GetPlaybackInfo()` without a source ID or request `DeviceProfile`. It does not run the POST method's device-specific evaluation loop. |
| [`Jellyfin.Api/Controllers/MediaInfoController.cs`, `GetPostedPlaybackInfo()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Api/Controllers/MediaInfoController.cs#L90-L250) | POST variant. Binds legacy query parameters and optional `PlaybackInfoDto`, gives query values precedence with null-coalescing assignment, resolves the profile, obtains sources, calls `SetDeviceSpecificData()` per source when a profile exists, sorts, and optionally opens a live stream. |
| [`Jellyfin.Api/Helpers/MediaInfoHelper.cs`, `GetPlaybackInfo()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L84-L136) | Resolves sources, reports `NoCompatibleStream` when none match, JSON-clones source objects before request-specific mutation, rewrites published live-stream paths, and creates the play-session ID. |
| [`Jellyfin.Api/Helpers/MediaInfoHelper.cs`, `ResolvePlaybackMediaSources()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L138-L158) | Gives `liveStreamId` precedence; otherwise gets playback sources and returns all for a blank source ID or only case-insensitive ID matches for a nonblank ID. |
| [`Jellyfin.Api/Helpers/MediaInfoHelper.cs`, `SetDeviceSpecificData()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L160-L359) | Builds `MediaOptions` around one source and the client profile, invokes `StreamBuilder`, then writes request-specific support flags, transcode URL/container/reasons, subtitle delivery, and selected stream indexes. |
| [`Jellyfin.Api/Helpers/MediaInfoHelper.cs`, `SortMediaSources()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L361-L418) | Keeps the queried item's own source first, then considers Direct Play/Direct Stream, file protocol, bitrate fit, and original order. |
| [`Jellyfin.Api/Controllers/UserLibraryController.cs`, `GetItem()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Api/Controllers/UserLibraryController.cs#L74-L108) | Builds default DTO options and delegates item projection to `IDtoService`. |
| [`Emby.Server.Implementations/Dto/DtoService.cs`, DTO population block](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Emby.Server.Implementations/Dto/DtoService.cs#L390-L416) | Populates `BaseItemDto.MediaSources` from static sources when requested, without a playback request profile. |
| [`Emby.Server.Implementations/Library/MediaSourceManager.cs`, `GetPlaybackMediaSources()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Emby.Server.Implementations/Library/MediaSourceManager.cs#L177-L237) | Gets static and dynamic sources, probes when necessary, applies user permissions/default streams to dynamic sources, and performs its own source ordering. |
| [`Emby.Server.Implementations/Library/MediaSourceManager.cs`, `GetStaticMediaSources()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Emby.Server.Implementations/Library/MediaSourceManager.cs#L383-L418) | Applies source visibility, user track defaults, coarse user transcoding/remux permissions, and alternate-version resume states. It does not consume a `DeviceProfile`. |
| [`Emby.Server.Implementations/Library/MediaSourceManager.cs`, private `SortMediaSources()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Emby.Server.Implementations/Library/MediaSourceManager.cs#L606-L632) | Keeps the queried/preferred source first, then video-file, non-3D, and wider sources. |
| [`MediaBrowser.Model/Dlna/StreamBuilder.cs`, `GetOptimalVideoStream()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/MediaBrowser.Model/Dlna/StreamBuilder.cs#L225-L303) and [`BuildVideoItem()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/MediaBrowser.Model/Dlna/StreamBuilder.cs#L646-L825) | Evaluates sources against `MediaOptions.Profile`; in this controller path it receives one source at a time. `BuildVideoItem()` performs Direct Play/Direct Stream/transcode selection and records reasons. |
| [`MediaBrowser.Model/Dlna/StreamBuilder.cs`, `GetVideoDirectPlayProfile()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/MediaBrowser.Model/Dlna/StreamBuilder.cs#L1278-L1418) | Evaluates direct-play profiles and their codec/profile/level/resolution/range and related conditions, returning a play method and reason flags. |
| [`MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs`, `RegisterServices()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs#L1-L19) | Public plugin extension contract for adding services before the container is built. |
| [`Emby.Server.Implementations/Plugins/PluginManager.cs`, `RegisterServices()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Emby.Server.Implementations/Plugins/PluginManager.cs#L193-L239) | Discovers parameterless plugin registrators, checks plugin state/support, creates them, and invokes service registration. |
| [`Emby.Server.Implementations/Plugins/PluginManager.cs`, `CreatePluginInstance()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Emby.Server.Implementations/Plugins/PluginManager.cs#L549-L616) | Creates the main plugin through `ActivatorUtilities`, then reads initialized `Version`, ID, name, description, and assembly path while attaching or creating its plugin record. |
| [`MediaBrowser.Common/Plugins/BasePluginOfT.cs`, `BasePlugin<TConfiguration>` constructor](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/MediaBrowser.Common/Plugins/BasePluginOfT.cs#L35-L65) | Receives `IApplicationPaths` and `IXmlSerializer`, derives assembly/data paths and version from the loaded assembly, and calls `SetAttributes()`. Plain non-generic `BasePlugin` has no equivalent constructor initialization. |
| [`Emby.Server.Implementations/ApplicationHost.cs`, `Init()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Emby.Server.Implementations/ApplicationHost.cs#L461-L493) | Discovers types, registers server services, then invokes plugin service registration. |
| [`Jellyfin.Server/Program.cs`, `StartServer()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Server/Program.cs#L159-L196) | Calls `appHost.Init(services)` during host service configuration and only then builds the host. |
| [`Jellyfin.Server/Startup.cs`, `Configure()`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Server/Startup.cs#L160-L257) | Defines Jellyfin's middleware order inside its base-URL branch, including response compression before routing/endpoints. |

ASP.NET Core's [`IStartupFilter` contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.hosting.istartupfilter?view=aspnetcore-10.0) and [startup-filter ordering documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/startup?view=aspnetcore-10.0#extend-startup-with-startup-filters) independently establish what a service registered as `IStartupFilter` does. Jellyfin does not need a plugin-specific startup-filter hook beyond exposing the normal service collection before host construction.

### Official plugin template status

The official `jellyfin-plugin-template` `master` revision inspected was [`726279e026ff82e3bebea1bcd8f106412a718952`](https://github.com/jellyfin/jellyfin-plugin-template/commit/726279e026ff82e3bebea1bcd8f106412a718952) (2026-09-03). Its sample project still targets `net9.0`, references Jellyfin `10.11.5`, and declares target ABI `10.11.0.0`. It is therefore **not Jellyfin 12-compatible as written**. This repository retains the template's useful project/analyzer/build-manifest structure, but deliberately replaces those framework, package, and ABI values with the matching RC7 values above. The sample configuration page was removed because this prototype must not add a configuration UI.

## PlaybackInfo request binding

Source: [`Jellyfin.Api/Controllers/MediaInfoController.cs`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Api/Controllers/MediaInfoController.cs), method `GetPostedPlaybackInfo()` (route `POST Items/{itemId}/PlaybackInfo`, lines 90-250).

- `itemId` is an ASP.NET route-bound `Guid`.
- The obsolete query parameter `mediaSourceId` is bound as `string?`.
- The optional JSON body is bound as `PlaybackInfoDto?`.
- Query values have precedence. At line 158 the controller executes `mediaSourceId ??= playbackInfoDto?.MediaSourceId`; therefore the body is used only when the query binding produced `null`.
- A present-but-empty query parameter is a subtle exception: it does not fall back to the body because an empty string is non-null, but the helper later treats it as absent because it uses `string.IsNullOrWhiteSpace()`.
- The controller passes the effective value to `MediaInfoHelper.GetPlaybackInfo()` at lines 177-183.

`Jellyfin.Api/Models/MediaInfoDtos/PlaybackInfoDto.cs`, property `MediaSourceId` (lines 41-44), confirms that `MediaSourceId` is supported in the request body.

### Moonfin serialization

Current Moonfin source was inspected at commit [`560fa54bb2d9c03258adba963c1402b5cf03ca0a`](https://github.com/Moonfin-Client/Moonfin-Core/commit/560fa54bb2d9c03258adba963c1402b5cf03ca0a) (2026-09-06):

- `packages/server_core/lib/src/models/playback_models.dart`, `PlaybackInfoRequest.toJson()` writes both `MediaSourceId` and `DeviceProfile` into the JSON body when present.
- `packages/server_jellyfin/lib/src/api/jellyfin_playback_api.dart`, `JellyfinPlaybackApi.getPlaybackInfo()` also copies body `MediaSourceId` into the `mediaSourceId` query parameter and posts the complete body.
- `packages/playback_jellyfin/lib/src/jellyfin_media_stream_resolver.dart`, `resolve()` constructs that request with the selected source ID and device profile.
- `lib/ui/screens/detail/item_detail_screen.dart` initializes the selected source from `item.mediaSources.first`, confirming the default-first client behavior in current source.

This confirms the query-and-body duplication in current Moonfin source. It supports, but cannot by itself prove, the behavior of a separately built Moonfin 2.5.1 binary observed in the original capture.

## Behavior when `mediaSourceId` is absent

Source: [`Jellyfin.Api/Helpers/MediaInfoHelper.cs`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/Jellyfin.Api/Helpers/MediaInfoHelper.cs), methods `GetPlaybackInfo()` (lines 93-136) and `ResolvePlaybackMediaSources()` (lines 138-158).

- A non-empty `liveStreamId` takes precedence and resolves one live source.
- Otherwise the helper always calls `IMediaSourceManager.GetPlaybackMediaSources()` first.
- A null, empty, or whitespace `mediaSourceId` returns all sources.
- A non-empty ID filters that collection using an ordinal, case-insensitive comparison. No match produces an empty array and ultimately `PlaybackErrorCode.NoCompatibleStream`.

The prototype assumption that removing the ID exposes all merged versions is confirmed, subject to the `liveStreamId` exception.

However, exposing all sources does **not** make Jellyfin choose the most playable source automatically:

- `Emby.Server.Implementations/Library/MediaSourceManager.cs`, `GetPlaybackMediaSources()` (lines 177-237) and private `SortMediaSources()` (lines 606-631), sort the queried item's source first, then video files/non-3D sources, then width.
- `Jellyfin.Api/Helpers/MediaInfoHelper.cs`, `SortMediaSources()` (lines 367-418), again forces the queried item's source first before considering direct-play/direct-stream flags, protocol, and bitrate.
- The comments explicitly say this preserves per-version resume state and makes transcoding the queried version preferable to switching siblings.

Response reordering is therefore essential to the proposed behavior; request unpinning alone is insufficient.

## When device-specific fields are populated

Source: `MediaInfoController.GetPostedPlaybackInfo()` and `MediaInfoHelper.SetDeviceSpecificData()` (lines 182-359).

1. The POST controller reads `PlaybackInfoDto.DeviceProfile` first. If absent, it asks `IDeviceManager.GetCapabilities()` for the authenticated device's stored profile.
2. `GetPlaybackInfo()` obtains and JSON-clones the source list before device evaluation.
3. Only when a profile was found does the controller call `SetDeviceSpecificData()` for every returned source.
4. `SetDeviceSpecificData()` creates Jellyfin's `MediaBrowser.Model.Dlna.StreamBuilder`, supplies exactly one source plus the real `DeviceProfile`, user policy, bitrate and stream selections, and calls `GetOptimalVideoStream()` or `GetOptimalAudioStream()`.
5. It then populates `SupportsDirectPlay`, `SupportsDirectStream`, `SupportsTranscoding`, `TranscodingUrl`, transcoding container/protocol, default stream indexes, subtitle delivery information, and `TranscodeReasons` from that result.

Important limitations:

- `GET Items/{itemId}/PlaybackInfo` does not run this POST-only device-profile loop.
- A POST with neither a body profile nor stored device capabilities also skips device-specific evaluation; source flags then retain their generic/static/user-policy values.
- In RC7 `SetDeviceSpecificData()` sets `options.EnableDirectStream = false` unless `MediaOptions.ForceDirectStream` is true (lines 261-265), but that property is not set in this call path. Lines 279-283 consequently report `SupportsDirectStream` as true only when the selected method is also Direct Play. Therefore the specification's proposed `SupportsDirectStream` second tier does **not reliably identify remux/direct-stream candidates on Jellyfin 12 RC7**.
- Jellyfin's internal `StreamBuilder.DirectStreamReasons` mask (lines 22-27 of `MediaBrowser.Model/Dlna/StreamBuilder.cs`) and the populated `TranscodeReasons` can distinguish reasons compatible with stream copy from reasons requiring video conversion, but the mask itself is internal. Any plugin-side mirror must be narrowly tested against the pinned Jellyfin version.
- [`MediaBrowser.Model/Dto/MediaSourceInfo.cs`](https://github.com/jellyfin/jellyfin/blob/4910aafa1a8227a65a037d3d2d299a32691e4de3/MediaBrowser.Model/Dto/MediaSourceInfo.cs#L14-L28) initializes all three support flags to `true`. Before `SetDeviceSpecificData()` overwrites them, they are optimistic defaults/coarse permission flags, not proof of compatibility with the requesting client.
- `MediaSourceInfo.TranscodeReasons` is marked `JsonIgnore` (lines 119-123 of that file). It is available to a typed MVC result filter before serialization but absent from the HTTP `PlaybackInfoResponse` JSON. A response-buffering middleware cannot use those reasons unless Jellyfin first changes the wire contract.

## Item DTO path

- `Jellyfin.Api/Controllers/UserLibraryController.cs`, `GetItem()` (lines 82-108), builds a default `DtoOptions` and calls `IDtoService.GetBaseItemDto()`.
- `MediaBrowser.Controller/Dto/DtoOptions.cs`, constructor (lines 27-46), enables all fields by default, including media sources.
- `Emby.Server.Implementations/Dto/DtoService.cs`, DTO construction (lines 403-409), calls `MediaSourceManager.GetStaticMediaSources()` and assigns `BaseItemDto.MediaSources`.
- `Emby.Server.Implementations/Library/MediaSourceManager.cs`, `GetStaticMediaSources()` (lines 383-418), applies user visibility, track defaults, user transcoding/remux permissions, and alternate-version resume state. It has no request `DeviceProfile` and does not perform client codec evaluation.

The specification is correct that the item-detail path is not the place to duplicate device compatibility logic.

## Plugin service and middleware registration

The supported service-registration path is confirmed:

- `MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs` exposes `RegisterServices(IServiceCollection, IServerApplicationHost)` and requires a parameterless implementation.
- `Emby.Server.Implementations/Plugins/PluginManager.cs`, `RegisterServices()` (lines 201-239), discovers enabled plugin implementations and invokes them before dependency injection is instantiated.
- `Emby.Server.Implementations/ApplicationHost.cs`, `Init()` (lines 462-493), registers server services and then plugin services.
- `Jellyfin.Server/Program.cs`, `StartServer()` (lines 159-186), calls `appHost.Init(services)` before building the host.

`IStartupFilter` is a standard ASP.NET Core extension point specifically intended to add middleware around `Startup.Configure`. Registering `IStartupFilter` from an `IPluginServiceRegistrator` therefore works with this startup sequence and compiles against the exact RC7 packages. The prototype uses this mechanism.

### Middleware ordering

`Jellyfin.Server/Startup.cs`, `Configure()` (lines 166-256), maps the configured Jellyfin base URL and installs exception handling, response compression, authentication, routing, authorization, and endpoints inside that branch.

The plugin startup filter adds its middleware before invoking Jellyfin's `Startup.Configure`, so it wraps the whole pipeline and sees the original path (including any configured base-URL prefix). This is early enough to rewrite query/body data before routing and MVC model binding. It also means:

- the classifier must tolerate a base-URL prefix;
- the middleware runs before authentication, so it must never log tokens or full request bodies;
- its response-side code runs outside Jellyfin's response-compression middleware.

## Request and response buffering findings

### Request

ASP.NET Core's supported `HttpRequest.EnableBuffering()` API makes the body seekable for repeated reads. Its default in-memory threshold is 30 KiB; larger bodies spill to a temporary file, and the no-limit overload has no plugin-specific cap. After inspection, `Body.Position` must be restored to zero before MVC model binding.

References: [Microsoft's `EnableBuffering` API documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httprequestrewindextensions?view=aspnetcore-10.0) and [request-body buffering guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/use-http-context?view=aspnetcore-10.0#enable-request-body-buffering).

The prototype middleware follows that pattern and parses the body as a mutable `JsonNode`. For a marked implicit/default request it removes every case-insensitive top-level `MediaSourceId` property, replaces the request stream only when the body changed, updates `ContentLength`, and removes every case-insensitive query key. Unknown JSON fields and the complete nested `DeviceProfile` remain in the object graph; insignificant JSON whitespace is not preserved. The original stream, query, and content length are restored after the downstream pipeline completes, and a replacement stream is disposed there.

Risks are temporary-file I/O, extra latency/allocation for a large `DeviceProfile`, malformed or aborted bodies, and imposing a plugin buffer limit lower than Jellyfin's accepted request-size limit. The implementation uses the framework's default buffering behavior without adding a lower plugin limit. JSON and buffering I/O failures make the rewrite fail closed: the complete request, including a matching query pin, is left unchanged. Cancellation still propagates.

### Response

A naive outer middleware cannot safely assume its buffered bytes are JSON. Jellyfin calls `UseResponseCompression()` inside the plugin's startup-filter middleware, so clients advertising gzip/Brotli can cause the inner middleware to write compressed bytes to the plugin's replacement response stream. Reordering also requires correct handling of status code, JSON content type, `Content-Encoding`, `Content-Length`, streaming/flush behavior, cancellation, and copying the original response on every failure path.

Although decompression/recompression or temporarily forcing identity encoding is technically possible, neither is the preferred prototype design. The safer supported approach is a narrowly scoped global MVC result filter registered through `MvcOptions`: inspect only the `MediaInfoController.GetPostedPlaybackInfo` object result marked by the request middleware, reorder the in-memory `PlaybackInfoResponse.MediaSources`, and let Jellyfin's normal JSON formatter and response-compression middleware serialize it. This avoids buffering, compressed-payload manipulation, and unknown-field loss.

## Confirmed, wrong, and incomplete assumptions

Confirmed:

- A non-empty effective source ID filters PlaybackInfo to the matching source.
- With no source ID, all playback sources are returned (unless a live-stream ID takes precedence).
- POST PlaybackInfo evaluates each returned source with Jellyfin's real `DeviceProfile` when one is available.
- The item DTO path has static media-source information but not the request's full device profile.
- A plugin registrator can register an ASP.NET Core startup filter.
- Moonfin's current source sends `MediaSourceId` in both query and body and sends `DeviceProfile` in the body.

Wrong or incomplete:

- Removing `mediaSourceId` does not cause Jellyfin 12 to select the most playable source; two upstream sorts intentionally retain the queried item first.
- Query/body precedence needed to be stated exactly, including the present-empty-query edge case.
- Device-specific flags are not populated for every PlaybackInfo call: profile-less POST and GET skip the device-specific loop.
- `SupportsDirectStream` cannot be used as the RC7 remux tier as proposed; in this path it is effectively collapsed into Direct Play.
- `TranscodeReasons` cannot be recovered by buffering/deserializing the public JSON response because that property is explicitly excluded from serialization.
- One outer middleware can rewrite a response only with explicit compression/header/error handling. Treating the buffered response as plain JSON is unsafe.
- Explicit selection of the primary source remains indistinguishable from the client's default primary choice.

## Implemented end-to-end experiment

This iteration implements only the experiment needed to prove that a plugin can expose all versions to Jellyfin's existing device-profile evaluation:

1. The startup-filter middleware marks only `POST Items/{itemId}/PlaybackInfo` requests whose effective source GUID equals the route item GUID. It implements the controller's query-over-body, null-based precedence, including the present-empty-query edge case.
2. For a marked request, it removes the ID from both binding locations before MVC executes. It stores a correlation record in `HttpContext.Items` and leaves explicit non-default source selections untouched.
3. A global `IAsyncResultFilter`, registered with `MvcOptions`, observes only marked typed `PlaybackInfoResponse` results. It does not buffer HTTP response bytes and does not mutate or reorder `MediaSources`.
4. The filter logs original position, source ID, dimensions, video codec, bitrate, all three support flags, and the non-serialized `TranscodeReasons`. Playback and transcode URLs are never logged.
5. Automated tests cover query-only and body-only IDs, query precedence, matching/nonmatching/blank IDs, preservation of nested body data, non-PlaybackInfo scoping, result-filter scoping, correlation, and unchanged source order.

### Live RC7/Moonfin validation

A live Moonfin playback against Jellyfin 12 RC7 on 2026-09-06 confirmed the complete experiment with one correlation ID:

- Jellyfin loaded plugin version 0.1.0.2 and invoked both the registered `IStartupFilter` and its middleware configurator.
- Moonfin supplied the route item GUID as `MediaSourceId` in both the query and body, confirming the source-level serialization finding against the running client.
- The middleware identified the request as an implicit/default pin and removed both values before MVC binding.
- Jellyfin returned two evaluated sources, and the globally registered MVC result filter received the typed `PlaybackInfoResponse` before serialization.
- The original source remained first, confirming that unpinning alone does not change Jellyfin's preferred order.
- Both HEVC 3840×1606 and H.264 1918×802 sources reported Direct Play, Direct Stream, and Transcoding support with `TranscodeReasons=None` (`0`). This is consistent with the RC7 finding that Direct Stream mirrors Direct Play in this path and that `SupportsTranscoding` represents availability rather than the chosen play method.

This runtime evidence confirms the supported plugin registration and request/result interception architecture. It does not yet demonstrate a visible media-version switch because response ordering remains intentionally unchanged and both observed sources occupy the same Direct Play capability tier.

## Proposed iteration after the live experiment

Only after the correlated live logs prove the experiment should the filter begin reordering. The intended next step is:

1. Rank sources as Direct Play first; then sources whose Jellyfin-produced `TranscodeReasons` contain only RC7's direct-stream-compatible reasons; then remaining transcodes. This consumes Jellyfin's decision rather than maintaining device/codec rules. Pin and unit-test the reason mask against RC7 because the upstream constant is internal.
2. Within a class, rank by video pixel area (width × height), then video bitrate/source bitrate, then original index. Preserve all source objects and all non-source response fields unchanged.
3. Log old/new positions and add tests for the RC7 reason mask, stable ranking, null metadata, malformed requests, non-success MVC results, and profile-less responses before broadening scope.

## Assumption status matrix

| Prototype-spec assumption | Status | Finding |
| --- | --- | --- |
| Moonfin selects a source from item `MediaSources` before PlaybackInfo. | Confirmed for current source; captured 2.5.1 binary still observational | Current UI initializes the selected ID from the first DTO source and passes it through the playback resolver. |
| Moonfin sends `mediaSourceId` in the query. | Confirmed for current source | `JellyfinPlaybackApi.getPlaybackInfo()` copies body `MediaSourceId` into the query. |
| Moonfin may send body `MediaSourceId`. | Confirmed for current source | `PlaybackInfoRequest.toJson()` writes it, so both locations must be removed for an unpinned request. |
| Query has priority over body. | Confirmed with qualification | Priority is null-based. A present empty query suppresses body fallback but is later treated as unpinned. |
| A present source ID evaluates only that source. | Confirmed | The helper first gets all sources and then filters to matching IDs; a stale ID yields no compatible stream. |
| An absent source ID evaluates all sources. | Confirmed with qualification | True for blank ID unless `liveStreamId` takes precedence. |
| Jellyfin will naturally prefer the most playable version once unpinned. | Disproven | Both upstream sorts explicitly preserve the queried item's source as default. |
| PlaybackInfo uses the supplied client profile. | Confirmed with qualification | Only POST, and only when body profile or stored device capabilities supply a profile. |
| Support flags in item DTOs are client-specific. | Disproven | Item DTOs use static sources and coarse user permissions; the flags default to true before request-specific evaluation. |
| `SupportsDirectPlay` is meaningful after POST device evaluation. | Confirmed | It is set from `StreamInfo.PlayMethod == DirectPlay`. |
| `SupportsDirectStream` is a usable RC7 remux rank. | Disproven | The POST path disables `EnableDirectStream`, making the returned flag effectively mirror Direct Play. |
| `SupportsTranscoding` is meaningful after POST device evaluation. | Confirmed with qualification | It combines the selected method/profile availability and user policy; it does not distinguish remux from full transcode. |
| Jellyfin's own profile evaluator should be reused. | Confirmed | `SetDeviceSpecificData()` already runs `StreamBuilder` with the real profile and source. |
| Item DTO construction has the full playback profile. | Disproven | The DTO path has no request `DeviceProfile`. |
| A normal plugin can register `IStartupFilter`. | Confirmed by source architecture | Plugin registrators receive the pre-build `IServiceCollection`; ASP.NET Core consumes registered startup filters when composing `Startup.Configure`. |
| Startup-filter middleware can inspect/rewrite request query and body before MVC binding. | Confirmed with operational qualifications | It wraps Jellyfin's pipeline; body rewinding, size/disk costs, malformed input, content length, and stream lifetime must be handled. |
| The same outer middleware can simply buffer JSON and reorder the response. | Disproven as a safe design | It may receive compressed bytes, must repair response headers, and cannot see JSON-ignored `TranscodeReasons`. A typed MVC result filter is safer. |
| Comparing effective source ID with route item ID distinguishes implicit default selection. | Qualified heuristic only | It matches the observed default, but an explicit selection of the primary version is indistinguishable. |
| Higher resolution/bitrate are available as quality tie-breakers. | Confirmed | Width/height and bitrate are present on evaluated `MediaSourceInfo`/video stream data, but null handling and stable original-order fallback need tests. |

## Review of the implemented experiment

This review uses the upstream findings above as its premise; successful compilation is only a compatibility check, not architectural evidence.

- `Jellyfin.Plugin.OptimalVersions.csproj` matches the inspected RC7 server: `net10.0`, `Jellyfin.Controller` and `Jellyfin.Model` `12.0.0-rc7`, a framework reference to `Microsoft.AspNetCore.App`, and no bundled Jellyfin runtime assemblies.
- `Plugin` derives from `BasePlugin<PluginConfiguration>` and uses RC7's standard `IApplicationPaths`/`IXmlSerializer` constructor. This is necessary even though the prototype exposes no configuration UI: deriving directly from plain `BasePlugin` leaves its version and paths uninitialized and can make `PluginManager.CreatePluginInstance()` throw when it reads `instance.Version`.
- `PluginServiceRegistrator` registers `IStartupFilter`, the result filter, and its global `MvcOptions` filter entry; this matches the verified pre-container plugin registration lifecycle and uses only public ASP.NET Core/Jellyfin contracts.
- `PlaybackInfoStartupFilter` adds the request rewriter before calling the supplied `next` configurator, so it wraps Jellyfin's pipeline as documented.
- `PlaybackInfoRequestClassifier` accepts the optional base-URL prefix, restricts observation to POST PlaybackInfo paths, parses the route item as a GUID, and compares source IDs as GUIDs. Its query-present/body-fallback model matches `GetPostedPlaybackInfo()`'s null-based precedence, including the empty-query edge case.
- `PlaybackInfoLoggingMiddleware` uses `EnableBuffering()`, rewinds before MVC binding, and changes only the two source-ID locations for marked requests. It does not log the body, user ID, device profile, token, URL, or playback URL.
- `PlaybackInfoDiagnosticsResultFilter` consumes the typed response before serialization, can observe `TranscodeReasons`, and logs the original list without changing it.
- The prototype contains no configuration UI, raw response buffering, capability ranking, or response reordering.

No implementation depends on the disproven assumptions about automatic Jellyfin source ordering, `SupportsDirectStream`, response JSON visibility of `TranscodeReasons`, or plain-text response buffering. Real-server loading and a correlated Moonfin capture remain the acceptance test before response ordering is implemented.
