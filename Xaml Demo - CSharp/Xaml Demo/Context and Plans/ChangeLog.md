# Change Log

## October 8, 2025

### Documentation Rationalization - Mission Alignment

**Objective**: Clarify project mission, streamline documentation, and establish iteration handoff protocol to prevent architectural drift.

#### Problem Addressed

Previous iterations accumulated exploratory details that obscured the core mission:
- Project objectives unclear about MAUI as orchestration layer
- Plan document cluttered with WPF host exploration (Steps 5-9)
- Inconsistent handoff between iterations leading to confusion about architecture
- Current blocker (main MAUI app packaging) was initially misidentified

#### Actions Taken

1. **Created `IterationHandoffGuide.md`**
   - Establishes protocol for starting new iterations
   - Defines standards for documenting progress
   - Checklist for iteration completion
   - Common pitfalls to avoid (assumption drift, exploration sprawl)
   - Template for status updates and handoff summaries

2. **Updated `ProjectObjectives.md`**
   - Added clear statement: "MAUI as the Orchestration Layer"
   - Architecture diagram showing MAUI coordinating multiple surfaces
   - Clarified abstraction goals (in-process vs out-of-process)
   - Distinguished educational objectives from implementation details

3. **Created `ArchivalNotes-Steps5-9.md`**
   - Preserved detailed WPF host exploration findings
   - Documented WinRT activation barrier investigation
   - Moved 4,000+ lines of exploratory history out of main plan
   - Kept technical insights accessible without cluttering active docs

4. **Rewrote `Plan - Abstraction Prototype.md`**
   - Condensed from verbose exploration log to focused action plan
   - Clear "Current Iteration Status" section at top
   - Quick reference for active projects and key abstractions
   - Implementation history condensed to achievements and outcomes
   - Next steps with specific, actionable items
   - Performance baselines and risk mitigations in reference tables

#### Key Structural Changes

**Plan Document Structure**:
```
Before: 500+ lines mixing exploration attempts with current status
After:  
- Current Iteration Status (blocker, next action, recent work)
- Quick Reference (projects, abstractions, docs)
- Implementation History (condensed to outcomes)
- Next Steps (clear action items with success criteria)
- Architecture Reference (diagrams, interfaces, metrics)
- Handoff Summary (what works, what's blocked, next action)
```

**Documentation Cross-References**:
- ProjectObjectives.md → Mission and architecture principles
- IterationHandoffGuide.md → Iteration protocol
- Plan.md → Current status and action items
- ArchivalNotes-Steps5-9.md → Detailed exploration history
- ChangeLog.md → Session-by-session technical progress

#### Architectural Clarity Achieved

**Confirmed Understanding**:
1. **MAUI is the orchestration layer** - coordinates multiple surfaces
2. **IRenderSurface abstraction** - framework-agnostic surface contract
3. **Process isolation** - solution for WinRT activation barriers
4. **Out-of-process architecture** - IPC via named pipes for external MAUI process
5. **Current blocker** - Main MAUI app needs packaging, not WPF host

**Eliminated Confusion**:
- WPF host was exploratory (Steps 5-6), not the primary architecture
- Steps 7-9 validated packaging requirements, not ongoing work
- External process architecture (Step 10) is the correct path forward

#### Files Modified

- `Xaml Demo/Context and Plans/IterationHandoffGuide.md` (NEW - 250 lines)
- `Xaml Demo/Context and Plans/ProjectObjectives.md` (UPDATED - added architecture section)
- `Xaml Demo/Context and Plans/ArchivalNotes-Steps5-9.md` (NEW - 400 lines)
- `Xaml Demo/Context and Plans/Plan - Abstraction Prototype.md` (REWRITTEN - 400 lines, was 1,500+)
- `Xaml Demo/Context and Plans/ChangeLog.md` (this entry)

#### Benefits for Future Iterations

