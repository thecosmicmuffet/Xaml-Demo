# Change Log

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
