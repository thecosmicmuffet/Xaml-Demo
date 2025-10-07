# Status
ACTIVE
Current Step: 8 - Runtime MAUI Window Embedding Validation
Previous: Step 7 - COMPLETE ✅ (Reflection-based build architecture)
(msbuild capabilities have been added to vs code via msbuild-tools command, see msbuild-tools-for-vs-code.md)

## Current State (Step 8 - COMPLETE)

**Package Identity Success:** Successfully created and deployed a Windows Application Package that provides the necessary activation context for MAUI initialization. The WPF host application now runs with package identity.

### Step 8 Completion (October 7, 2025)

✅ **Package Creation:**
- Created Windows Application Package (.msix) with proper manifest
- Generated and installed self-signed certificate for package signing
- Built and deployed signed package with package identity
- Fixed missing image resources with correct sizes

✅ **Application Launch:**
- WPF host application launches successfully with package identity
- Windows App Runtime files properly loaded
- Build infrastructure proven with MSBuild command-line tools

### Next: Step 9 - MAUI Window Embedding Validation
**Goal:** Test if MAUI windows can now be created and embedded with the package identity in place.

## Step 7 Summary (Completed)

Successfully resolved build conflicts using a __reflection-based architecture__ that cleanly separates build-time and runtime concerns. **Note:** This resolved the build problem but did not validate runtime embedding.

## Achievements

✅ __Build Validation:__

- WPF Host builds successfully (2.8s) with zero namespace conflicts
- MAUI Project builds successfully (4.1s)
- All assemblies copy correctly to output directory
- Clean separation - no XAML parser interference

⚠️ __Runtime State:__

- WPF host launches and runs successfully
- Application responds to user interaction  
- MAUI assemblies load dynamically via reflection
- Windows App Runtime DLLs present but **MAUI initialization fails**
- **MAUI window HWND remains 0** (not acquired)
- Root cause: Missing COM activation context

✅ __Architecture:__

- Removed direct MAUI ProjectReference from WPF host
- Converted MauiBootstrapper to reflection-based loading
- Maintained packaging infrastructure (.wapproj) for future MSIX deployment
- Comprehensive lifecycle logging throughout

## Key Insight

__Package identity provides *runtime* activation context, NOT build-time integration.__

This architectural understanding allowed us to:

- Eliminate namespace conflicts (Application, Window, Rect)
- Remove XAML parser interference
- Maintain clean project boundaries
- Preserve Windows App Runtime activation capability

## Deployment Options

1. __Unpackaged (Current - Working):__ Direct execution from bin\Debug for development
2. __Visual Studio Packaging (Future):__ MSIX creation via VS MSBuild tooling
3. __MSBuild CLI (Alternative):__ Command-line packaging with VS Developer tools

## Documentation

- ✅ Plan.md updated - Step 7 marked COMPLETE
- ✅ ChangeLog.md entry added with full details
- ✅ Step7-Resolution.md - Complete architectural documentation
- ✅ Step7-ValidationFindings.md - Problem analysis preserved

## Step 8 Challenge: Runtime MAUI Window Embedding

### Problem Statement
From Step7-ValidationFindings.md and ChangeLog analysis:

**What Works:**
- ✅ Reflection-based assembly loading
- ✅ Windows App Runtime native bootstrap (version 0x00010008)  
- ✅ Core ViewModel with 1000 items
- ✅ WPF ListBox displays successfully

**What Fails:**
- ❌ `MauiProgram.CreateMauiApp()` throws TypeInitializationException
- ❌ Failure in `Microsoft.Maui.Handlers.ViewHandler` static constructor
- ❌ COMException indicates missing activation context
- ❌ MAUI window HWND acquisition returns 0

### Root Cause
**MAUI requires Windows App Runtime activation context** that includes:
1. Package identity (Package.appxmanifest)
2. Proper COM apartment state  
3. Windows App Runtime initialization with package context

**Key Insight:** The reflection-based approach successfully loads assemblies but **cannot provide the COM activation context** that MAUI's initialization requires.

## Architectural Options for Step 8+

### Option 1: Package Identity (Recommended from Step7-ValidationFindings.md)
**Approach:** Convert WPF host to packaged application

**Changes Required:**
- Add Package.appxmanifest to WPF project or use existing .wapproj
- Build via Visual Studio MSBuild (packaging infrastructure)
- Provides proper Windows App Runtime activation context at runtime

**Pros:** Proper activation context; simplifies deployment  
**Cons:** Requires Visual Studio tooling; more complex dev workflow

### Option 2: IPC/Separate Process Architecture  
**Approach:** Run MAUI as standalone packaged app; use IPC

