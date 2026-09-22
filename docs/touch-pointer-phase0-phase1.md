# Touch-screen wheel input: Phase 0 and Phase 1

## Current input architecture

- `App` owns `MouseHook`, `KeyboardHook`, `GestureController`, and the tray. The hooks are started before `GestureController` is created.
- `MouseHook` uses `WH_MOUSE_LL` for mouse buttons, movement, and wheel events. `KeyboardHook` handles keyboard triggers. `GestureController` subscribes to those events, manages the wheel interaction, and shows `RadialWindow`.
- Touch-screen contacts and pen contacts have no dedicated provider in the current runtime. Mouse promotion is insufficient for identifying fingers versus an active pen.

## Windows API feasibility finding

`WM_POINTERDOWN/UPDATE/UP` is delivered to the window targeted by the contact. A normal click-through window is not the contact's target, so a fully transparent, click-through overlay cannot both leave another application interactive and observe its complete pointer stream. `RegisterPointerInputTarget` can redirect all input of a pointer type, but requires UIAccess, allows only one target per desktop and type, and redirects that input away from its ordinary target. It therefore does not satisfy the proposed "global observation while all applications retain normal touch/pen input" contract.

Do not connect a local-window pointer probe to `GestureController` or claim global support until an input source with the required delivery and pass-through semantics has been validated on target hardware. The proposed 150 ms hold, distance band, synchronized motion, and pen cooldown are candidates for later recognition tests, not proven palm-rejection thresholds.

## Phase 1 diagnostic

Build Release, then run `StarPie.exe --touch-probe` on a Windows touch-screen device. A separate instance may be used while the normal StarPie process is running. The probe starts without mouse/keyboard hooks, tray, gesture controller, or wheel. Touch or write **inside the probe window**. For a two-finger check, hold one finger down, place a second finger without lifting the first, and look for `Touch contacts=2` with two distinct IDs (the live interface currently uses the Chinese label `当前按下的 Touch 触点数=2`). Its status line and normal StarPie log record pointer type, ID, phase, screen pixel position, and flags. Updates are logged at most every 100 ms; the live status line shows the latest event. The probe leaves messages unhandled so normal WPF processing continues. Closing the window ends the diagnostic process.

Record whether `Touch` and `Pen` each appear, whether two simultaneous finger contacts have distinct IDs, and whether pen hover, down, and up are observed on the actual device. Also note the Windows version, device model, DPI/scaling, and monitor arrangement.

## Verification status

- `dotnet build WinPieGestures -c Release`: passed, 0 warnings and 0 errors.
- Hardware observation reported on 2026-09-20: the user saw both `Touch` and `Pen`. The local probe log contains overlapping Touch IDs (for example `3285` and `3286` both entered before either left), confirming two simultaneous contacts reached the probe window. On this device, WPF exposed those contacts through `WM_POINTERENTER/LEAVE` with the `DOWN/UP` flags in `POINTER_INFO`; the probe now counts contacts from flags rather than only message names.
- Palm rejection, behavior over Chrome/Word, and global triggering: not tested or implemented in this phase.

## Phase 2/3 local recognition experiment

The probe now also observes WPF `PreviewTouchDown/Move/Up` to obtain the movement stream that was absent from its Win32 hook log on the tested device. WPF `PreviewStylusDown/Up`, filtered to `TabletDeviceType.Stylus`, drives a pen-priority guard. The independent `TouchGestureRecognizer` accepts two contacts held for at least 150 ms, finger separation from 30 to 250 physical pixels, and at least 40 pixels of motion from each finger within 30 degrees of each other. It emits one of eight directions once per contact sequence. A third contact blocks the sequence; pen down suppresses touch, with a 500 ms cooldown after pen up. The probe displays recognition but **does not open the wheel**.

`dotnet run --project scratch/TouchRecognizerTests/TouchRecognizerTests.csproj -c Release` passed eight deterministic checks covering timing, paired motion, single-finger movement, opposing motion, third contact, one-shot behavior, and pen cooldown. The Release app build passed with zero warnings and errors. These thresholds are provisional and are not evidence of reliable palm rejection across devices.

Hardware follow-up: the user observed WPF Touch Move events, the probe log recorded `Right`, `Down`, and `Left` recognitions, and the user reported that pen-down suppressed recognition. WPF `Down` is also present in the log; its on-screen line is quickly replaced by `Move` and `Up`. This supports the local-window input path only.

