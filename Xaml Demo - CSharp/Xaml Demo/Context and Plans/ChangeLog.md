# Change Log

## 2025-09-24
### Added
- Percentile realization metrics (p50/p90/p99) to SurfacePerfAggregator first-N window summaries.
- Correlation/session id support (per-surface Start returns session; stale events ignored).
- Duplicate container detection (per containerId) separated from tail (overflow) unique realizations.
- Tail (overflow) tracking: tail count, min, max, last elapsed; readiness classification (Ready, ReadyWithTail, UnstableTail, NotReady).
- Readiness summary emitted exactly at closure of first-N window; optional survey (Flush/HardStop) output.
- Container identity integration:
  - MAUI: FirstBindTracker now passes containerId (RuntimeHelpers.GetHashCode(root element)).
  - WinUI: ContainerContentChanging supplies containerId from ItemContainer (fallback to sender).
- Extended PERF log formats:
  - PERF[SurfaceRealizationStart:*]
  - PERF[SurfaceRealization:*:index]
  - PERF[SurfaceRealizationOverflowStart:*]
  - PERF[SurfaceRealizationTail:*:index]
  - PERF[SurfaceRealizationSummary:*]: phase=Readiness/Partial, readiness=*, p50/p90/p99 fields.
  - PERF[SurfaceRealizationSurvey:*]: final snapshot (partial or window closed).
### Modified
- SurfacePerfAggregator.cs (added samples array, percentile computation, session & identity tracking, tail & duplicate metrics, readiness classification logic).
- MultiVisualPerfView.xaml.cs (records containerId + session id for MAUI first binds).
- WinUIListViewShimHandler.cs (records containerId + session id for WinUI first binds).
### Pending
- Perf validation runs (2 cold launches) to capture new percentile distributions pre/post dynamic mount refactor.
- Documentation of delta metrics (Plan + ChangeLog appendix) after validation.
### Rationale
Enhances early realization measurement fidelity while isolating churn beyond readiness threshold. Provides deterministic percentile window enabling cross-run comparison and readiness semantics independent of tail volatility.
### Next Metrics
- Allocation sampling during state transitions (future Stage 4 increment).
- p99 vs tail-max drift delta (potential instability indicator).
- Overflow duplication ratio classification refinement.

#### Perf Validation Runs (Stage 4 Percentile Instrumentation)

Run 1 (Session timestamp 13:18:32)
| Surface | avg (ms) | min | p50 | p90 | p99 | max | dup | tail | readiness |
|---------|----------|-----|-----|-----|-----|-----|-----|------|-----------|
| MauiCollection | 479.00 | 30.42 | 236.19 | 1,327.26 | 1,645.71 | 1,645.71 | 0 | 0 | Ready |
| WinUIListView  | 1,439.68 | 39.05 | 1,252.57 | 2,932.36 | 3,240.92 | 3,240.92 | 0 | 0 | Ready |

Run 2 (Session timestamp 13:21:44)
| Surface | avg (ms) | min | p50 | p90 | p99 | max | dup | tail | readiness |
|---------|----------|-----|-----|-----|-----|-----|-----|------|-----------|
| MauiCollection | 500.97 | 31.93 | 242.80 | 1,399.23 | 1,706.58 | 1,706.58 | 0 | 0 | Ready |
| WinUIListView  | 1,534.16 | 39.11 | 1,330.37 | 3,335.24 | 3,642.51 | 3,642.51 | 0 | 0 | Ready |

Stability / Delta Analysis:
- Maui avg delta Run2 vs Run1: +21.97 ms (+4.6%) within target (< 550 ms threshold) ⇒ stable improvement over Stage 3 baseline (−64–66%).
- WinUI avg delta Run2 vs Run1: +94.48 ms (+6.6%) slightly above ±5% stability band; distribution shows heavier tail (p90 +402 ms, p99 +401 ms). Suggest collecting a third run to determine if tail escalation is transient (GC or background JIT) or systemic.
- Both surfaces retained readiness=Ready with zero tail overflow and zero duplicates, validating session / containerId correlation logic.
- Maui early min (≈32 ms) consistent with Run1 (≈30 ms) confirming accelerated first-container availability after dynamic mount + simplified template path.
- Recommended Actions: (1) Optional third run to confirm WinUI variance; (2) Add tail variance commentary to Plan; (3) Consider capturing allocation samples around WinUI spikes in a future increment.

Historical Baseline (Stage 3):
- Maui avg 1,396.00 ms vs Stage 4 Run2 500.97 ms (−64.1%).
- WinUI avg 1,422.88 ms vs Stage 4 Run2 1,534.16 ms (+7.8%) with reduced max (3,782.93 → 3,642.51 ms) indicating persistent tail but marginally lower absolute ceiling in second sample.

Readiness Interpretation:
- Absence of tail overflow lines (no PERF[SurfaceRealizationTail:*]) confirms clean closure at target N for both runs.
- p99 closely tracks max for each surface, indicating limited post-window volatility; classification algorithm remains appropriate.

Next (Planned):
- Optional Run 3; if WinUI avg remains >1,520 ms, open investigation task (container recycling pacing, potential batching).

