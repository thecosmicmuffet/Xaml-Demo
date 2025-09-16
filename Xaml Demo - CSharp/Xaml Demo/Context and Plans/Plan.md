# Status

Current Step: 2
## Previous Step Summary

Stage 1 surface abstraction introduced.

Changes:

- Added Surfaces: FrameworkSurfaceKind, IRenderSurface, ExistingMauiViewSurface.
- Extended MultiVisualPerfViewModel with SurfaceOrder property.
- Added x:Name to WinUIListViewShim (RightListShim).
- Updated MultiVisualPerfView.xaml.cs to collect surfaces dynamically (EnsureSurfaces).
- Build succeeded; only XML doc warnings (no functional errors).

Next optional fixes (not applied):

- Clean malformed XML comments in FirstBindTracker.cs and MultiVisualPerfViewModel.cs.
- Add future UWP placeholder surface implementation.

Ready for further stages (WPF host project, dispatcher abstraction, out-of-process exploration).

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
- [ ] Create Xaml.Demo.Core project (netstandard2.1) for shared logic
- [ ] Add Core project to solution and add project reference from MAUI project
- [ ] Introduce placeholder interfaces (ILogSink, IPerfTimer) in Core
- [ ] Move BaseViewModel, ISelectable, PerfItemViewModel, MultiVisualPerfViewModel into Core
- [ ] Adjust namespaces and using directives after move
- [ ] Extract LogHub contract to ILogSink and decide implementation placement
- [ ] Evaluate NavigationHub & ScenarioCatalog (defer move if tightly coupled to MAUI Shell)
- [ ] Decide on Color dependency strategy (direct Microsoft.Maui.Graphics reference vs abstraction)
- [ ] Update MAUI project code to reference Core types
- [ ] Build solution and resolve any compiler errors
- [ ] Update Plan.md progress markers after each completed milestone
- [ ] Draft embedding strategy notes for upcoming WPF host (HWND acquisition, HwndHost plan)

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