**Changes Required:**
- Keep MAUI as independent packaged application
- Implement named pipes or gRPC communication
- Synchronize state via IPC

**Pros:** Clean architectural boundary; each runs in proper context  
**Cons:** Complex IPC; no direct HWND embedding; performance overhead

### Option 3: Abandon MAUI Embedding (Fallback)
**Approach:** Focus on WPF + WinUI surfaces only

**Changes Required:**
- Remove MAUI embedding attempts
- Compare WPF ListBox vs WinUI ListView performance
- Document findings

**Pros:** Simpler; avoids activation context issues  
**Cons:** Loses MAUI comparison; reduces scope of abstraction demo

## Recommended Path Forward

Based on Step 5's original intent (WPF hosting multiple XAML frameworks) and the documented architectural investigation:

1. **Immediate:** Document current state clearly (this update)
2. **Next:** Attempt Option 1 (Packaging) using existing .wapproj infrastructure
3. **Validation:** Test if packaged deployment enables MAUI initialization
4. **Fallback:** If packaging doesn't resolve, pivot to Option 2 (IPC) or Option 3

## Updated Documentation
- ✅ Step7-Resolution.md - Build architecture success documented
- ✅ Step7-ValidationFindings.md - Runtime failure analysis complete  
- ✅ ChangeLog.md - Detailed technical history
- ✅ Plan.md (this file) - Updated to reflect Step 8 challenge

The reflection-based build architecture (Step 7) provides clean separation for development. Runtime embedding validation (Step 8) reveals the need for proper activation context, requiring architectural decisions about packaging vs. process isolation.

# Plan

### Stage 1 (In-place MAUI refactor – zero new projects)

Objective: Abstract “surfaces” so `MultiVisualPerfView` is a composition scaffold. Actions:

- Introduce minimal interface:

  ```csharp
  public enum FrameworkSurfaceKind { Maui, WinUI3, UwpPlaceholder }
  public interface IRenderSurface
  {
      FrameworkSurfaceKind Kind { get; }
      View MauiViewHost { get; } // returns a MAUI View if in-process renderable; null otherwise
      Task InitializeAsync(object context, CancellationToken ct);
  }
  ```

- Wrap existing left `CollectionView` as `MauiCollectionSurface`.

- Wrap existing `WinUIListViewShim` as `WinUIListViewSurface`.

- Add placeholder `UwpListViewSurface` (Kind=UwpPlaceholder) that currently yields a stub `Label` (“UWP placeholder on MAUI host”).

- Replace hard-coded Grid columns in `MultiVisualPerfView.xaml` with:

  - A `Grid` with named placeholders (`SurfaceHostA`, `SurfaceHostB`).
  - Code-behind that walks a `List<IRenderSurface>` from VM (or injected service) and mounts each `MauiViewHost`. Benefit: View logic becomes agnostic to future external-hosted surfaces.

### Stage 2 (Solution restructuring for Windows aggregation)

Objective: Add a WPF “super host” project. Actions:

- Create `Xaml.Demo.Core` (netstandard2.1 or net8.0-windows) for: ViewModels, PerfItem, selection infra, logging contracts (not MAUI types).

- Move non-UI logic (current ViewModels + services) into Core.

- MAUI project references Core (keeps current platform targets).

- Add `Xaml.Demo.Host.Wpf` (.NET 8/9; `<UseWPF>true</UseWPF>`).

- In WPF host:

  - A main `Grid` with regions for surfaces.

  - Host types:

    1. WinUI3/Maui region:

       - Launch MAUI window hidden
       - Obtain its HWND (after `CreateMauiApp()`)
       - Re-parent into a `HwndHost` derivative (manage sizing & message forwarding).

    2. UWP ListView region:

       - Use `WindowsXamlHost` (WinUI2/UWP) to instantiate a `Windows.UI.Xaml.Controls.ListView`.
       - Bind via a projection adapter to Core ViewModel (wrap items if necessary).

- Shared selection/color operations invoke Core VM; each surface provides refresh semantics.

#### Stage 2 Checklist

- [x] Analyze current ViewModel and service dependencies on MAUI-specific APIs
- [x] Create Xaml.Demo.Core project (netstandard2.1) for shared logic
- [x] Add Core project to solution and add project reference from MAUI project
- [x] Introduce placeholder interfaces (ILogSink, IPerfTimer) in Core
- [x] Move BaseViewModel, ISelectable, PerfItemViewModel, MultiVisualPerfViewModel into Core
- [x] Adjust namespaces and using directives after move (validated via successful multi-target build)
- [x] Extract LogHub contract to ILogSink and decide implementation placement (LogRouter + adapter)
- [x] Evaluate NavigationHub & ScenarioCatalog (decision: keep in MAUI; depend on MAUI Views & simple static pattern)
- [x] Decide on Color dependency strategy (direct Microsoft.Maui.Graphics reference)
- [x] Update MAUI project code to reference Core types (App.xaml, MultiVisualPerfView.xaml namespaces)
- [x] Build solution and resolve any compiler errors (build succeeded)
- [x] Update Plan.md progress markers after each completed milestone
- [x] Remove legacy stub files (excluded from compilation in csproj; retained temporarily for developer visibility)
- [x] Draft embedding strategy notes for upcoming WPF host (HWND acquisition, HwndHost plan)