1. **Clear Starting Point**: New iterations read IterationHandoffGuide → ProjectObjectives → Plan Status
2. **Reduced Cognitive Load**: Archival notes separate from active guidance
3. **Mission Alignment**: Every iteration can verify alignment with core objectives
4. **Better Handoffs**: Standardized status format prevents context loss
5. **Actionable Next Steps**: Clear success criteria for each task

#### Next Iteration Bootstrap

When the next AI iteration starts, they should:
1. Read `IterationHandoffGuide.md` for protocol
2. Check `Plan.md` Current Iteration Status for blocker and next action
3. Confirm understanding: "MAUI orchestrates via IRenderSurface; needs packaging for WinRT activation"
4. Proceed with packaging main MAUI app and external process
5. Test IPC architecture end-to-end

**Status**: Documentation rationalization complete. Clear handoff established for packaging and testing phase.

---

## October 7, 2025

### Step 10: Out-of-Process Architecture Implementation

**Summary**: Implemented complete IPC-based architecture for running MAUI as an external packaged process, bypassing WinRT activation barriers through process isolation.

#### Components Created

1. **IPC Communication Layer** (`Xaml.Demo.Core/Ipc/`)
   - `IpcMessageTypes.cs`: Comprehensive message type system
     - Lifecycle: ProcessReady, WindowCreated, Shutdown
     - Data sync: SelectionChanged, ColorSwap
     - Monitoring: GetStatus, StatusResponse, PerfMetric
   - `IpcChannel.cs`: Named pipe bidirectional channel
     - Server/client mode support
     - Async message serialization (JSON with type metadata)
     - Automatic reconnection handling

2. **External MAUI Process** (`Xaml.Demo.External/`)
   - Complete standalone MAUI application
   - Command-line argument parsing (`--pipe=<name>`)
   - IPC service for lifecycle management
   - Window handle transmission via IPC
   - Shared ViewModel integration (Core project)

3. **Updated ExternalProcessSurface**
   - Transformed from simulation to real process launcher
   - Named pipe server with unique per-instance names
   - Process lifecycle management (launch, monitor, cleanup)
   - HWND reception and embedding support
   - Implements `IDisposable` for graceful shutdown

#### Technical Architecture

**Process Communication Flow:**
```
Host → Create pipe server → Launch external.exe --pipe=XamlDemo_Surface_{guid}
     ← External connects as client
     ← ProcessReadyMessage(HWND, PID)
Host → GetEmbedHandleAsync() returns HWND for embedding
Host → ColorSwapMessage / SelectionChangedMessage
     ← PerfMetricMessage / StatusResponseMessage
Host → ShutdownMessage on cleanup
```

**Key Features:**
- **Process Isolation**: MAUI runs with full package identity and WinRT activation
- **Clean Boundaries**: Clear separation between host and rendering process
- **Graceful Lifecycle**: 10-second connection timeout, automatic cleanup on disconnect
- **Performance Monitoring**: Built-in perf metric transmission via IPC
- **State Synchronization**: Selection and color commands forwarded via IPC

#### Files Created

```
Xaml.Demo.Core/Ipc/
├── IpcMessageTypes.cs (10 message types, 150 lines)
└── IpcChannel.cs (named pipe channel, 150 lines)

Xaml.Demo.External/
├── Program.cs (entry point)
├── MauiProgram.cs (DI configuration)
├── App.xaml/cs (IPC initialization)
├── MainPage.xaml/cs (CollectionView UI)
├── Services/
│   ├── IpcService.cs (IPC lifecycle)
│   └── ConsoleLogSink.cs (logging)
├── Resources/Styles/ (Colors, Styles)
└── Xaml.Demo.External.csproj
```

#### Educational Value

This implementation demonstrates:
1. **Architectural Constraints**: Precise identification of WinRT activation as the embedding barrier
2. **Practical Workaround**: Process isolation as solution to framework limitations
3. **Abstraction Preservation**: `IRenderSurface` contract maintained despite cross-process boundary
4. **Performance Comparison**: Foundation for measuring IPC overhead vs in-process rendering