## Background input feasibility probe

An optional `--raw-digitizer-probe` switch, used together with `--touch-probe`, registers HID digitizer usages 0x0D/0x04 (touchscreen), 0x0D/0x02 (integrated pen), and 0x0D/0x01 (external pen) with `RIDEV_INPUTSINK`. The diagnostic counts `WM_INPUT` messages, reads the device's HID preparsed data, and resolves data indices with `HidP_GetData`; it does not assume fixed byte offsets. It does not affect wheel behavior. A 2026-09-21 hardware check with Chrome in the foreground confirmed two active touch IDs with changing X/Y values in 64-byte touchscreen reports. The same check confirmed integrated pen reports with `inRange=true` and transitions between `tip=false` and `tip=true` in 15-byte reports. Touch logical maxima were 28800 X and 18000 Y on that device. These observations establish background report delivery and field decoding on one machine; they do not establish a reliable screen pixel transform or cross-device compatibility.

One near-simultaneous sample in the first run reported raw touch `(20314, 5611)` and a local `POINTER_INFO` screen location `(2031, 561)`, consistent with a 10:1 coordinate transform on that display. This is a single correlation, not yet a validated mapping across the screen or after display changes.

The follow-up build logged raw device handle `0x1005E`, digitizer range `(0,0)-(28801,18001)`, and display pixels `(0,0)-(2880,1800)`; a local `POINTER_INFO.sourceDevice` was also `0x1005E`. At one near-simultaneous touch the raw `(16989,5650)` matched the local pointer `(1699,565)` after scaling. Chrome foreground reports continued with two confident contacts. These observations validate a coordinate transform on this machine and the specific tested display layout, not arbitrary multi-monitor layouts or device rotations.

The next diagnostic build decodes **every** raw report, maps its contacts into the returned display rectangle, and runs the same two-finger recognizer used by the local probe. It logs the recognized direction only; it does not open the wheel. It also uses raw pen `inRange`/`tip` to block recognition and enforces complete confident contact frames. The global gesture provider and wheel connection remain pending until this recognition path is tested with pen and fingers over another foreground application.

On 2026-09-21, the all-report diagnostic recognized `Right` and `Up` while Chrome was foreground. The pen later reported `inRange=true` and `tip=true` while Chrome remained foreground. The available sampled log does not show two active touch contacts during that pen interval, so simultaneous pen-plus-two-finger suppression remains unproven. An earlier probe process was also still running and wrote interleaved log entries; a clean single-probe overlap test is needed before promotion to the runtime.

A clean single-probe retest recognized `DownRight` and `Left` with Chrome foreground. During pen contact the hardware sent sustained pen reports and occasional single-contact touch reports, with no two-contact frames observed and no recognition. Thus the device plus recognizer avoided a false trigger in this run, while software suppression of simultaneous pen-plus-two-contact reports remains a synthetic-test case.

## Opt-in wheel integration experiment

The global provider uses a hidden raw-input HWND. It feeds recognized two-finger movement to `GestureController`, which reuses `RadialWindow`, profile selection, sector hit testing, and gesture completion. It ignores mouse movement during an active touch session and does not call `SetCursorPos` for a touch wheel. Pen proximity cancels an active touch wheel. The initial `--touch-wheel-experiment` mode remains available as a visual-only diagnostic unless paired with `--touch-wheel-actions`.

The user confirmed that the experiment opened the wheel over an ordinary maximized Chrome window without moving the mouse pointer. The final integrated build enables touch by default, executes the selected action on release, and exposes a touch enable switch, pen guard switch, hold time, finger spacing, and slide distance in Settings. Invalid raw touch frames cancel the session, and the sequence stays blocked until all fingers lift. The Release build passed with zero warnings and errors, and eight deterministic recognizer checks passed. Build4 device checks and the remaining memory limitation are recorded below.

The first wheel experiment showed an intermittent display: some Chrome gestures were recognized but `CheckIsIsolated` rejected them. A later diagnostic recorded `isolated=true` with `foreground=chrome.exe`, while every other activation guard was false. The user confirmed Chrome was an ordinary maximized window, and `DisableOnFullScreen=true` in the configuration. The existing fullscreen helper compares `GetWindowRect` to monitor bounds, which can include invisible resize borders; this is a false-positive candidate. The helper now checks DWM's visible frame against the monitor work area when the raw window rectangle covers the monitor. A live comparison of maximized Chrome and F11 Chrome remains required to validate that correction.