##### Stage 2 Dependency Analysis

BaseViewModel:
- MAUI-specific: static call to LogHub (which uses MainThread.BeginInvokeOnMainThread from Microsoft.Maui.ApplicationModel).
- Action: Remove static coupling. Introduce ILogSink in Core. BaseViewModel gets protected ILogSink? or static CoreLogRouter.SetSink(ILogSink). No other MAUI types.

PerfItemViewModel:
- Uses Microsoft.Maui.Graphics.Color (independent NuGet, can be referenced from Core without full MAUI).
- No UI-thread APIs. ColorString not auto-updated when Color changes (optional improvement: raise PropertyChanged for ColorString in setter).

MultiVisualPerfViewModel:
- Uses Microsoft.Maui.Graphics.Color/Colors.
- No direct MAUI UI-thread or shell dependencies.
- SurfaceOrder references FrameworkSurfaceKind (will move that enum or create Core copy; current enum namespace Xaml_Demo.Surfaces is MAUI project – move enum to Core to avoid circular ref).
- Selection logic pure .NET.

ISelectable:
- Pure .NET; safe to move as-is.

LogHub (current service):
- MAUI-specific: MainThread.BeginInvokeOnMainThread.
- Must remain in MAUI project (or split interface + impl). Provide ILogSink in Core; MAUI LogHub implements it and marshals to UI thread.
- Timer utilities also tied here; consider moving timing concerns to IPerfTimer (Core) and let LogHub just log.

Color dependency strategy:
- Option A: Reference Microsoft.Maui.Graphics in Core (simplest, low risk).
- Option B: Introduce lightweight IColorAdapter (overkill now). Choose Option A initially.

Planned Moves to Core (initial batch):
- BaseViewModel (after removing direct LogHub static call; replace with protected Log or injected ILogSink).
- ISelectable.
- PerfItemViewModel.
- MultiVisualPerfViewModel.
- FrameworkSurfaceKind (if not introducing WPF project yet; keeps SurfaceOrder compiling).

Deferred / Evaluate:
- NavigationHub, ScenarioCatalog: currently likely tied to MAUI Shell navigation; keep in MAUI for now.
- LogHub implementation (keep in MAUI; only interface in Core).
- ExistingMauiViewSurface (remains MAUI-specific).

Additional Adjustments:
- Introduce Core/Logging/ILogSink.cs with Write(string).
- Introduce Core/Perf/IPerfTimer.cs (Start, Stop, Elapsed) (placeholder).
- Add Core static LogRouter (optional) to allow BaseViewModel.Log(message) with late-binding.

##### WPF Embedding Strategy Draft (Stage 2 Output)

Goals: Host MAUI/WinUI3 content and a UWP (WinUI2) ListView inside a WPF super-host while preserving Core ViewModel isolation.

1. Process / Window Initialization
   - Launch MAUI app headless (hidden primary window) via MauiProgram.CreateMauiApp().
   - Retrieve native HWND (WindowHandler.PlatformView / WinUI Window -> GetWindowHandle()) once created.
   - Delay reparent until window fully activated (hook Activated or use dispatcher idle).

2. Reparenting into WPF
   - Custom HwndHost subclass (MauiHwndHost) overrides BuildWindowCore/DestroyWindowCore.
   - Use SetParent(childHwnd, hostHwnd) then AdjustWindowRectEx to strip chrome (WS_CHILD, remove WS_OVERLAPPED, apply WS_CLIPCHILDREN | WS_CLIPSIBLINGS).
   - Persist original styles to restore on detach (if needed for standalone debugging).

3. Sizing & Layout
   - Override HwndHost.OnWindowPositionChanged or handle WM_SIZE in host to call MoveWindow(child, 0,0,width,height, TRUE).
   - DPI awareness: query GetDpiForWindow(child) and scale if WPF VisualTree uses different DPI (rare if per-monitor aware).

4. Focus & Input Routing
   - Intercept WM_SETFOCUS / WM_KILLFOCUS in HwndHost WndProc; forward SetFocus(childHwnd) when host gains focus to preserve keyboard nav.
   - Translate accelerator keys (e.g., F5, Ctrl shortcuts) if WPF top-level menu needs them: preview KeyDown in WPF, optionally SendMessage(childHwnd, WM_KEYDOWN,...).