#### Next Steps (Pending)

1. Add `Xaml.Demo.External` to solution file
2. Configure packaging for external process deployment
3. Update `LaunchExternalProcess()` with actual executable path
4. Build and test end-to-end IPC communication
5. Implement HWND embedding in WPF host (optional)
6. Document performance characteristics

**Status**: Architecture complete, ready for integration testing.

---

### Step 10.1: Build Error Resolution

**Issue**: Build failed with error CS0234 - `System.Text.Json` namespace not found in `Xaml.Demo.Core` project.

**Root Cause**: The `Xaml.Demo.Core` project targets `netstandard2.1`, which does not include `System.Text.Json` in the framework. The IPC implementation uses `JsonSerializer` for message serialization but the NuGet package reference was missing.

**Solution**: Added `System.Text.Json` NuGet package reference and upgraded C# language version to 9.0 in `Xaml.Demo.Core.csproj`:
```xml
<LangVersion>9.0</LangVersion>
<PackageReference Include="System.Text.Json" Version="8.0.0" />
```

**Files Modified**:
- `Xaml Demo - CSharp/Xaml.Demo.Core/Xaml.Demo.Core.csproj`

**Additional Fix**: C# 9.0 language version required for target-typed object creation (`new()`) syntax used in IPC implementation.

**Status**: Build errors resolved. Ready to proceed with building the solution.

---

### Step 10.2: LogRouter Namespace Resolution

**Issue**: Build failed on non-Windows platforms (iOS, MacCatalyst) with error CS0103 - `LogRouter` not found in `ExternalProcessSurface.cs`.

**Root Cause**: The `LogRouter` class is defined in `Xaml.Demo.Core` project under the `Xaml_Demo.Logging` namespace, but `ExternalProcessSurface.cs` in the MAUI project was missing the using directive for this namespace.

**Architectural Validation**: This confirms proper separation of concerns - `LogRouter` is correctly placed in the Core project as a platform-agnostic logging abstraction, with platform-specific implementations (`ILogSink`) providing the actual logging behavior. The MAUI project just needed to reference the Core namespace.

**Solution**: Added missing `using Xaml_Demo.Logging;` directive to `ExternalProcessSurface.cs`.

**Files Modified**:
- `Xaml Demo - CSharp/Xaml Demo/Surfaces/ExternalProcessSurface.cs`

**Status**: Namespace issue resolved. LogRouter separation of concerns validated as correct.

---

### Step 8: MAUI Activation Context Progress
- Successfully created Windows Application Package (.msix) with proper manifest
- Created and installed self-signed certificate for package signing  
- Built and deployed signed package with package identity
- Configured WPF host project with RuntimeIdentifier for proper platform targeting
- Successfully launched WPF host application

### Key Findings:
1. **Package Identity Solution**: Created a Windows Application Package that provides the necessary identity for MAUI initialization
2. **Reflection Architecture Works**: The reflection-based approach successfully avoids direct references while allowing runtime loading
3. **Windows App Runtime**: Successfully copied Windows App Runtime files from MAUI project output to WPF host

### Step 9: MAUI Window Embedding Validation - CRITICAL FINDINGS

#### Achievements:
1. **Package Identity Successfully Deployed**:
   - Created Windows Application Package (.msix) with proper signing
   - Successfully installed and launched packaged WPF application
   - Package provides identity context for activation

2. **Comprehensive DLL Packaging**:
   - Created automation script (`Scripts/Add-MauiDllsToPackage.ps1`) to identify required DLLs
   - Successfully packaged all 25 MAUI and dependency DLLs:
     - Xaml Demo.dll (MAUI app assembly)
     - Microsoft.Maui.*.dll (MAUI framework)
     - Microsoft.Extensions.*.dll (dependency injection, logging, configuration)
     - Microsoft.UI.Xaml.*.dll (WinUI components)
     - Microsoft.WindowsAppRuntime.dll (Windows App Runtime)

