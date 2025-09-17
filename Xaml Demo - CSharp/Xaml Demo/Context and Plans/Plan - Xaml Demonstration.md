# Status
ACTIVE
Implementation iteration (SelectableByProperty state + converter-based template swapping)

## Previous Step Summary
Initial plan stub only; no prior implementation steps documented.

## Current Iteration (2025-09-17)
Implemented a new visual state `SelectableByProperty` in `MultiVisualPerfView` that demonstrates selection-driven template swapping via:
- A boolean-binding (`PerfItemViewModel.Selected`)
- A custom converter (`SelectionToTemplateConverter`)
- A dynamic template host control (`TemplateHost`) to realize the bound `DataTemplate`

This offers a contrast with the existing `Selectable` state which uses a `DataTemplateSelector` coupled to collection-level selection membership (`MultiVisualPerfViewModel.SelectedItems` + `SelectionVersion` refresh triggers).

### Added Artifacts
- `Converters/SelectionToTemplateConverter.cs`
- `Controls/TemplateHost.cs`
- New `DataTemplate` (`PerfItemTemplateByProp`) and state `SelectableByProperty` in `MultiVisualPerfView.xaml`
- Additional button: “Toggle Select All (ItemViewModel property)”
- Code-behind method `OnToggleSelectAllViaProperty`
- Extended state cycle sequence: Normal → Highlighted → Selectable → SelectableByProperty → (wrap)

### Rationale
This demonstrates an alternate abstraction layer where:
1. Per-item selection is intrinsic to the item VM (`Selected` property) rather than maintained in an external HashSet.
2. The View chooses between existing visual templates (`PerfItemTemplate` / `PerfItemTemplateSelected`) through a simple value conversion instead of a selector driven by view model collection membership.
3. Template realization timing + logging pathways remain comparable to earlier modes (leveraging existing timer logic in `OnChangeVisualState` and operation timing in selection toggles).

### Comparative Notes
| Aspect | HashSet + Selector (Selectable) | Item Property + Converter (SelectableByProperty) |
|--------|---------------------------------|--------------------------------------------------|
| Selection Authority | Central collection (`SelectedItems`) | Distributed per-item (`PerfItemViewModel.Selected`) |
| Refresh Mechanism | Force reassign ItemTemplate to trigger selector | Direct property change drives converter reevaluation |
| Complexity | Higher (membership + bump version) | Lower (boolean binding) |
| Bulk Toggle Cost | HashSet iteration + per-item Replace notifications | Direct property set loop |
| Extensibility | Can express multi-source selection contexts | Natural for simple binary selection |

### Pending Evaluation / Next Metrics
- Measure time difference between bulk select operations in both modes (already instrumented via LogHub timers).
- Observe GC / allocation patterns if `TemplateHost` leads to more frequent full element recreation vs selector path.
- Decide if a generic “TemplateHost” surface abstraction is useful for future device-specific template injection.

## Next Planned Enhancements
1. Add metric aggregation (avg realization time across state transitions).
2. Introduce a hybrid mode: HashSet authority + per-item shadow Selected property (for cross-surface sync scenarios).
3. Document performance observations after first measurement pass.
4. (Optional) Create a unified `ISelectionStrategy` abstraction to swap between strategies at runtime (educational value).

## Open Questions
- Should `TemplateHost` cache realized views when templates flip back and forth? (Currently recreates for clarity of demo.)
- Is an `ISelectableItem` interface warranted for broader surfaces beyond MAUI CollectionView?
- Do we surface selection change deltas through an event for external instrumentation?

## Risks / Watch Items
| Risk | Impact | Mitigation |
|------|--------|------------|
| Frequent template recreation overhead | Higher CPU / allocations | Optional caching layer in `TemplateHost` |
| Divergence between HashSet + property states if combined later | Inconsistent visuals | Introduce synchronization helper |
| Converter-based approach obscures selection source for unfamiliar readers | Learning curve | Inline comments + Plan documentation (this section) |

## Historical Log (Iteration Start)
See `ChangeLog.md` for chronological entries.

## Plan (High-Level Roadmap)
1. Baseline three selection rendering strategies (DONE: direct template, selector, converter).
2. Instrument comparative timings across:
   - State transitions
   - Bulk toggles
   - Lasso selection batches
3. Add surface abstraction experiments (future iteration – ties into Abstraction Prototype plan resume).
4. Introduce cross-platform host (WPF aggregation) after confirming in-process abstractions are stable.
5. Finalize educational narrative (diagrams + README section) summarizing layering benefits.

## Completion Criteria for This Iteration
- New state operational (DONE)
- Logging present for timing (DONE – reuses existing timers; may extend granular metrics later)
- Documentation updated (PLAN + CHANGELOG) (PARTIALLY DONE – ChangeLog initial entry queued)
- Build succeeds on all target frameworks
- Follow-up metrics step scheduled