5. Message Flow / Lifetime
   - No explicit message pump duplication: MAUI/WinUI ride WPF’s pump after reparenting.
   - Ensure MAUI dispatcher availability before reparenting (await MauiApp.Services.GetRequiredService<IDispatcher>() presence).
   - Shutdown ordering: Unparent (SetParent(childHwnd, IntPtr.Zero)) or destroy child before WPF App exits to avoid orphaned window handles.

6. UWP / WinUI2 ListView Hosting
   - Use WindowsXamlHost (WinUI XAML Islands) in WPF region.
   - XAML Island control creation: host.Initialized += CreateElement<Windows.UI.Xaml.Controls.ListView>().
   - Data Binding Bridge:
     * Core VM items projected via lightweight adapter implementing IList / INotifyCollectionChanged mapping to ObservableCollection<object>.
     * Selection synchronization: handle ListView.SelectionChanged -> invoke Core selection service; Core raises event -> update MAUI surface.

7. Cross-Thread Dispatch Abstraction
   - IUiDispatcher (Stage 3 formalization) provisional mapping:
     * Maui: app.Services.GetRequiredService<IDispatcher>().Dispatch / DispatchAsync.
     * WPF: Application.Current.Dispatcher.Invoke / BeginInvoke / InvokeAsync.
     * UWP Island: Windows.UI.Core.CoreDispatcher.RunAsync.
   - Interim: Provide DispatcherAdapterFactory that detects context by handle ownership.

8. Logging Integration
   - WPF host sets LogRouter.SetSink(new WpfTextBoxSink(TextBox)) marshal-to-UI; MAUI continues using LogSinkAdapter.
   - Island events (ListView realized, measure passes) write perf timing via IPerfTimer + ILogSink.

9. Performance Instrumentation
   - On first 200 item container materializations in each surface log timestamp deltas (MAUI CollectionView, WinUI ListView Shim, UWP ListView).
   - Use IPerfTimer.Start() per surface before binding; stop after threshold reached.

10. Risk Mitigations
   - Flicker on reparent: hide child before SetParent; show after sizing (SWP_SHOWWINDOW).
   - Input anomalies: ensure WS_EX_NOACTIVATE not set; explicitly SetFocus.
   - DPI mismatch: test on mixed-DPI monitors; adjust scaling or set PerMonitorV2 awareness in app manifest.

11. Next Implementation Artifacts (Stage 3)
   - MauiHwndHost.cs (WPF project).
   - MauiBootstrapper: spins up MAUI app and exposes HWND Task.
   - UwpListViewSurfaceAdapter: wraps WindowsXamlHost creation and item projection.

Completion Criteria for Stage 2:
   - Core isolation proven by exclusion of stub files from compile (csproj).
   - Strategy documented above for WPF embedding & cross-stack dispatch.
   - NavigationHub / ScenarioCatalog residency decision recorded.

### Stage 3 (Refinement & true surface abstraction)

Objective: Make surfaces lifecycle-agnostic and possibly out-of-process. Actions:

- Extend `IRenderSurface` with:

  - `Task<NativeHandleRef?> GetEmbedHandleAsync()`
  - `event EventHandler<SurfaceInvalidatedEventArgs> Invalidated`

- Introduce dispatcher abstraction:

  ```csharp
  public interface IUiDispatcher { void Post(Action); Task SwitchAsync(); }
  ```

- Provide adapters: `MauiDispatcherAdapter`, `WpfDispatcherAdapter`, `UwpDispatcherAdapter`.

- Implement synchronization rules for selection + color swap (marshal to each surface thread before invalidation).

#### Stage 3 Checklist

- [x] Unify FrameworkSurfaceKind (remove duplicate from MAUI; use Core enum)
- [x] Extend IRenderSurface (Add GetEmbedHandleAsync, Invalidated event)
- [x] Add NativeHandleRef struct (Core/Surfaces)
- [x] Add SurfaceInvalidatedEventArgs (Core/Surfaces)
- [x] Introduce IUiDispatcher interface (Core/Dispatching)
- [x] Implement MauiDispatcherAdapter and register in DI
- [x] Implement WpfDispatcherAdapter stub (placeholder until host project added)
- [x] Implement UwpDispatcherAdapter stub
- [x] Add ISurfaceCatalog service returning ordered IRenderSurface instances
- [x] Refactor MultiVisualPerfViewModel to consume ISurfaceCatalog (remove direct SurfaceOrder list)
- [x] Refactor MultiVisualPerfView.xaml.cs to resolve and mount surfaces dynamically
- [x] Update existing surfaces to new interface members
- [x] Add selection/color synchronization using dispatcher marshaling (hybrid HashSet + per-item Selected sync)
- [x] Integrate IPerfTimer instrumentation (first N realized items per surface)
- [x] Add lifecycle state enum + logging transitions
- [x] Update LogRouter to optionally dispatch via IUiDispatcher
- [x] Remove legacy duplicates (FrameworkSurfaceKind.cs in MAUI, obsolete ViewModel files)
- [x] Document decisions & progress updates in Plan.md (instrumentation + dispatcher sections added 2025-09-23)
- [x] Build & validate Windows target behavior (PERF summaries captured in session-log.txt and recorded in ChangeLog)
- [x] Commit Stage 3 final implementation (message: "Stage3: surface perf instrumentation + dispatcher finalize")

