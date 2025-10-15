# Plan - Abstraction Prototype

**Status**: ACTIVE  
**Current Step**: 10 - Out-of-Process Architecture Testing  
**Last Updated**: October 8, 2025

---

## Current Iteration Status

**Mission**: MAUI orchestrates multiple rendering surfaces (in-process + out-of-process) via `IRenderSurface` abstraction

**Blocker**: Main MAUI app requires packaging for Windows App Runtime activation  
**Next Action**: Package main MAUI app and external process, then test IPC communication

### Recent Accomplishments
- ✅ IPC infrastructure implemented (named pipes, message types)
- ✅ External MAUI process created with command-line pipe argument parsing
- ✅ Packaging infrastructure validated (Steps 8-9)
- ✅ ExternalProcessSurface ready to launch external process

### Architectural Understanding
- **WinRT Activation Barrier**: Modern Windows UI frameworks require package identity for COM activation (validated in Steps 5-9, see `ArchivalNotes-Steps5-9.md`)
- **MAUI as Orchestrator**: Main MAUI app coordinates multiple surfaces through `IRenderSurface` abstraction
- **Process Isolation**: External processes communicate via IPC (named pipes) for independent activation contexts
- **Performance Baseline**: MauiCollection avg=479ms, WinUI avg=1,440ms (first 50 items)

---

## Quick Reference

### Active Projects
- **`Xaml Demo`**: Main MAUI application (orchestration layer) - *Needs packaging*
- **`Xaml.Demo.Core`**: Shared ViewModels, interfaces, IPC infrastructure
- **`Xaml.Demo.External`**: External MAUI process for out-of-process surface - *Needs packaging*
- **`Xaml.Demo.Package`**: Windows Application Package (existing infrastructure)

### Key Abstractions
- **`IRenderSurface`**: Framework-agnostic surface contract
- **`IpcChannel`**: Named pipe communication for cross-process coordination
- **`ISurfaceCatalog`**: Surface registration and ordering
- **`IUiDispatcher`**: Cross-thread marshaling abstraction

### Documentation Files
- **`ProjectObjectives.md`**: Core mission and architecture principles
- **`IterationHandoffGuide.md`**: Iteration protocol and handoff standards
- **`ArchivalNotes-Steps5-9.md`**: Detailed WinRT activation investigation history
- **`ChangeLog.md`**: Session-by-session technical progress

---

## Implementation History (Condensed)

### Stages 1-3: Core Abstraction & Instrumentation - COMPLETE ✅

**Objective**: Create framework-agnostic surface abstraction with performance metrics

**Key Deliverables**:
- `IRenderSurface` interface with lifecycle management (InitializeAsync, GetEmbedHandleAsync, Invalidated event)
- `MauiCollectionSurface` and `WinUIListViewSurface` implementations
- Performance instrumentation via `SurfacePerfAggregator` (first-N item realization timing)
- `IUiDispatcher` abstraction for cross-thread marshaling
- Selection synchronization across surfaces (HashSet + per-item Selected property)

**Results**:
- MauiCollection: avg=479ms, p50=236ms, p90=1,327ms (50 items)
- WinUIListView: avg=1,440ms, p50=1,253ms, p90=2,932ms (50 items)
- Both surfaces achieve "Ready" state with consistent selection behavior

---

### Stage 4: Dynamic Surface Mounting - COMPLETE ✅

**Objective**: Remove static XAML; mount all surfaces dynamically via catalog

**Key Changes**:
- Replaced static `ItemsCollectionView` with dynamic `LeftSurfaceHost` ContentView
- Surfaces instantiated at runtime via `ISurfaceCatalog`
- Template assignment moved to code-behind for clearer dynamic hosting
- `ExternalProcessSurface` stub created (simulation mode)

**Performance Validation**:
- No regression from dynamic mounting
- MauiCollection improved: 1,396ms → 479ms avg (reduced template overhead)
- WinUI remained stable: ~1,440ms avg

---

### Steps 5-9: WinRT Activation Investigation - COMPLETE ✅

**Objective**: Understand activation context requirements for cross-framework hosting

**Exploration Path** (detailed in `ArchivalNotes-Steps5-9.md`):
1. **Step 5**: WPF host with reflection-based MAUI loading → Assembly loading works, WinRT activation fails
2. **Step 6**: Hybrid WinUI 3 window approach → Bootstrap fails without package identity
3. **Step 7**: Build isolation via pure reflection → Confirms packaging is the requirement
4. **Step 8**: Package identity creation → Infrastructure validated
5. **Step 9**: MAUI embedding with package identity → Each framework needs its own package context

