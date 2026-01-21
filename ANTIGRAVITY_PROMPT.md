# Visualizer UI/UX Overhaul - Development Task

## Overview
The societies simulation visualizer needs significant UI/UX improvements to address performance issues, poor responsive design, and usability problems. This is a comprehensive refactor task that will improve the user experience significantly.

## Priority Issues to Fix

### 1. **CRITICAL: Performance Optimization**
- **Problem**: `_update_map_data()` called multiple times per frame, causing massive performance degradation
- **Problem**: UI panels update every single tick without throttling
- **Problem**: Market panel rebuilds entire filter lists on every update
- **Solution**: Implement update throttling, data caching, and smart update scheduling

### 2. **HIGH: Layout and Responsive Design** 
- **Problem**: Fixed 260px left panel width doesn't adapt to screen sizes
- **Problem**: Metrics panel overlays map without proper resizing
- **Problem**: Bottom bar is cluttered with poor visual hierarchy
- **Solution**: Make panels resizable, implement responsive breakpoints, improve space management

### 3. **HIGH: Code Architecture**
- **Problem**: VisualizerMain has too many responsibilities (file I/O, UI updates, state management)
- **Problem**: Heavy reliance on @onready node paths creates brittle code
- **Problem**: No separation of concerns between UI and data logic
- **Solution**: Create separate controller classes, implement proper MVC pattern, use signals for decoupling

## Specific Implementation Tasks

### Phase 1: Performance & Stability (Do First)
1. **Add Update Throttling System**
   ```gdscript
   # In visualizer_main.gd - implement update timer and pending update system
   # Target: Max 10 UI updates per second instead of 60+
   ```

2. **Fix Duplicate Update Calls**
   ```gdscript
   # Remove duplicate _update_map_data() calls in _on_run_started()
   # Consolidate update logic into single throttled method
   ```

3. **Implement Data Caching**
   ```gdscript
   # Cache expensive calculations in panels
   # Only rebuild filter data when underlying data actually changes
   ```

### Phase 2: Layout & Responsive Design
1. **Make Left Panel Resizable**
   ```gdscript
   # Replace fixed VBoxContainer with HSplitContainer
   # Add minimum/maximum size constraints
   ```

2. **Fix Metrics Panel Overlay**
   ```gdscript
   # Implement proper panel that doesn't obscure map
   # Add toggle behavior with smooth transitions
   ```

3. **Improve Bottom Bar Layout**
   ```gdscript
   # Group related elements visually
   # Add proper spacing and visual hierarchy
   ```

### Phase 3: Architecture Refactor
1. **Create UI Controller Classes**
   ```gdscript
   # MapViewController.gd - handles map-related UI logic
   # PanelController.gd - manages data panels
   # StateController.gd - handles simulation state
   ```

2. **Implement Proper Signal System**
   ```gdscript
   # Replace direct method calls with signal-based communication
   # Create centralized event system for UI updates
   ```

3. **Separate Data Models**
   ```gdscript
   # Create proper data models with caching
   # Implement observer pattern for data changes
   ```

### Phase 4: UX Enhancements
1. **Add Visual Feedback**
   ```gdscript
   # Loading indicators for file operations
   # Toast notifications for non-critical messages
   # Hover states and transitions
   ```

2. **Improve Error Handling**
   ```gdscript
   # Persistent error messages instead of temporary labels
   # Better error categorization and display
   ```

3. **Add Keyboard Shortcuts**
   ```gdscript
   # Space: pause/play
   # S: step tick
   # D: step day
   # M: toggle metrics
   # Esc: clear selection
   ```

## Implementation Guidelines

### Code Quality Standards
- Follow existing Godot naming conventions
- Add proper documentation for new classes/methods
- Use type hints throughout
- Implement null safety checks

### Performance Targets
- UI updates: ≤10 per second (currently 60+)
- Panel refresh: ≤50ms per panel
- Map rendering: Maintain 60fps at 1x zoom

### Testing Requirements
- Test with different screen resolutions
- Verify performance with large agent counts (1000+)
- Test all file operations (save/load/replay)
- Verify responsive behavior at extreme window sizes

## Files to Modify/Create

### Modify Existing Files:
- `viz/visualizer_main.gd` - Performance fixes, architecture improvements
- `viz/visualizer_main.tscn` - Layout changes, responsive design
- `viz/map/map_view.gd` - Caching, rendering optimizations
- `viz/metrics/metrics_panel.gd` - Data limiting, overlay fixes
- `viz/market/market_panel.gd` - Filter caching
- `viz/factions/factions_panel.gd` - Performance optimizations

### Create New Files:
- `viz/controllers/map_view_controller.gd`
- `viz/controllers/panel_controller.gd` 
- `viz/controllers/state_controller.gd`
- `viz/ui/update_throttler.gd`
- `viz/ui/toast_manager.gd`

## Success Criteria
1. **Performance**: Smooth 60fps with 1000+ agents at 1x zoom
2. **Responsive**: Works well on 1024x768 to 4K displays
3. **Usability**: Intuitive controls, clear visual feedback
4. **Maintainability**: Clean architecture, easy to extend
5. **Stability**: No memory leaks, proper cleanup

## Notes
- Focus on Phase 1 (Performance) first as it impacts user experience most
- Maintain backward compatibility with save/replay files
- Keep existing functionality intact while improving architecture
- Test thoroughly at each phase before proceeding to next

This is a significant refactor that will dramatically improve the visualizer's usability and performance. Take it methodically, testing each improvement before moving to the next phase.