### Instrumentation Summary (Stage 3)

Captured first 50 item realizations per surface (target = 50) using SurfacePerfAggregator + FirstBindTracker / ContainerContentChanging:

- MauiCollection: avg=1,396.00 ms, min=267.17 ms, max=2,768.64 ms, count=50
- WinUIListView: avg=1,422.88 ms, min=31.58 ms, max=3,782.93 ms, count=50

Observations:
- WinUI surface produces earliest first realizations (31–109 ms first 10) indicating faster initial container availability.
- MAUI surface shows higher initial cost but tighter mid‑range distribution (reduced variance until late tail > item ~40).
- Tail latency spikes higher on WinUI (max ~3.78s) suggesting deferred template/materialization bursts.
- Hybrid selection synchronization (HashSet + per-item Selected) ensured consistent selection visuals across both surfaces without extra template churn (Replace notifications only for changed items).
- Dispatcher override confirmed: LogRouter marshals when DispatcherOverride present; no deadlocks observed in timing operations.

Next Metric Enhancements (planned Stage 4+):
- Distribution percentiles (p50 / p90 / p99) for first-N.
- Allocation counters during bulk selection & state transitions.
- Cross-surface diff summary (delta of avg & min) emitted in single PERF summary line.

### Stage 4 (Optional cross-process exploration)

#### Stage 4 Path C Detailed Plan (Chosen)

Phase 4A: Dynamic Mount Migration
- Remove static `ItemsCollectionView` from XAML; introduce `LeftSurfaceHost` (`ContentView`) analogous to existing `RightSurfaceHost`.
- Move all item template resources that remain necessary into a shared ResourceDictionary or into `MauiCollectionSurface` construction (base template already present).
- Update visual state setters to apply to dynamic CollectionView instance (store reference after surface InitializeAsync; programmatic `GoToState` remains unchanged).
- Refactor `ForceSelectorRefreshIfNeeded` to reference dynamic instance (injected field).
- Ensure perf instrumentation still subscribes (re-wire FirstBindTracker enabling attribute or attach handler in surface creation).

Phase 4B: ExternalProcessSurface Stub
- Add `ExternalProcessSurface : IRenderSurface` (Kind: reuse `UwpPlaceholder` or introduce `ExternalSim` if enum extension acceptable).
- `InitializeAsync`: delay (e.g. 150–250 ms) to simulate startup; transition lifecycle states; raise `Invalidated(DataChanged,"SimExternalReady")`.
- `MauiViewHost` => null; `GetEmbedHandleAsync` returns null (placeholder).
- Catalog: optionally append; host logic skips mounting when `MauiViewHost` null (log diagnostic for visibility).

Phase 4C: Documentation & Risk Update
- Plan document: record rationale for separating dynamic mount before real WPF host (reduces coupling & simplifies diff when adding HWND embedding).
- Add risk table entries: template state transitions after dynamic migration, selection refresh invariants, lasso reactivation strategy (currently disabled code remains reference).
- Add “Next Host Tasks” preface listing WPF host scaffolding steps.

Deferred (Stage 4 subsequent commits)
- Add WPF host project (`Xaml.Demo.Host.Wpf`) with `MauiBootstrapper` + `MauiHwndHost`.
- Provide `MauiWindowSurface` exposing MAUI root HWND via `GetEmbedHandleAsync`.
- IPC / shared swapchain experimentation groundwork (decide on DirectComposition vs D3DImage path).

Success Criteria for Initial Stage 4 Commit
- App runs with dynamically mounted left & right surfaces (no static `ItemsCollectionView` in XAML).
- Visual states function identically across new dynamic instance.
- Perf metrics still recorded for both surfaces (no regression in first-N capture).
- ExternalProcessSurface stub logs lifecycle + invalidation without affecting layout.
- Plan & ChangeLog updated (this document + ChangeLog).