**Key Finding**: **WinRT activation requires package identity**. Cross-framework in-process hosting encounters COM activation barriers. Solution: Process isolation with IPC.

**Outcome**: Validated out-of-process architecture as clean solution for framework boundaries.

---

### Step 10: Out-of-Process Architecture - IN PROGRESS ⚙️

**Objective**: Implement true cross-process MAUI embedding using IPC

#### Implementation Complete ✅

**IPC Infrastructure** (`Xaml.Demo.Core/Ipc/`):
- `IpcMessageTypes.cs`: Message definitions
  - `ProcessReadyMessage`: Initial handshake with HWND and PID
  - `WindowCreatedMessage`: Window size/position updates
  - `SelectionChangedMessage`: Selection state sync
  - `ColorSwapMessage`: Command routing
  - `PerfMetricMessage`: Performance data
  - `ShutdownMessage`: Graceful shutdown
- `IpcChannel.cs`: Named pipe bidirectional communication
  - Server and client modes
  - Async message sending/receiving
  - Connection timeout handling

**External MAUI Process** (`Xaml.Demo.External/`):
- Standalone MAUI app with proper Windows platform structure
- Command-line argument parsing: `--pipe=<name>`
- `IpcService.cs`: Manages connection lifecycle and message handling
- `ConsoleLogSink.cs`: Logging for external process
- `MainPage.xaml`: CollectionView with 1000 color items
- Windows platform `App.xaml.cs`: Parses pipe name and initializes IPC

**ExternalProcessSurface** (`Xaml Demo/Surfaces/`):
- Transformed from simulation to real IPC implementation
- Launches external process with unique pipe name
- Receives window handle via `ProcessReadyMessage`
- Implements `IDisposable` for cleanup
- 10-second connection timeout with cancellation

**Test Infrastructure**:
- `Scripts/Test-IpcArchitecture.ps1`: Automated build and launch script
- Process monitoring and status display

#### Testing Blocked ❌

**Current Issue**: Main MAUI app fails to launch due to missing package identity

From `session-log.txt`:
```
HOST[Lifecycle:WinAppRuntimeBootstrap]: status=ExhaustedAttempts
HOST[Lifecycle:MauiCreateReflectionError]: TypeInitializationException
  → ViewHandler static constructor
    → COMException (0x80040154) at WinRT.ActivationFactory.Get()
```

**Root Cause**: Main MAUI app running unpackaged cannot initialize Windows App Runtime.

---

## Next Steps (Clear Action Items)

### 1. Package Main MAUI Application

**Objective**: Enable Windows App Runtime activation in main app

**Actions**:
- Use existing `Xaml.Demo.Package.wapproj` infrastructure
- Update package manifest to reference main MAUI app
- Build package via MSBuild Tools in VS Code
- Install .msix package using PowerShell
- Verify main app launches successfully

**Success Criteria**:
- Main MAUI app initializes without TypeInitializationException
- In-process surfaces (MauiCollection, WinUIListView) display correctly
- Log shows successful Windows App Runtime bootstrap

**Files to Modify**:
- `Xaml.Demo.Package/Package.appxmanifest`: Update EntryPoint
- May need to rebuild package with updated references

### 2. Package External MAUI Process

**Objective**: Provide independent package identity for external process

**Actions**:
- Create separate package project for `Xaml.Demo.External` OR
- Add external app as additional executable in main package
- Configure package identity and manifest
- Build and deploy external package

**Success Criteria**:
- External process launches standalone with `--pipe` argument
- Windows App Runtime initializes successfully
- CollectionView displays 1000 color items

### 3. Test IPC Architecture End-to-End

**Objective**: Validate cross-process communication and window handle transmission

**Test Sequence**:
1. Launch main packaged MAUI app
2. Navigate to MultiVisualPerfView
3. Verify `ExternalProcessSurface` launches external process
4. Confirm named pipe connection in logs
5. Verify `ProcessReadyMessage` with valid HWND received
6. Test command messages (ColorSwap, SelectionChanged)
7. Verify graceful shutdown

**Success Criteria**:
- External process launches and connects
- Window handle transmitted successfully
- IPC messages flow bidirectionally
- Both processes can be shut down cleanly
- No zombie processes or orphaned pipes

### 4. Document Results

**Objective**: Capture findings for educational value and future reference

**Documentation Updates**:
- `ChangeLog.md`: Add entry with IPC test results
- `Plan - Abstraction Prototype.md`: Update status with outcomes
- Performance comparison: External process overhead vs. in-process
- Architectural findings: Process isolation trade-offs

