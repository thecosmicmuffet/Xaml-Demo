# Change Log

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
  
### Pending
- Physical deletion of legacy stub ViewModel/enum files (currently excluded in csproj).
- Documentation section finalization in Plan (hybrid selection + perf narrative expansion).
- Windows target validation run logs capture & summary.
- Commit final Stage 3 message: "Stage3: surface perf instrumentation + dispatcher finalize".

### Rationale
Introduces concrete measurable surface abstraction benefits by comparing first-realization latency across MAUI and WinUI surfaces while unifying timing infrastructure under Core logging + dispatcher abstraction.

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