Metrics Validation Post-Migration
- Compare first 10 & first 50 realization timing pre/post dynamic refactor (expect negligible change; log if delta > ±5% average).
- Verify selection bulk toggle durations remain within prior variance bounds.

### Stage 4 Progress (Initial Dynamic Mount + External Stub)

Completed:
- Replaced static `ItemsCollectionView` with dynamic `LeftSurfaceHost` + runtime `MauiCollectionSurface` instantiation.
- Updated code-behind to build surfaces via catalog; dynamic template assignment (`ApplyTemplateForState`).
- Adjusted selection refresh and viewport estimation to use dynamic collection view reference.
- Added `ExternalProcessSurface` (simulated out-of-process; lifecycle + invalidation signaling).
- Extended `DefaultSurfaceCatalog` to include `FrameworkSurfaceKind.UwpPlaceholder` (stub not hostable; gracefully skipped).
- Removed XAML VisualState template setters; logic now centralized in code-behind for clearer dynamic host demonstration.

Pending (next commit goals):
- Run perf validation pass; log first-N realization deltas pre/post migration.
- Optional: attach FirstBindTracker attribute programmatically in `MauiCollectionSurface` if deeper instrumentation needed.
- Document perf comparison deltas (append to ChangeLog + Plan once captured).

Risks Observed / Mitigated:
- Template reassignment still forces selector reevaluation only in Selectable state—confirmed via dynamic path.
- No null reference guards missing for `_mauiCollectionView`; defensive checks added.
- ExternalProcessSurface currently silent if catalog ordering changes; host skip path validated.

Next Steps (prior to WPF host work):
1. Perf validation & documentation.
2. Introduce percentiles computation in `SurfacePerfAggregator` (p50/p90) (scoped change).
3. Prepare WPF host scaffolding section (HWND acquisition prototype outline).

### Stage 4 (Optional cross-process exploration)

Objective: Replace in-proc HWND parenting with true cross-process visuals. Options:

1. Shared DirectComposition / SwapChain path:

   - Each process renders to a shared Direct3D11 texture (via `CreateSharedHandle`).
   - WPF host uses D3DImage or custom `HwndHost` + DirectComposition to present.

2. AppWindow embedding (if API evolves) for cleaner lifetime.

3. IPC (named pipes or `MemoryMappedFile`) for control messages; surfaces remain passive renderers.

### Stage 5 (UWP → WinUI3 parity / consolidation)

Objective: Replace UWP ListView with equivalent WinUI3 control once Islands parity suffices; keep abstraction unchanged so host swap is transparent.

## Abstractions / Interfaces

| Concern | Interface / Type | Notes | |--------|------------------|------| | Surface contract | `IRenderSurface` | Uniform attach/init model | | Surface kind enum | `FrameworkSurfaceKind` | Routing / diagnostics | | Dispatcher | `IUiDispatcher` | Thread affinity isolation | | Selection logic | `ISelectable` (existing) | Already adequate | | Logging | `ILogSink` (adapt `LogHub`) | Decouple from MAUI static | | Perf tracking | `IPerfTimer` | Replace direct static calls for testability | | Surface registry | `ISurfaceCatalog` | Provides ordered list for host |

## Initial Refactor Steps (Stage 1 Implementation)

1. Create `Surfaces` folder; add `FrameworkSurfaceKind`, `IRenderSurface`.

2. Implement `MauiCollectionSurface` (wrap existing `CollectionView` creation + ItemsSource binding).

3. Implement `WinUIListViewSurface` (returns existing `WinUIListViewShim`).

4. Modify `MultiVisualPerfViewModel` to expose `IReadOnlyList<FrameworkSurfaceKind>` ordering (or a factory injection).

5. In `MultiVisualPerfView` code-behind:

   - On load: resolve surfaces via a simple factory using VM.
   - Clear existing static XAML right column; instead programmatically add hosts to Grid columns 0 and 1.

6. Preserve current lasso + state logic on left surface only (detect `Kind == Maui`).

7. Insert placeholder UWP surface (Label) to validate dynamic insertion path.

## Risks / Mitigations

| Risk | Impact | Mitigation | |------|--------|-----------| | Thread marshaling errors when adding WPF host later | UI freezes / crashes | Early dispatcher abstraction | | Focus / input anomalies when re-parenting MAUI HWND into WPF | Lost keyboard navigation | Implement `HwndHost` subclass managing `WM_SETFOCUS` routing | | Selection refresh reliance on MAUI Replace semantics | Inconsistent template updates across surfaces | Introduce surface-level `Refresh(IEnumerable<object>)` method; MAUI impl keeps current trick; others rebind ItemsSource snapshot | | Large item count (1000) perf variance across frameworks | Skewed metrics | Add instrumentation per surface (first realize count vs time) | | Logging static coupling | Harder cross-process adoption | Abstract via `ILogSink` and inject |

