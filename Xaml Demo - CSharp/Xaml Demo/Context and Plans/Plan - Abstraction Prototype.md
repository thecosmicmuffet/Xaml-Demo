# Status

Current Step: 3 (12/20)

## Previous Iteration Summary

Stage 3 dynamic surface infrastructure implemented:

- Added NativeHandleRef, SurfaceInvalidatedEventArgs, extended IRenderSurface.
- Implemented dispatcher abstraction (IUiDispatcher + MauiDispatcherAdapter + ambient + stubs for WPF/UWP) and registered in DI.
- Centralized FrameworkSurfaceKind in Core; removed duplicate.
- Added ISurfaceCatalog and refactored MultiVisualPerfViewModel to consume it (SurfaceOrder removed).
- Converted MultiVisualPerfView to dynamically mount WinUI list via RightSurfaceHost; replaced static WinUI shim with runtime-created instance.
- Added MauiCollectionSurface and WinUIListViewSurface implementations (MauiCollectionSurface currently preparatory; left CollectionView still static).
- Updated ExistingMauiViewSurface to new interface members.
- Plan.md checklist updated to reflect completed items.

Remaining Stage 3 items (not yet implemented):

- Selection/color synchronization across surfaces using dispatcher marshaling.
- Per-surface IPerfTimer instrumentation.
- Lifecycle state enum + logging transitions.
- LogRouter dispatcher integration.
- Cleanup of obsolete ViewModel / surface artifacts & dynamic left surface conversion (replace static ItemsCollectionView with host + MauiCollectionSurface).
- Documentation updates in Plan.md for decisions & lifecycle semantics.
- Build & validation on Windows target; commit final Stage 3 scaffolding.

Current checklist status preserved below.

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
- [ ] Add selection/color synchronization using dispatcher marshaling
- [ ] Integrate IPerfTimer instrumentation (first N realized items per surface)
- [ ] Add lifecycle state enum + logging transitions
- [ ] Update LogRouter to optionally dispatch via IUiDispatcher
- [ ] Remove legacy duplicates (FrameworkSurfaceKind.cs in MAUI, obsolete ViewModel files)
- [ ] Document decisions & progress updates in Plan.md
- [ ] Build & validate Windows target behavior
- [ ] Commit Stage 3 initial implementation (message: "Stage3: surface lifecycle + dispatcher scaffolding")

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
