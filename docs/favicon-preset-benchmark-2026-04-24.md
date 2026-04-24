# Favicon preset benchmark, 2026-04-24

## Context

KeeFetch was tested against `X:\Downloads\keefetch_favicon_test_suite_200_FIXED_manifest.csv`, a 200-entry favicon test manifest. The goal was to tune fetch presets for a clearer speed/coverage tradeoff after users saw large batches taking multiple minutes.

The benchmark harness is `eng\benchmark-presets.ps1`. It loads the built `bin\Release\net48\KeeFetch.dll` via reflection, runs the same downloader code used by the plugin, and supports CSV inputs plus bounded concurrent downloads. The full-manifest runs below used `-Concurrency 8`, matching the plugin's bulk entry worker cap.

Example command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\eng\benchmark-presets.ps1' -UrlFile 'X:\Downloads\keefetch_favicon_test_suite_200_FIXED_manifest.csv' -Presets 'Fast','Balanced','Thorough' -Concurrency 8"
```

## Results

| Profile | Success | Not found | Synthetic | Cache hits | Average ms | Slowest ms | Notes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Fast | 117/200 | 83 | 0 | 30 | 7923 | 30107 | Fastest product preset, no synthetic fallback. |
| Balanced, old full chain | 127/200 | 73 | 11 | 33 | 9275 | 28630 | Direct Site, Google, Twenty Icons, DuckDuckGo, Yandex, Favicone. |
| Balanced without Twenty Icons | 124/200 | 76 | 23 | 26 | 8298 | 31482 | Faster but worse coverage on the full corpus. |
| Balanced without DuckDuckGo | 136/200 | 64 | 17 | 33 | 8409 | 29198 | Strong experimental result, but a later real-preset rerun showed timing variance. |
| Balanced, Google + Favicone | 136/200 | 64 | 19 | 33 | 9533 | 34609 | Best final balanced tradeoff after repeated full-corpus testing. |
| Thorough | 137/200 | 63 | 20 | 33 | 18475 | 49904 | Best coverage, roughly twice the average time of balanced. |

## Decision

Use `Balanced` as the recommended default with this provider chain:

```text
Direct Site -> Google -> Favicone
```

Rationale:

- It preserved nearly all observed `Thorough` coverage on the 200-entry corpus: `136/200` vs `137/200`.
- It avoided the costly exhaustive fallback tail that made `Thorough` average `18475 ms`.
- It avoided providers that were low-yield or noisy for the balanced use case on this corpus.
- It still gives users a clear escalation path: `Fast` for bulk speed, `Thorough` for maximum coverage.

## Provider observations

- `Direct Site` remains the most important provider and dominates elapsed time.
- `Google` is useful enough to stay in `Balanced`, but timing varies noticeably across runs.
- `Favicone` adds meaningful coverage cheaply enough to justify in `Balanced`.
- `DuckDuckGo` was poor in the full runs and is better left to `Thorough`.
- `Twenty Icons` looked useful in small samples but did not justify its cost in the final full-corpus balanced path.
- `Icon Horse` belongs only in `Thorough`; it is a last-resort synthetic provider and can create a slow tail.

## Current preset behavior

| Preset | Intended use | Provider chain |
| --- | --- | --- |
| Fast | Large batches where speed matters most | Direct Site -> Google -> Twenty Icons |
| Balanced | Recommended default | Direct Site -> Google -> Favicone |
| Thorough | Maximum coverage | Direct Site -> Twenty Icons -> DuckDuckGo -> Google -> Yandex -> Favicone -> Icon Horse |

## Follow-up work

1. Run a real KeePass UI batch with the rebuilt `KeeFetch.plgx` and compare the completion dialog against the harness results.
2. Add in-flight origin deduplication so concurrent entries for the same origin share one lookup instead of racing duplicate network work.
3. Add optional per-origin result export in diagnostics to identify which URL categories still miss.
4. Consider a small UI affordance that explains presets in user terms: speed, recommended, maximum coverage.
5. Keep `Thorough` available, but avoid making it the default unless a user explicitly prioritizes coverage over time.