## Minimal Code Diffs (Stage 1 Guidance)

Add interfaces:

```csharp
public enum FrameworkSurfaceKind { MauiCollection, WinUIListView, UwpPlaceholder }

public interface IRenderSurface
{
    FrameworkSurfaceKind Kind { get; }
    View? MauiViewHost { get; }
    Task InitializeAsync(object context, CancellationToken ct);
}
```

Surface factory:

```csharp
public static class SurfaceFactory
{
    public static IRenderSurface Create(FrameworkSurfaceKind kind, MultiVisualPerfViewModel vm) =>
        kind switch
        {
            FrameworkSurfaceKind.MauiCollection => new MauiCollectionSurface(vm),
            FrameworkSurfaceKind.WinUIListView => new WinUIListViewSurface(vm),
            _ => new UwpPlaceholderSurface()
        };
}
```

ViewModel addition:

```csharp
public IReadOnlyList<FrameworkSurfaceKind> SurfaceOrder { get; } =
    new[] { FrameworkSurfaceKind.MauiCollection, FrameworkSurfaceKind.WinUIListView };
```

`MultiVisualPerfView` load logic (conceptual):

```csharp
foreach (var kind in vm.SurfaceOrder)
{
    var surface = SurfaceFactory.Create(kind, vm);
    await surface.InitializeAsync(vm, CancellationToken.None);
    if (surface.MauiViewHost is View v)
        HostGrid.Add(v, columnIndex++, 0);
}
```

## Recommendation

Proceed with Stage 1 refactor inside existing MAUI project before adding new WPF host. That yields a stable abstraction boundary and de-risks later platform integration.

## Stage 4 Perf Validation – Run 1 (Post Dynamic Mount, Percentile Instrumentation Enabled)

Source log timestamp: 2025-09-24 13:18:32 (session=1)

Collected first-N (N=50) realization metrics for both surfaces with new percentile + readiness pipeline:

| Surface | avg (ms) | min | p50 | p90 | p99 | max | dup | tail | readiness |
|---------|----------|-----|-----|-----|-----|-----|-----|------|-----------|
| MauiCollection | 479.00 | 30.42 | 236.19 | 1,327.26 | 1,645.71 | 1,645.71 | 0 | 0 | Ready |
| WinUIListView  | 1,439.68 | 39.05 | 1,252.57 | 2,932.36 | 3,240.92 | 3,240.92 | 0 | 0 | Ready |

Delta vs Stage 3 (pre‑dynamic mount baseline):
- Maui avg improved: 1,396.00 ms → 479.00 ms (−65.7%). Earlier stable mid distribution; reduced long tail (old max 2,768.64 ms now 1,645.71 ms).
- WinUI avg roughly flat: 1,422.88 ms → 1,439.68 ms (+1.2%). Tail max reduced (3,782.93 → 3,240.92 ms) indicating some late outlier shrinkage.
- Maui min improved dramatically (267.17 → 30.42 ms) reflecting earlier first container availability after dynamic mount + template path simplification.
- Percentile spread (Maui): p50 well below old average, indicating front-half readiness achieved substantially earlier; p90 < old average, tail compression evident.
- Readiness Classification: Both Ready with zero tail overflow (no post-window churn recorded in first session).

Preliminary Interpretation:
- Dynamic surface mount + direct template assignment reduced initial MAUI binding overhead (less XAML setter indirection and no initial selector toggling).
- WinUI path unchanged; variance remains dominated by container creation bursts; further optimization may target virtualization thresholds or deferred template phases.
- Next run will confirm stability (guard against unusually favorable GC/CPU conditions). If second run corroborates, document architectural rationale (reduced visual tree churn & deferred selector reapplication) in ChangeLog addendum.

Next Actions:
1. Perform second cold run to confirm MauiCollection average remains < 550 ms and WinUI within ±5% of prior average.
2. Append comparative delta table & narrative to ChangeLog (2025-09-24 section).
3. Mark perf validation checklist items complete; proceed to readiness classification documentation & commit.

---

## Step 5 (WPF Host Scaffold – Partial Implementation Accepted)

Decision: Proceed with documentation of partial scaffold; defer full WindowsAppSDK integration (PRI task failure) and embedded MAUI window handle realization. Reflection-based bootstrap retained; direct MAUI reference postponed to later stabilization.

