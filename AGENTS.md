# Agent Instructions

This repository contains an experimental Jellyfin server plugin.

Read `docs/prototype-spec.md` before changing implementation code.

## Priorities

1. Verify behavior against the current upstream Jellyfin source before relying on assumptions in the prototype specification.
2. Prefer supported Jellyfin plugin and ASP.NET extension mechanisms.
3. Avoid Jellyfin forks, client modifications, Harmony patches, reflection hacks, and private-field manipulation unless a supported approach has first been proven impossible.
4. Keep the first implementation diagnostic and minimal.
5. Reuse Jellyfin's own `DeviceProfile` evaluation rather than maintaining codec/device compatibility rules.

## Upstream research

When behavior depends on Jellyfin internals, inspect the current upstream source rather than relying on memory.

Relevant areas include:

* `Jellyfin.Api.Controllers.MediaInfoController`
* `Jellyfin.Api.Helpers.MediaInfoHelper`
* `Jellyfin.Api.Controllers.UserLibraryController`
* `Emby.Server.Implementations.Dto.DtoService`
* `Emby.Server.Implementations.Library.MediaSourceManager`
* `MediaBrowser.Model.Dlna.StreamBuilder`

Record important discoveries or deviations from the current prototype assumptions in `docs/upstream-notes.md`.

## Quality requirements

Before completing a coding task:

* build the complete solution;
* run the project's formatter;
* run all configured analyzers;
* address compiler warnings introduced by the change;
* address analyzer warnings introduced by the change;
* run relevant tests;
* do not leave TODOs merely to avoid resolving a known implementation issue.

Do not suppress analyzer warnings without explaining why suppression is appropriate.

## Prototype constraints

Do not add a configuration UI until the core behavior has been demonstrated against a real client.

Do not over-engineer abstractions around a behavior that has not yet been experimentally verified.

Prefer explicit logging during the prototype phase so request and media-source selection behavior can be inspected from Jellyfin logs.

## Compatibility target

This plugin must support Jellyfin 12.x.

Treat Jellyfin 12 as the primary and minimum supported server version unless the task explicitly says otherwise.

Do not add compatibility shims for Jellyfin 10.x or 11.x unless they are trivial and do not complicate the Jellyfin 12 implementation.

When inspecting upstream source or selecting NuGet package versions, use Jellyfin 12-compatible APIs and packages.

## Research-before-implementation rule

For tasks that depend on Jellyfin internals, required upstream research artifacts are blocking deliverables.

If a task instructs you to document upstream behavior in
`docs/upstream-notes.md`, do not proceed to implementation until that
documentation has been written.

Code is not a substitute for the research record.

Before implementing against an upstream API or internal behavior:

1. inspect the target Jellyfin 12 source;
2. document the relevant paths, methods, and conclusions;
3. note any discrepancy with the design specification;
4. only then implement.
