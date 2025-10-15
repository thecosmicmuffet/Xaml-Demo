# Project Mission

## MAUI as the Orchestration Layer

**Core Principle**: MAUI provides the stable XAML specification layer that orchestrates multiple rendering surfaces, each potentially using different UI frameworks (WinUI3, UWP, WPF) or running in separate processes.

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│ MAUI Application (Orchestration Layer)                      │
│ - Provides stable XAML structure                            │
│ - Coordinates via IRenderSurface abstraction                │
│ - Manages lifecycle and communication                       │
└─────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        ▼                   ▼                   ▼
┌──────────────┐   ┌──────────────┐   ┌──────────────┐
│ In-Process   │   │ In-Process   │   │ Out-of-      │
│ Surface      │   │ Surface      │   │ Process      │
│ (MAUI)       │   │ (WinUI3)     │   │ Surface      │
│              │   │              │   │ (External    │
│              │   │              │   │  MAUI App)   │
└──────────────┘   └──────────────┘   └──────────────┘
                                                │
                                                ▼
                                       ┌──────────────┐
                                       │ IPC Channel  │
                                       │ (Named Pipes)│
                                       └──────────────┘
```

## Primary Goals

### 1. Demonstrate XAML Abstraction Utility

Prove that XAML can orchestrate components with:
- Different UI framework dependencies (MAUI, WinUI3, UWP, WPF)
- Different rendering pipelines (in-process vs. out-of-process)
- Different activation contexts (packaged vs. unpackaged)
- Unified data binding and command infrastructure

**Key Abstraction**: The `IRenderSurface` interface provides framework-agnostic surface integration, allowing MAUI to coordinate surfaces regardless of their underlying implementation.

### 2. Overcome Cross-Framework Limitations

**Challenge**: WinUI3 and modern Windows UI frameworks require specific activation contexts (package identity, COM registration) that create barriers to traditional in-process hosting.

**Solution Approach**:
- **In-Process Surfaces**: For compatible frameworks (MAUI CollectionView, WinUI3 ListView with custom handlers)
- **Out-of-Process Surfaces**: For isolated activation contexts via IPC communication
- **Process Boundary Management**: Using named pipes for state synchronization and command routing

### 3. Educational Demonstration

Create a working example that teaches:
- Where framework abstraction works seamlessly
- Where architectural boundaries require process isolation
- How to design surface abstractions that work across both scenarios
- Performance trade-offs between different rendering approaches

## Teach XAML abstraction techniques with diverse UI Approaches 

Show different techniques for achieving similar effects and couple these techniques to metrics like load time and memory footprint as well as demonstrating differences in behavior or potential for expansion. The main frame of the app should have a console-style text display where I can keep an updated log of relevant information when various controls are used. I also need basic viewmodel infrastructure with a property that I can bind to this frame. I would prefer the notification infrastructure be static so I can access it from any other viewmodel or control while performing other tasks. The text frame will always be present; content will be displayed via Page controls in a Frame next to the text output.

## Explore epistemological issues with UI and data

Create a Model based around Color, which is a view consideration, but often must be stored as information and user-associated. Color can be used to communicate UI framework concepts to the user, but is always competing for clarity with general use cases, like decoration, personalization, or latent space affordances. To demonstrate proper MVVM with Colors as the main internal concept, the ViewModel should have a observable collection of ItemViewModels in order to maintain a data-type neutrality in design, and demonstrate how the Model can be used unpredictably at the View layer. I'm going to use the ItemViewModels in particular to illustrate listview performance considerations. They should have a Color property and a DefaultColor which each return Color objects, a ColorString property which returns the hex code string, and a Function GetColor(). DefaultColor should be stored in a separate field from the current Color, and the ItemViewModel constructor should only allow setting the DefaultColor at creation. The MultiVisualPerfViewModel collection should contain 1000 entries. A private function should be created to create any number of entries and hue-shift incrementally by N/numEntries in order to populate a spectrum of ItemViewModels DefaultColors.