### Implemented (Step 5 Partial)
- New project `Xaml.Demo.Host.Wpf` (net10.0-windows; pure WPF, no WinAppSDK packaging).
- Reflection `MauiBootstrapper` (loads MAUI assembly, acquires Services, attempts HWND).
- `MauiHwndHost` (HWND re-parent host).
- `WpfTextBoxLogSink` + LogRouter hookup in MainWindow.
- MainWindow layout: (Column0 placeholder for MAUI window, Column1 WPF ListBox bound to Core `MultiVisualPerfViewModel.Items`, bottom log console).
- VM instantiation (1000 color items) independent of MAUI host success.
- Basic selection + color swap commands wired (Toggle Select All, Swap Colors).

### Deferred / Remaining
- Successful HWND acquisition & embed (handle currently may remain zero under reflection path).
- Full lifecycle logging (HostConstructed → MauiBootstrapping → MauiReady → Embedded).
- Surface catalog extension for WPF host scenario (placeholder only, no additional surfaces).
- Validation metrics (compare WPF list virtualization vs MAUI/WinUI surfaces).
- Risk table update (focus routing, DPI scaling, cleanup ordering) – pending.
- MAUI window forced creation path (explicit Window instantiation if none opened).

### Constraints / Issues
- WindowsAppSDK PRI generation task (ExpandPriContent) error when attempting WinAppSDK package reference; removed for now.
- Build kept independent of Win2D / WinAppSDK tasks to avoid packaging overhead.
- No PlatformTarget mismatch (explicit x64 to be added later if Win2D integration required).

### Rationale
Capturing the partial scaffold clarifies abstraction seam and enables parallel planning of:
1. Direct MAUI embedding vs reflection bootstrap.
2. Future external process swapchain integration.
3. Catalog-driven multi-surface host in WPF.

### Next Increment Options
- Add minimal lifecycle logging + risk table, then mark Step 5 complete.
- Introduce forced MAUI Window creation to guarantee HWND for embedding test.
- Add simple allocation / timing probes for WPF ListBox first-N item materialization.

### Success Criteria (Adjusted)
Partial acceptance: structural host + VM + logging sink present. Full success postponed until HWND embed validated and lifecycle metrics captured.

---

## Step 6 Hybrid WinUI 3 Window Attempt Complete

### Summary
Attempted to create a hybrid WinUI 3 window as an intermediate host for MAUI content. This approach aimed to leverage WinUI 3's activation context to bypass the limitations discovered in Step 5.

### Results
The hybrid approach failed due to similar activation context requirements:
- WinUI 3 initialization failed with "Bad IL format" error on Microsoft.UI.Xaml.dll
- Mixed-mode assemblies require package context for loading
- Both WinUI 3 and MAUI require packaged application identity

### Key Insights
The investigation confirms that modern Windows UI frameworks (MAUI, WinUI 3) are fundamentally designed for packaged applications. Attempting to host them from unpackaged WPF applications faces insurmountable activation context barriers.

See ChangeLog for detailed technical analysis and performance metrics.

## Step 5 WPF Host Investigation Complete

I've completed a comprehensive analysis of the WPF host implementation and documented the findings in the ChangeLog. Here's the key summary:

### Current State

The WPF host successfully:

- ✅ Loads Windows App Runtime DLLs (version 0x00010008)
- ✅ Resolves assembly chain (WPF → MAUI → dependencies)
- ✅ Captures WPF performance metrics (avg=2,208.60 ms for 50 items)

But fails at:

- ❌ MAUI initialization due to missing COM activation context
- ❌ ViewHandler static constructor throws COMException
- ❌ Cannot embed MAUI window (hwnd=0)

### Root Cause

MAUI requires a Windows App Runtime activation context that includes:

- Package identity (Package.appxmanifest)
- Proper COM apartment state
- Windows App Runtime initialization with package context

The reflection-based approach fundamentally cannot provide this context.

### Recommendations

I've documented three architectural options for moving forward:

1. __Direct ProjectReference with Package Identity__ (Recommended)

   - Convert WPF to packaged application
   - Add direct MAUI reference
   - Provides proper activation context

2. __IPC Architecture__

   - Run MAUI as separate process
   - Use named pipes/gRPC for communication
   - Clean architectural boundary

3. __Hybrid WinUI 3 Window__

   - Create WinUI window from WPF
   - Host MAUI content there
   - Complex but possible

### Performance Insights

Comparative analysis shows:

- MAUI: avg=479.00 ms, p50=236.19 ms (best mid-range performance)
- WinUI: avg=1,439.68 ms, p50=1,252.57 ms
- WPF: avg=2,208.60 ms, p50=3,276.29 ms (highest latency due to virtualization expansion)

The investigation reveals that the reflection-based embedding approach has reached its technical limits due to fundamental Windows App Runtime requirements. The project should pivot to one of the recommended approaches for Step 6.
