# News Publisher 1.2.0 / Diafaneia Connector 1.0.0 — Acceptance Results

Date: 2026-08-26

## Automated results executed in this environment

| Suite | Result |
| --- | ---: |
| Accepted News Publisher 1.1.1 pre-change static baseline | 56 PASS, 0 FAIL |
| New Windows/Connector/security/UI/regression static suite | 132 PASS, 0 FAIL |
| Existing KARAGO Android 1.1.2 parser/payload tests | 17 PASS, 0 FAIL |
| Existing KARAGO Android 1.1.2 Smart Category Picker tests | 35 PASS, 0 FAIL |
| Existing KARAGO Android 1.1.2 bridge/admin/security/UI checks | 38 PASS, 0 FAIL |
| Existing KARAGO Android 1.1.2 build/distribution checks | 24 PASS, 0 FAIL |
| Existing Android total | 114 PASS, 0 FAIL |
| Total checks actually executed | **302 PASS, 0 FAIL** |

The 132-check suite verifies the new REST contract, multi-device key isolation, per-device scopes/revocation, HTTPS-only identity, redirect blocking, credential redaction, real terms, Draft/Publish, featured/inline images, actual image type checks, Yoast fields, atomic REQUEST_ID reservation and conflicts, retry behavior, advertisement preservation, Smart Category Picker preservation, old bridge coexistence, workflow packaging and secret scanning.

## Baseline functional tests preserved in source

The existing .NET acceptance project remains present and now contains 39 parser/category/advertisement tests: the original 38 tests plus one new deterministic-random-order retry test.

## Checks that could not be executed here

- .NET compile and the 39 .NET acceptance tests: **NOT RUN** — the current container has no `dotnet` SDK/runtime.
- Self-contained Windows `win-x64` publish and `News-Publisher.exe`: **NOT BUILT** for the same reason.
- `php -l` and live WordPress runtime tests: **NOT RUN** — the container has no PHP/WordPress runtime.
- Live `/status`, `/terms` and `/publish`: **NOT RUN** — the new plugin is not installed on diafaneia.eu and no private key was requested or used.

The included GitHub Actions workflow runs the .NET acceptance project, the 132 static checks, the Windows publish and the Connector ZIP packaging on `windows-latest`.

No APK was built or changed. The accepted Alithenia/Sportaki Android 1.1.2 project was regression-tested separately and remains untouched.