3. **Assembly Loading SUCCESS**:
   - ✅ `HOST[Lifecycle:MauiAssemblyLoaded]: name=Xaml Demo`
   - ✅ `HOST[Lifecycle:MauiControlsAssemblyLoaded]: name=Microsoft.Maui.Controls`
   - ✅ ViewHandler TypeInitializationException from Step 8 RESOLVED
   - ✅ No more DLL loading errors

#### Critical Discovery - WinRT Activation Barrier:
**MAUI initialization fails at WinRT COM activation**, not DLL loading:

```
COMException (0x80040154): The request is not supported.
at WinRT.ActivationFactory.Get(String typeName, Guid iid)
at Microsoft.UI.Xaml.Input.FocusManager.get__objRef_global__Microsoft_UI_Xaml_Input_IFocusManagerStatics()
at Microsoft.Maui.Handlers.ViewHandler..cctor()
```

**Root Cause**: Package identity alone is insufficient. WinRT type activation requires:
- Proper COM registration of WinUI types
- Windows App Runtime initialization (currently failing with hr=0x80070032)
- Or a true WinUI 3 application with proper manifest declarations

**Implications**:
- Embedding MAUI (.NET 10 + WinUI 3) into WPF requires more than package identity
- The architectural boundary is at the **WinRT activation layer**, not the assembly loading layer
- Successfully demonstrates the **limits of abstraction** - some framework dependencies cannot be isolated

### Current Status:
- **Step 9 COMPLETE**: Successfully validated the architectural boundaries
- Package identity deployment working
- Assembly loading architecture proven
- WinRT activation barrier identified and documented

### Next Steps:
1. **Document architectural findings**: Update plan with WinRT activation constraint
2. **Alternative approaches to explore**:
   - Out-of-process MAUI hosting (already in codebase as ExternalProcessSurface)
   - Pure WinUI 3 host instead of WPF
   - MAUI without WinUI dependencies (if possible)
3. **Step 10**: Compare performance metrics across successfully working surfaces

## Previous Entries

### September 29, 2025

#### Step 7 Completion: Reflection-based Architecture
- Successfully implemented reflection-based MAUI loading in WPF host
- MauiBootstrapper class uses Assembly.Load to dynamically load MAUI assemblies
- Removed all direct project references between WPF host and MAUI project
- Build succeeds without conflicts - projects can coexist in solution

#### Key Components Created:
1. **MauiBootstrapper.cs**: Reflection-based MAUI initialization
2. **MauiHwndHost.cs**: Placeholder for MAUI window hosting
3. **WinUIWindowHost.cs**: Placeholder for WinUI window hosting  

#### Build Configuration:
- WPF Host: Targets net10.0-windows10.0.19041.0 with WPF enabled
- MAUI assemblies copied to WPF output via MSBuild target
- Windows App SDK referenced for runtime only (build assets excluded)

### September 24, 2025

#### Initial Project Setup
- Created solution structure with Core, MAUI, and WPF host projects
- Established abstraction layers for surface rendering
- Defined interfaces for cross-framework communication

#### Architecture Decisions:
1. **Separation of Concerns**: ViewModels in Core project, framework-specific Views in respective projects
2. **Surface Abstraction**: IRenderSurface interface for pluggable rendering targets
3. **Performance Tracking**: Built-in performance aggregation for comparing frameworks

#### Created Projects:
- **Xaml.Demo.Core**: Shared ViewModels and interfaces
- **Xaml Demo** (MAUI): Primary MAUI application
- **Xaml.Demo.Host.Wpf**: WPF host for embedding MAUI/WinUI content
- **Xaml.Demo.Package**: Windows Application Package for identity