The first DWM/work-area correction did not fix this machine because its auto-hidden taskbar makes the monitor work area equal the full monitor. A direct Win32 read of the same Chrome window gave ordinary maximized `GetWindowRect=(-7,-7)-(1446,906)`, `IsZoomed=true`, style `0x17CF0000`, and DWM visible `(0,0)-(2880,1800)` in physical pixels. F11 gave rect `(0,0)-(1440,900)` in the PowerShell process's virtualized coordinates, `IsZoomed=true`, style `0x170B0000`, and the same DWM visible bounds. The helper now treats a maximized captioned window with a small invisible border outside the monitor as ordinary maximized, including auto-hidden taskbars. The user confirmed that the updated build opens over ordinary maximized Chrome. The user does not require F11 activation; F11 suppression has not been checked in that latest build.

The first integrated build exposed a release-frame edge case. This HID device reported `ContactCount=2` while the decoded active contacts dropped to zero, so an equality check canceled the session instead of completing it. After accepting zero active contacts, the user confirmed consecutive wheel appearances. A later log also showed `ContactCount=2` with one active contact during release. The final decoder check therefore treats the reported count as an upper bound for active contacts, while still rejecting missing counts, more than ten contacts, invalid positions, and contacts without confidence. With build4 the user confirmed two consecutive selected actions executed, the original mouse wheel and its action worked, and pen proximity plus two fingers did not open the wheel. One earlier build4 gesture intentionally escaped and did not execute; a later touch gesture selected sector 0 and logged a `Ctrl+W` hotkey execution.

An idle 10-second sample after the build4 interaction showed 0% process CPU increase but a 156.5 MB working set and 147.8 MB private bytes. This exceeds the touch specification's 100 MB memory target. The sample includes the resident app and cached wheel after interaction; no before/after baseline for the new provider was recorded, so it does not attribute the memory use to touch input. This performance item remains open. F11 activation was not requested by the user and was not included in the final device check.

## Native listener memory follow-up

The first global provider used a WPF `HwndSource` for its hidden raw-input target. The isolated `scratch/TouchMemoryProbe` measured 32.7 MB working set before provider construction and 65.9 MB after registration, while managed heap increased only 0.3 MB. This pointed to WPF window/input initialization rather than the small touch state collections. The provider now registers a native Win32 hidden window class and passes its HWND to the same raw HID decoder. In the same isolated probe, working set moved from 32.6 MB to 33.4 MB after registration; after 50 create/dispose cycles it was 33.8 MB. These are isolated-process measurements, not an application-level savings claim.

The user checked the native-listener build on the touchscreen and reported consecutive two-finger wheel gestures, mouse wheel behavior, and pen suppression working. Its log contains two selected touch actions (`F5` and `Ctrl+W`). The Release build and eight recognizer checks passed. A post-interaction app sample had 0% CPU increase during 10 seconds of idle but 203.8 MB working set and 153.9 MB private bytes; this run also loaded an official system-action plugin and had several wheel interactions, so it cannot be directly compared to the earlier 156.5 MB sample. A controlled same-process startup versus post-wheel measurement remains necessary. The overall 100 MB working-set target is still unmet.

### Controlled memory result on the tested machine

The first purported touch-enabled cold-start measurement was invalid: log timestamps show three wheel gestures occurred before the sample. The subsequent controlled measurements used `touch-wheel-native-final` with no gesture before the cold-start sample, and the same source build with `--disable-touch` as a diagnostic-only override:

| Scenario | Working set | Private bytes | Idle 10 s CPU delta |
| --- | ---: | ---: | ---: |
| Normal silent start, touch enabled, no wheel | 14.3 MB | 28.5 MB | 0% of one core |
| Silent start with `--disable-touch`, no wheel | 14.3 MB | 28.3 MB | 0% of one core |
| Same touch-enabled process after one wheel and `ShowDesktop` action | 188.4 MB | 145.8 MB | 0% of one core |
| Fresh touch-enabled process after one wheel shown then canceled, no action | 172.1 MB | 138.9 MB | 0% of one core |

