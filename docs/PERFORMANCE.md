# 0.4.0 test candidate: UI allocation reduction

The quota section, its rings, captions and countdown labels are now built once. Refresh updates their values and visibility. Unchanged theme brushes and settings backgrounds are reused. Ring setters invalidate rendering when their values change; theme changes explicitly refresh existing rings.

Adaptive glass retains its existing behavior: a translucent background, opaque controls/text, and light/dark colors selected from Windows theme. This candidate does not introduce backdrop blur.

Local comparison on Windows x64, Release build, .NET 10.0.14. Both runs used the same diagnostic instrumentation: warm up with 20 UI refreshes, then measure 1,000 unchanged `RefreshView` calls on the UI thread after opening/closing settings and the picker. The baseline used 0.3.1 UI logic. No forced garbage collection or working-set trimming is used.

| Measurement | Baseline | Optimized |
| --- | ---: | ---: |
| Allocated bytes for 1,000 UI refreshes | 70,147,912 | 1,104,328 |
| Elapsed milliseconds, one local run | 904 | 61 |
| Working set after diagnostic workload | 158,461,952 bytes | 157,425,664 bytes |
| Private bytes after diagnostic workload | 66,277,376 | 66,265,088 |

The meaningful result is approximately 98.4% fewer managed allocations in this synthetic refresh workload. Working set/private memory barely changed. These are not idle RAM measurements, network polling benchmarks, or a promise that Task Manager will fall by 98%. Rendering screenshots itself consumes memory, and runtime/OS caching varies between runs.

Run UI checks and inspect the `performance` section:

```powershell
.\build.ps1 -Installer
.\artifacts\installer-payload\LimitWidget.exe --smoke-test artifacts/performance-check
Get-Content artifacts/performance-check/smoke-result.json
```

UI checks cover object/brush reuse, quota visibility toggles, footer collapse, repainting a changed percentage, three themes/sizes, settings and the color picker. Security checks, helper signature validation, credential storage and polling intervals are retained.

For manual acceptance, compare ordinary Task Manager usage after the same actions and several refresh cycles; look for sustained growth, CPU while idle, correct changing percentages, and retained behavior of glass opacity, saved login, lock/on-top and settings.