## 2025-09-23
### Added
- SurfacePerfAggregator (first-N realization metrics per surface).
- PerfConfig (FirstRealizationSampleCount configurable).
- MAUI CollectionView realization instrumentation via FirstBindTracker.
- WinUI ListView realization instrumentation via ContainerContentChanging.
- IPerfTimer integration for SwapColors, ChangeVisualState, ToggleSelectAll (HashSet), ToggleSelectAllViaProperty.
- Dispatcher override wiring in MauiProgram (LogRouter.DispatcherOverride).
- WinUIListViewShimHandler realization logging & summary.
- MultiVisualPerfView instrumentation hooks.

### Modified
- MultiVisualPerfView.xaml.cs (added perf timers, realization subscription).
- WinUIListViewShimHandler.cs (added ContainerContentChanging hook, lifecycle flush).
- MauiProgram.cs (dispatcher override provisioning).
- Plan - Abstraction Prototype.md (pending update for instrumentation section & checklist—see next commit).
- Added StopwatchPerfTimer usages replacing prior LogHub timer calls in bulk ops.
  
### Completed (Stage 3 Finalization)
- Documentation section finalized in Plan (hybrid selection + perf narrative + instrumentation summary).
- Stage 3 finalized (commit planned / executed: "Stage3: surface perf instrumentation + dispatcher finalize"); Plan checklist updated (all Stage 3 items checked).
- Instrumentation summary (first 50 realization metrics per surface) added to Plan; cross-reference maintained here for historical continuity.

### Rationale
Introduces concrete measurable surface abstraction benefits by comparing first-realization latency across MAUI and WinUI surfaces while unifying timing infrastructure under Core logging + dispatcher abstraction.

### Metrics (Windows Validation Run)
Captured first 50 item realizations per surface (target=50):

- MauiCollection: avg=1,396.00 ms, min=267.17 ms, max=2,768.64 ms, count=50
- WinUIListView: avg=1,422.88 ms, min=31.58 ms, max=3,782.93 ms, count=50

Sample early realization deltas (WinUI first 10: 31.58–109.48 ms; MAUI first 10: 267.17–396.28 ms) illustrate earlier initial container availability on WinUI with later tail latency variance, while MAUI exhibits higher initial cost but narrower tail spread until larger spikes post item ~40.

### Maintenance
- Legacy duplicate stubs physically removed (ViewModels & FrameworkSurfaceKind) after Core migration.
- Added CompositeLogSink + FileLogSink (session-log.txt) for durable capture of PERF and lifecycle lines.
- Windows target validation run performed; PERF summaries persisted to session-log.txt for documentation ingestion.

## 2025-09-17

## 2025-09-17
### Added
- New visual state `SelectableByProperty` in `MultiVisualPerfView.xaml`.
- `PerfItemTemplateByProp` DataTemplate using per-item `Selected` property.
- `SelectionToTemplateConverter` (boolean -> DataTemplate).
- `TemplateHost` control for dynamic DataTemplate realization.
- New bulk toggle button: "Toggle Select All (ItemViewModel property)" plus handler `OnToggleSelectAllViaProperty`.
- Extended visual state cycle: Normal → Highlighted → Selectable → SelectableByProperty → Normal.

### Modified
- `MultiVisualPerfView.xaml.cs` updated to include new state cycle and property-based bulk select logic.
- Plan document (`Plan - Xaml Demonstration.md`) activated and expanded with rationale, comparative analysis, and next steps.

### Rationale
Introduces an alternate selection abstraction (per-item property + converter) beside existing collection-level HashSet + DataTemplateSelector approach to illustrate layering choices and performance considerations.

### Pending / Next
- Gather comparative timing metrics (bulk select, state transitions, realization) across selection strategies.
- Optional granular instrumentation (allocation / template creation counts).
- Potential hybrid selection strategy abstraction.

## 2025-09-17 (Stage 3 Surface Abstraction Progress)
### Added
- Hybrid selection synchronization (HashSet membership ↔ per-item `Selected` property) with recursion suppression in `MultiVisualPerfViewModel`.
- Lifecycle instrumentation:
  - Introduced `SurfaceLifecycleState` enum and `SurfaceLifecycle` logging helpers.
  - Added lifecycle state + transition logging to `ExistingMauiViewSurface`, `WinUIListViewSurface`, and `MauiCollectionSurface`.
- Performance timing infrastructure: `StopwatchPerfTimer` (implements `IPerfTimer`) with automatic `PERF[...]` log output.
- Logging improvements: `LogRouter` now optionally marshals to UI dispatcher (`EnableDispatcherMarshaling`, `DispatcherOverride`).
- Extended `IRenderSurface` with `State` property (lifecycle awareness).

### Modified
- Updated surface implementations to track and log state transitions (`Constructed → Initializing → Initialized`).
- `MultiVisualPerfViewModel` now subscribes to per-item `Selected` changes and maintains authoritative HashSet in sync.
- Plan document (Abstraction Prototype) updated with new remaining items and implemented features.

### Pending / Next
- Integrate `IPerfTimer` into surface realization / first-bind pathways (capture first N item materializations per surface).
- Add per-surface realization metrics aggregation (count, elapsed total, average) and emit summarized log lines.
- Remove obsolete duplicate ViewModel and enum files in MAUI project; adjust project file.
- Convert static left `CollectionView` to dynamic host using `MauiCollectionSurface`.
- Dispatcher-based LogRouter validation on Windows target build.
- ChangeLog entry for final Stage 3 commit (`"Stage3: surface sync + lifecycle + perf instrumentation"`).