This isolates the large increase to the first WPF wheel presentation, not touch monitoring or the selected plugin action. A standalone probe created an empty off-screen 482×482 transparent WPF window with hardware rendering: working set rose from 37.7 MB to 132.8 MB in one run (another run reached 146.8 MB). A 1×1 transparent window still rose from 37.8 MB to 108.8 MB. Closing the window did not immediately return this working set. A 482×482 software-rendered variant reached 88.8 MB from a 38.7 MB baseline, but switching the production wheel to software rendering would violate the project's hardware-acceleration and latency requirements. These probe figures vary by run and describe WPF's native rendering footprint on this machine; they are not a benchmark for other hardware. The 100 MB post-wheel working-set target is not met by the current WPF wheel architecture. Meeting it likely requires a separate rendering architecture study rather than additional touch-decoder tuning or forced working-set trimming.

An off-screen `scratch/NativeLayeredProbe` prototype first used Win32 `WS_EX_LAYERED` and a 482×482 premultiplied BGRA DIB. Its standalone working set rose from 23.3 MB to 28.4 MB after first display. Across 240 synthetic bitmap updates, the `Marshal.Copy` plus `UpdateLayeredWindow` call took 0.09 ms median, 0.55 ms p95, and 1.37 ms maximum; process CPU time for the batch was 31 ms. This first test drew no wheel content. It established that the native layered surface itself is lightweight, not that a complete wheel can meet the rendering requirements.

### Software layered-window evaluation

The user selected evaluation of a software-rendered layered window and relaxed the hardware-acceleration requirement for this study. `scratch/NativeLayeredProbe` now has a `--wheel` mode that renders a transparent 482×482 eight-sector wheel with antialiased geometry, direction labels, center content, and selected-sector highlight using GDI+, then copies premultiplied BGRA pixels into the DIB and calls `UpdateLayeredWindow`. The saved `wheel-prototype.png` was visually inspected: transparency, eight directions, the selected sector, and center content are present. It is a simplified visual sample, not a copy of the current production styles.

Four fresh-process Release runs, each rendering and updating 240 frames off-screen, produced the following results on the tested machine:

| Measure | Observed range |
| --- | ---: |
| Working set before window creation | 24.1 MB |
| Working set after first wheel display | 36.8–37.6 MB |
| Working set after 240 frames and window destruction | 39.4–41.1 MB |
| First `UpdateLayeredWindow` and `ShowWindow` call | 1.64–3.13 ms |
| Complete frame median, including GDI+ draw, pixel copy, and window update | 1.52–2.01 ms |
| Complete frame p95 | 3.67–4.05 ms |
| Complete frame maximum | 4.79–7.70 ms |
| Process CPU time for 240 frames | 375–578 ms |

This makes software layered rendering a credible **candidate** for reducing the first-wheel memory footprint. The measured standalone process stayed below the 100 MB goal, but the full application has not been integrated or measured, so that goal is not yet achieved. The tight loop measures drawing cost without frame pacing, live pointer input, real screen presentation, other application load, or 60/120 Hz scheduling. Its first-display timing excludes GDI+ initialization and the initial drawing performed before the timer starts. It therefore cannot establish the product's under-16-ms activation target.

The production `RadialWindow` includes 4/8/12 sectors, three style renderers, action names and icons, outer sub-ring and honeycomb sub-actions, blur/shadow effects, animations, escape dimming, custom center image, DPI changes, and window-placement behavior. The prototype does not implement these. Before making software rendering the default, acceptance must include post-wheel working set below 100 MB on the tested device, first appearance below 16 ms with the full draw path timed, acceptable 60/120 Hz frame delivery and CPU use, visual comparison, multi-monitor DPI, touch and mouse action parity, pen suppression, and full-screen behavior.

### Opt-in touch-wheel trial

The software layered renderer is connected to `GestureController` for a trial using `StarPie.exe --silent --software-touch-wheel`. It shares the existing touch recognizer, sector selection, release handling, and action executor. The ordinary mouse wheel still uses `RadialWindow`. This is a process launch option; removing `--software-touch-wheel` restores the original touch wheel, without changing `config.json`. The trial uses simplified dark sectors and action-name labels, and does not reproduce configured icons, custom styles, and animations.

