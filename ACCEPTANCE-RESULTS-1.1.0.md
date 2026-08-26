# News Publisher 1.1.0 — Acceptance Results

Date: 2026-08-26

## Result

- Windows parser, Smart Category Picker and advertisement model tests: **38 PASS, 0 FAIL**
- Diafaneia bridge, security, Yoast, UI and advertisement static checks: **56 PASS, 0 FAIL**
- Existing KARAGO Android/plugin baseline: **114 PASS, 0 FAIL**
- Total automated checks: **208 PASS, 0 FAIL**
- Windows Release compile: **PASS — 0 errors, 0 warnings**
- Self-contained `win-x64` publish: **PASS**
- Output identified as: **PE32+ Windows x86-64 GUI executable**

The WPF window could not be opened interactively in the Linux build environment. The executable was genuinely compiled and published, but final operator smoke testing must occur on Windows.

## Advertisement acceptance coverage

- Three supplied advertisements are preconfigured in the supplied order.
- First advertisement appears before the complete article.
- Every remaining advertisement appears after the complete article.
- Every configured advertisement appears exactly once.
- Sequential mode follows the operator-defined order.
- Up/Down controls change the fixed order.
- Random mode includes every advertisement exactly once and produces a different order.
- Adding more than three advertisements is supported; a 25-ad test passed.
- Existing configured advertisement HTML is removed before reapplication, preventing duplicates.
- Add, edit, delete and reorder controls are present.
- Settings persist in LocalAppData and are written atomically.
- No credential or secret was added to advertisement settings or source code.

## Unchanged functionality

- Real WordPress category retrieval and pagination
- Smart Category Picker, frequent categories, search and multi-select
- Diafaneia-only wrong-site protection
- Application Password authentication
- Windows Credential Manager storage
- HTTPS-only endpoint
- REQUEST_ID/idempotency
- Draft and Publish
- Optional featured image upload
- Yoast SEO title, meta description and focus keyword mapping
- WordPress bridge plugin 1.0.0 (unchanged)