**Metrics to Capture**:
- External surface initialization time
- IPC message latency (round-trip time)
- External process memory footprint
- Comparison with in-process surface performance

---

## Architecture Reference

### Surface Abstraction Layers

```
┌─────────────────────────────────────────────────────┐
│ MAUI XAML (MultiVisualPerfView.xaml)                │
│ - Orchestration layer                               │
│ - Framework-agnostic UI structure                   │
└─────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────┐
│ ISurfaceCatalog / IRenderSurface                    │
│ - Abstraction boundary                              │
│ - Lifecycle management                              │
│ - Event coordination                                │
└─────────────────────────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┐
        │               │               │
        ▼               ▼               ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ In-Process   │ │ In-Process   │ │ Out-of-      │
│ Surface      │ │ Surface      │ │ Process      │
│              │ │              │ │ Surface      │
│ MAUI         │ │ WinUI3       │ │              │
│ Collection   │ │ ListView     │ │ IPC Channel  │
│              │ │ (Handler)    │ │ Named Pipes  │
└──────────────┘ └──────────────┘ └──────────────┘
                                          │
                                          ▼
                                  ┌──────────────┐
                                  │ External     │
                                  │ MAUI Process │
                                  │ (Packaged)   │
                                  └──────────────┘
```

### Key Interfaces

| Interface | Purpose | Location |
|-----------|---------|----------|
| `IRenderSurface` | Uniform surface contract | `Xaml.Demo.Core/Surfaces/` |
| `ISurfaceCatalog` | Surface registration | `Xaml.Demo.Core/Surfaces/` |
| `IUiDispatcher` | Thread marshaling | `Xaml.Demo.Core/Dispatching/` |
| `ILogSink` | Logging abstraction | `Xaml.Demo.Core/Logging/` |
| `IPerfTimer` | Performance measurement | `Xaml.Demo.Core/Perf/` |
| `IpcChannel` | Cross-process communication | `Xaml.Demo.Core/Ipc/` |

### IPC Message Flow

```
Host Process                        External Process
=============                       ================
1. Create named pipe server
2. Launch external.exe --pipe=X --> 3. Parse --pipe argument
4. Accept connection            <-- 5. Connect as client
6. Listen for messages          <-- 7. Send ProcessReadyMessage
8. Receive HWND                     
9. Store handle for embedding       
10. Send ColorSwapMessage       --> 11. Execute command
                                <-- 12. Send PerfMetric
```

---

## Risk Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Package build complexity | Deployment friction | Use existing package infrastructure; automate via scripts |
| IPC connection timeout | Surface fails to initialize | 10-second timeout with cancellation; retry logic |
| External process crash | Data loss | IPC disconnect detection; graceful degradation |
| Window handle ownership | Cleanup issues | External process retains handle; host uses for embedding only |
| Cross-process performance | UI lag | Async IPC; minimize message frequency; batch updates |

---

## Performance Baselines

### In-Process Surfaces (Stage 4 Results)

**MauiCollectionSurface**:
- Average: 479.00 ms
- p50: 236.19 ms
- p90: 1,327.26 ms
- p99: 1,645.71 ms
- First item: 30.42 ms
- Readiness: Ready (50/50 items)

**WinUIListViewSurface**:
- Average: 1,439.68 ms
- p50: 1,252.57 ms
- p90: 2,932.36 ms
- p99: 3,240.92 ms
- First item: 39.05 ms
- Readiness: Ready (50/50 items)

### Expected External Process Metrics

- Initialization overhead: +100-200ms (process launch, IPC handshake)
- IPC latency: ~1-5ms per message (named pipes, local machine)
- Memory isolation: +30-50MB (separate process overhead)
- Benefits: Independent crash domain, clean activation context

---

## Handoff Summary

**What Works**:
- ✅ Core abstraction layer (`IRenderSurface`, `ISurfaceCatalog`)
- ✅ In-process surface implementations with performance instrumentation
- ✅ IPC infrastructure (named pipes, message types)
- ✅ External MAUI process with command-line argument parsing
- ✅ Dynamic surface mounting via catalog

**What's Blocked**:
- ❌ Main MAUI app requires packaging for WinRT activation
- ❌ Cannot test IPC architecture until main app runs

**Clear Next Action**:
1. Package main MAUI app using `Xaml.Demo.Package.wapproj`
2. Build and install package
3. Verify main app launches successfully
4. Then proceed with external process packaging and IPC testing

**Iteration Protocol**: See `IterationHandoffGuide.md` for standards on starting new iterations, documenting progress, and creating handoff summaries.

---

**Last Updated**: October 8, 2025  
**Next Review**: After packaging and IPC testing complete
