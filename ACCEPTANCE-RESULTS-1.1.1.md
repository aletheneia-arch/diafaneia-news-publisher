# News Publisher 1.1.1 — Acceptance Results

Date: 2026-08-26

## Distribution hotfix

Version 1.1.1 corrects the Windows portable package. Version 1.1.0 omitted five native WPF runtime DLLs from its ZIP even though the build had produced them. The 1.1.1 package includes the executable together with every required native DLL from the normal publish output.

## Result

- Windows parser, Smart Category Picker and advertisement model tests: **38 PASS, 0 FAIL**
- Diafaneia bridge, security, Yoast, UI and advertisement static checks: **56 PASS, 0 FAIL**
- Existing KARAGO Android/plugin baseline: **114 PASS, 0 FAIL**
- Total automated checks: **208 PASS, 0 FAIL**
- Windows Release compile: **PASS — 0 errors, 0 warnings**
- Self-contained `win-x64` publish: **PASS**
- Executable: **PE32+ Windows x86-64 GUI**
- Required native WPF DLLs included in final portable ZIP: **5/5 PASS**

The WPF window cannot be opened interactively inside the Linux build environment. Final launch confirmation must be performed on Windows.

## Required runtime files included

- D3DCompiler_47_cor3.dll
- PenImc_cor3.dll
- PresentationNative_cor3.dll
- vcruntime140_cor3.dll
- wpfgfx_cor3.dll

The portable folder also includes:

- News-Publisher.exe

## WordPress

The WordPress bridge remains version 1.0.0 and requires no replacement.