The main Release build passed with 0 warnings and 0 errors. An isolated integration smoke test instantiated the renderer from the built `StarPie.dll`, showed and dismissed it twice, changed highlights, and found `WindowFromPoint` at its center resolved to the underlying window. Across runs, the first complete `Present` took 40–74 ms and the second about 5–7 ms. The smoke process working set was about 76 MB before `Present` and 95–96 MB after two cycles. This process already had a WPF `Application` and assembly reflection loaded, so these numbers are not the complete StarPie application's memory usage. The first display misses the project's 16 ms latency target in this test; it is acceptable only as a trial, not as a production performance claim.

The user ran `software-touch-wheel-build` on the touchscreen and reported that two consecutive two-finger operations and the mouse wheel were normal, with acceptable simplified appearance. A subsequent read of that still-running trial process showed 61.9 MB working set, 41.5 MB private bytes, and 0 ms CPU gain over five idle seconds. This is a single post-interaction sample, with no controlled pre-interaction baseline or confirmation of exactly which renderer paths were exercised; it is not proof that all configurations remain below 100 MB. A small fix for restoring the initial highlight on a reused native window was compiled into the intermediate `software-touch-wheel-build2` and carried into build3.

`software-touch-wheel-build3` extends the software renderer to profiles with sub-actions. Its outer ring divides the parent sector by the same angular boundaries used by `GestureController.ProcessMove`; its fan positions use `RadialWindow.GetFanSlotIndex` and `GetFanSubOffsetForShape`, the same functions as `HitTestFanSubs`. Isolated rendered images for both submenu modes were visually inspected, and the Release build passed with 0 warnings/errors. This extends the trial to the user's Global profile (four sub-action slots), which previously fell back to WPF. The mouse renderer remains WPF, so using the mouse wheel may still incur the earlier WPF memory cost.

The build3 device check then confirmed two-finger gestures in Chrome and a Global-profile submenu action on the desktop. The first general response of "normal" did not itself prove a submenu selection: the log showed only `subSector=-1` for the action tried. A targeted retest selected the left-side "常用工具" parent and its "计算器" child; the log recorded `sector=4 subSector=1 escaped=False` followed by execution of `System/Calculator`, and the user confirmed Calculator opened. The running StarPie process measured 71.4 MB working set / 39.3 MB private bytes after initial gestures, then 81.3 MB / 40.2 MB after the submenu action; each 10-second idle observation added 0 ms process CPU. These are device-specific post-interaction samples, not a cold-start comparison or a guarantee across profiles and machines. The active mouse renderer is still WPF, and its later memory cost has not been isolated in this build3 run.

### Touch action latency correction

After using build3, the user reported delay specifically **after sliding and releasing, before the action took effect**. A temporary timing build split the touch completion path into state capture, window dismissal, action resolution, and action queueing. Four measured gestures spent 56.8–77.0 ms in state capture and 41.7–82.2 ms in the following dismissal segment; action resolution and queueing were effectively instantaneous. The path called `ActionExecutor.ReleaseStuckModifiers()` twice, once in `EndActiveGesture` and once in `CloseGestureWindow`. That safeguard sends synthetic key-up events for mouse/keyboard gesture recovery. Touch gestures do not hold a keyboard modifier. The touch completion and cancellation paths now skip those two calls while mouse and keyboard paths retain them.

On the same device, the `software-touch-wheel-fast-build` retest showed state capture at 0.0 ms and window dismissal at 1.5–6.2 ms; action resolution remained 0.0 ms and queueing was 0.0–5.4 ms across the recorded samples. The action executor started in the same logged millisecond or the next, and the user reported that execution felt faster. The stable trial output is `scratch/software-touch-wheel-final/StarPie.exe --silent --software-touch-wheel`; it removes temporary timing logs only, retains the measured fix, builds with 0 warnings/errors, and passes the eight recognizer checks. The user-tested fast build remains a valid trial until the next restart. In the `v1.8.0-touch.1` derivative, software rendering is the default for touch gestures; `--wpf-touch-wheel` selects the previous WPF touch renderer. Mouse gestures continue to use WPF.

`GestureController.ShowRadialUI` moves the pointer only for the mouse path when the wheel center is adjusted. Touch has an explicit completion path and leaves the physical pointer alone. Mouse and keyboard hooks retain their existing input paths. The hardware probe remains opt-in.
