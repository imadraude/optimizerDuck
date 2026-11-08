# Evidence-based performance work

This fork deliberately does **not** promise FPS improvements from changing registry values.

## Phase 1 changes

- Resolve display adapter configuration using the actual PnP instance's `Enum\\...\Driver` registration instead of assuming `VideoController1 == ...\\0000`; missing or inaccessible mappings are skipped.
- Remove global MMCSS and `Win32PrioritySeparation` presets that cannot be justified as universal performance optimizations.
- Remove the list of 200+ forced service startup types. Windows can use service triggers; resetting all of them is not a game optimization.
- Fix Windows 10 news visibility (`ShellFeedsTaskbarViewMode = 2`); correct misleading "latency" tags on keyboard repeat.
- Escalate risk labels for aggressive power and vendor driver registry tweaks. Risk labels are *not* proof the tweaks work.
- Add a read-only PresentMon comparison tool and unit tests.

## Measurement protocol

The only supported benchmark claim is one backed by repeated measurements. Run at least **3 captures per condition**, interleaved A/B/A/B where possible, of the *same game scene* with identical resolution, graphics, frame cap, power/AC, background tasks, temperature and driver version. Use [PresentMon](https://github.com/GameTechDev/PresentMon) to capture per-frame `MsBetweenPresents` (or the older `FrameTime` column). Frame-interval measurements do **not** equal end-to-end input latency or displayed FPS.

Example:

```powershell
python tools/performance/compare_presentmon.py --before before1.csv before2.csv before3.csv --after after1.csv after2.csv after3.csv --process GTA5.exe --output perf-report.md
python -m unittest discover -s tools/performance/tests -v
```

Report mean-rate FPS, 1% low, p95/p99 frame interval and frame intervals over 50ms. Compare variation between runs. A single positive result is not a verified performance improvement. Do not apply several tweaks between A/B captures: each tweak needs isolated evaluation.

## Future work — not implemented

- UI-controlled per-game reversible optimization sessions.
- GPU-busy versus frame-time analysis to inform GPU/CPU bottleneck hypothesis.
- Tested hardware-specific power profiles with AC/battery and thermal guardrails.
- Driver- and build-specific validation; automated regression tests on real hardware.

## References

- [Microsoft: Multimedia Class Scheduler Service](https://learn.microsoft.com/windows/win32/procthread/multimedia-class-scheduler-service)
- [Microsoft: Win32_VideoController](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-videocontroller)
- [PresentMon](https://github.com/GameTechDev/PresentMon)
