# ShareX.ImageEditor Port Status

Last updated: 2026-05-08

## Port Source
- ShareX.ImageEditor commit: `9ca8f54fd` (latest local ShareX commit touching ShareX.ImageEditor as of 2026-05-08)
- XerahS submodule last synced to: `9ca8f54fd` (pending submodule commit)
- XerahS submodule current HEAD before this session: `360eeab`

## Port Activity (2026-05-08)

- Previous recorded ShareX sync: `c98176bf1`
- Latest upstream ShareX commit touching ShareX.ImageEditor: `9ca8f54fd`
- Result: caught up through `9ca8f54fd` in the working tree
- Method: mapped sync from the local `C:\Users\liveu\source\repos\ShareX Team\ShareX\ShareX.ImageEditor` checkout into `src/ShareX.ImageEditor`, followed by XerahS host integration fixes
- Risk: high; the range spans smart padding layout, cursor handling, SKBitmap migration, DPI-aware zoom, annotation toolbar features, curved arrows and lines, insert image flow, quick crop, copy behavior, and bitmap dimension handling

### Commits reviewed and ported

- `f98e7dbdc` Remove background color from OverlayCanvas in EditorView.axaml
- `4882e8dc9` Refactor smart padding logic and improve overlay canvas layout updates
- `5ce18a230` Refactor smart padding handling and improve state management in image editor
- `a84f2f7f8` Simplify SmartPaddingColor logic by removing unnecessary sampling conditions
- `e9673afbd` Enable clipping for SmartPaddingBorder to improve rendering behavior
- `ca628eed3` Enhance WindowsDesktopWallpaperService to retrieve wallpaper from registry cache and improve error handling
- `f92e16aa4` Refactor WindowsDesktopWallpaperService to update MaxWallpaperPath constant and clean up using directives
- `a1a8174dc` Refactor code to ensure consistent formatting and remove unnecessary comments across multiple files
- `b8a330217` Enhance cursor synchronization for active tools in the image editor
- `dedde99d7` Refactor cursor handling for hovered shapes and active tools in the image editor
- `a78eac137` Improve image rendering and synchronization in the editor
- `53a2a7e9c` Add aspect ratio anchor support to ResizeImageEffect and update related logic
- `18887222b` Implement DPI scaling for zoom functionality in image editor
- `07bc2f32d` Add support for Ctrl+Enter to insert new lines in text boxes
- `8a6b6ab1a` Refactor image handling to use SKBitmap for improved performance and memory efficiency
- `9d47239ce` Refactor image handling to use SKBitmap for improved performance and memory efficiency in image processing functions
- `e7a301902` Add font family selection support in annotation toolbar and related components
- `332594318` Implement FontFamilyPickerDropdown control for enhanced font selection in annotation toolbar
- `8fadd17e0` Add StringToFontFamilyConverter for font family binding in FontFamilyPickerDropdown
- `4448d0feb` Implement curved segment annotations for Arrow and Line shapes with helper methods for curve management
- `f028be397` Refactor ArrowAnnotation to improve arrow cap geometry calculations and streamline rendering logic
- `6f7f04d7e` Add ArrowStyle support to annotation tools and UI components
- `245adc539` Enhance tool selection logic to include Freehand tool in shape selection and hover state updates
- `847cdb2c7` Enhance selection logic to include FreehandAnnotation in Polyline shape handling
- `2ec1b0713` Refactor ArrowAnnotation and CurvedSegmentHelper to support modern arrow styles and improve curve handling
- `a55bd97c4` Enhance ArrowAnnotation rendering by updating visual properties in AnnotationVisualFactory
- `1f2b20405` Refactor curve point calculations to use quadratic control points for arrow and line annotations
- `d2b3aa3bd` Fix style selectors in ImageEditorStyles.axaml for improved TextBlock specificity
- `612c49a78` Refactor CanSave method to remove unnecessary ImageFilePath check
- `c1308f5e7` Add InsertImageDialog and refactor image insertion logic
- `889273c4a` Refactor copy functionality in EditorView to use CopyAnnotationCommand and update key bindings
- `4472dbea1` Add Quick Crop feature to Image Editor and update related settings
- `ccc87f9e0` Refactor bitmap handling to use pixel size for dimensions in image processing
- `9ca8f54fd` Add SkiaSharp.Views.WindowsForms package and enhance image loading functionality

### Files added

- `src/ShareX.ImageEditor/Core/Annotations/ArrowStyle.cs`
- `src/ShareX.ImageEditor/Core/Annotations/Shapes/CurvedSegmentHelper.cs`
- `src/ShareX.ImageEditor/Core/Annotations/Shapes/ICurvedSegmentAnnotation.cs`
- `src/ShareX.ImageEditor/Presentation/Controls/ArrowStylePickerDropdown.axaml`
- `src/ShareX.ImageEditor/Presentation/Controls/ArrowStylePickerDropdown.axaml.cs`
- `src/ShareX.ImageEditor/Presentation/Controls/FontFamilyPickerDropdown.axaml`
- `src/ShareX.ImageEditor/Presentation/Controls/FontFamilyPickerDropdown.axaml.cs`
- `src/ShareX.ImageEditor/Presentation/Converters/StringToFontFamilyConverter.cs`
- `src/ShareX.ImageEditor/Presentation/ViewModels/InsertImageDialogViewModel.cs`
- `src/ShareX.ImageEditor/Presentation/Views/EditorView.ImageInsert.cs`
- `src/ShareX.ImageEditor/Presentation/Views/InsertImageDialogView.axaml`
- `src/ShareX.ImageEditor/Presentation/Views/InsertImageDialogView.axaml.cs`

### Files updated

- Updated mapped upstream changes across `Core/Abstractions`, `Core/Annotations`, `Core/Editor`, `Core/ImageEffects`, `Hosting`, `Presentation/Controllers`, `Presentation/Controls`, `Presentation/Rendering`, `Presentation/Theming`, `Presentation/ViewModels`, and `Presentation/Views`.
- Updated RegionCapture's toolbar adapter for the new font family and arrow style members.
- Updated XerahS UI integration hooks for editor application title and annotation snapshot persistence.
- Updated tests for the renamed classic arrow-head multiplier constant and Step undo numbering behavior.

### Adaptations kept for XerahS

- Preserved the submodule `src/ShareX.ImageEditor` layout.
- Preserved XerahS annotation persistence hooks on `EditorCore` and `EditorView`.
- Preserved XerahS `StepTailStyle` support and tail-style picker contract.
- Preserved embedded-editor start screen suppression in XerahS host flows.
- Kept SkiaSharp package usage aligned with XerahS central package management instead of adopting ShareX root package changes directly.
- Fixed Step annotation numbering after undo/redo by syncing the next number from the restored annotation layer.

### Verification

- `dotnet build ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj -m:1 /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors on 2026-05-08.
- `dotnet build -m:1 /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors on 2026-05-08.
- `dotnet test tests\XerahS.Tests\XerahS.Tests.csproj --filter "FullyQualifiedName~StepAnnotation_AfterUndo_UsesNextVisibleNumber" -m:1 /nodeReuse:false /p:UseSharedCompilation=false --no-restore` passed on 2026-05-08.

## Port Activity (2026-04-26)

- Previous recorded ShareX sync: `c6e3c5260`
- Latest upstream ShareX commit touching ShareX.ImageEditor: `c98176bf1`
- Result: caught up through `c98176bf1` in the working tree
- Method: mapped sync from the local `C:\Users\liveu\source\repos\ShareX Team\ShareX\ShareX.ImageEditor` checkout into `src/ShareX.ImageEditor`, followed by XerahS host integration fixes
- Risk: high; the range spans start-screen flow, recent image files, editor mode flags, Skia/Avalonia API migration, cursor capture, dialog behavior, and image resizing behavior

### Commits reviewed and ported

- `5e7e59a93` Async emoji preview and picker loading
- `c61d57887` Upgrade packages and migrate to Avalonia APIs
- `44dc953b3` Cleanup usings, format SKPaint, and minor UI fixes
- `1ca713cf5` Handle Escape to close dialogs and panels
- `30ff71fff` Use SKSamplingOptions and SKImage for resampling
- `4a8247fed` Use ReflectionBinding for SelectEmojiCommand
- `2598bca20` Add CopyContext and fix bitmap clipboard checks
- `6b172822c` Improve emoji picker init, focus & shortcut handling
- `1cbf1da91` Centralize interaction cursor overrides
- `64b9dfc44` Use interaction capture layer for cursor handling
- `43964127e` Add start screen, URL/clipboard loading, recent files
- `c79ae99fa` Disable sample fallback; compact start UI
- `0ed42d55e` Embed URL input and status into Start Screen
- `372b894c3` Restyle start-screen status panel
- `7b1b10cf4` Rework start screen URL panel layout
- `1e0939db0` Add ShowEditorDialog overload and modern editor flow
- `275fc301a` Refactor editor mode into granular UI flags
- `e5333a2bb` Update MainViewModel UI flags in editor
- `7722a3ef4` Assign ImageFilePath inside VM branch
- `45ee36513` Remove theme selection functionality from EditorView and MainViewModel
- `5134ddf01` Fix resx
- `09b456fa2` Added "Remember window state" option
- `a07b88bcf` Refactor ConfirmationDialogView layout for improved content structure
- `c66d92c6f` Add custom ItemsPanel to RecentFiles ItemsControl for improved layout
- `c11df0504` Refactor NewImageDialogView layout for improved styling and consistency
- `3479bed9c` Update ConfirmationDialogViewModel title and enhance layout for consistency
- `e714392b5` Enhance NewImageDialogView with solid background option and update layout for clarity
- `7404c68ed` Enhance EditorInputController to support crosshair cursor interaction for specific tools
- `998e1b7c7` Add RefreshSpotlightOverlay call on new image request and bitmap load
- `f3bebe613` Implement recent image file management in the annotation toolbar
- `c98176bf1` Add aspect ratio handling to image resizing functionality

### Files added

- `src/ShareX.ImageEditor/Presentation/ViewModels/StartScreenDialogViewModel.cs`
- `src/ShareX.ImageEditor/Presentation/Views/StartScreenDialogView.axaml`
- `src/ShareX.ImageEditor/Presentation/Views/StartScreenDialogView.axaml.cs`

### Files updated

- Updated mapped upstream changes across `Core/Abstractions`, `Core/Annotations/Effects`, `Core/Editor`, `Core/ImageEffects`, `Hosting`, `Presentation/Controllers`, `Presentation/Controls`, `Presentation/Emoji`, `Presentation/Theming`, `Presentation/ViewModels`, and `Presentation/Views`.
- Updated XerahS host integration call sites for the new `MainViewModel` UI flags.
- Updated RegionCapture's toolbar adapter implementation for recent-image members added upstream.
- Updated the ViewLocator test for the current confirmation dialog constructor.

### Adaptations kept for XerahS

- Preserved the submodule `src/ShareX.ImageEditor` layout.
- Preserved XerahS annotation persistence hooks on `EditorCore`.
- Preserved XerahS tail-style options and icon constants used by the tail-style picker.
- Preserved the submodule library license header wording for synced `.cs` files.
- Mapped removed `TaskMode` / `ShowTaskModeButtons` host usage to `ShowFileMenu`, `ShowTaskButtons`, `UseContinueWorkflow`, `ShowBottomToolbar`, and `ShowStartScreen`.

### Verification

- `dotnet build ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj -m:1 /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors on 2026-04-26.
- `dotnet build src\desktop\XerahS.sln -m:1 /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors on 2026-04-26.

## Port Activity (2026-04-09)

- Previous recorded ShareX sync: `9bad8ddd9`
- Latest upstream ShareX commit touching ShareX.ImageEditor: `c6e3c5260`
- Result: caught up through `c6e3c5260` in the working tree
- Method: semantic port from the local `C:\Users\liveu\source\repos\ShareX Team\ShareX\ShareX.ImageEditor` checkout, not a blind cherry-pick

### Commits reviewed and ported

- `53e28977a` Improve text annotation wrapping, sizing and layout
- `eccbb2602` Use measured text size when finalizing annotation
- `87dd609b3` Add emoji picker, catalog and renderer
- `02bc3434e` Add EmojiAnnotation and editor/visual support
- `9374f0c46` Add rotation support and interactive emoji render
- `d00234b84` Update emoji catalog and picker ViewModel
- `0fc3865c9` Refactor Emoji picker layout and styles
- `c6e3c5260` Improve emoji picker UI and modal close behavior

### Files added

- `src/ShareX.ImageEditor/Assets/emoji-catalog.json`
- `src/ShareX.ImageEditor/Core/Annotations/Shapes/EmojiAnnotation.cs`
- `src/ShareX.ImageEditor/Presentation/Controls/EmojiPreviewImage.cs`
- `src/ShareX.ImageEditor/Presentation/Emoji/EmojiCatalogEntry.cs`
- `src/ShareX.ImageEditor/Presentation/Emoji/WindowsEmojiBitmapRenderer.cs`
- `src/ShareX.ImageEditor/Presentation/ViewModels/EmojiPickerDialogViewModel.cs`
- `src/ShareX.ImageEditor/Presentation/Views/EmojiPickerDialogView.axaml`
- `src/ShareX.ImageEditor/Presentation/Views/EmojiPickerDialogView.axaml.cs`

### Adaptations kept for XerahS

- Preserved the submodule `src/ShareX.ImageEditor` layout instead of mirroring the ShareX repo root.
- Kept XerahS-specific annotation model members such as `StepTailStyle` while adding the upstream emoji discriminator.
- Ported controller and view behavior into existing Avalonia files instead of replacing them wholesale.
- Updated text editing, emoji insertion, emoji rotation, and interactive emoji resize behavior without discarding local host integration.

### Verification

- `dotnet build ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj -m:1` passed with 0 errors on 2026-04-09. Existing SkiaSharp deprecation warnings remain in the project.
- `dotnet build src\desktop\XerahS.sln -m:1 /nodeReuse:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors on 2026-04-09.

## Port Activity (2026-04-08)
The following ShareX commits were ported in this session:

### High Risk — Interaction Cache System (a9d829b9f + 10433d15e)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| BaseEffectAnnotation.cs | a9d829b9f | Core/Annotations/Effects/ | High | ✅ Ported | Added GetInteractionCacheKey, CreateInteractionCacheBitmap, UpdateEffectFromInteractionCache, UpdateEffectFromAlignedCache |
| BlurAnnotation.cs | a9d829b9f + 10433d15e | Core/Annotations/Effects/ | High | ✅ Ported | Added CreateBlurredSourceCache, interaction cache methods |
| PixelateAnnotation.cs | a9d829b9f | Core/Annotations/Effects/ | High | ✅ Ported | Added CreatePixelatedSourceCache, interaction cache methods |
| HighlightAnnotation.cs | a9d829b9f | Core/Annotations/Effects/ | High | ✅ Ported | Added CreateHighlightedSourceCache, ApplyHighlightToBitmap, interaction cache methods |
| MagnifyAnnotation.cs | a9d829b9f | Core/Annotations/Effects/ | High | ✅ Ported | Added interaction cache methods, refactored to UpdateEffectCore |
| EditorView.CoreBridge.cs | a9d829b9f | Presentation/Views/ | High | ✅ Ported | Added _cachedEffectPreview*, TryUpdateCachedEffectVisual, EnsureEffectPreviewCache, ClearEffectPreviewCache, ClearInteractiveEffectPreviewCache, UpdateInteractiveEffectVisual |
| EditorInputController.cs | a9d829b9f | Presentation/Controllers/ | Medium | ✅ Ported | UpdateEffectVisual now delegates to _view.UpdateInteractiveEffectVisual |
| AnnotationEffectVisualUpdater.cs | a9d829b9f | Presentation/Rendering/ | Medium | ✅ Verified | Already had ApplyEffectBrush and UpdateEffectVisual methods |

### Medium Risk — Spotlight Overlay (86369123f)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| SpotlightControl.cs | 86369123f | Presentation/Controls/ | Medium | ✅ Ported | Simplified to selection shell only; darkening overlay moved to SpotlightOverlayControl |
| SpotlightOverlayControl.cs | 86369123f | Presentation/Controls/ | High | ✅ Ported | NEW file — renders darkening overlay via shared SKCanvasControl |
| EditorView.axaml | 86369123f | Presentation/Views/ | High | ✅ Ported | Added SpotlightOverlayControl to canvas |
| EditorView.CoreBridge.cs | 86369123f | Presentation/Views/ | High | ✅ Ported | Added RefreshSpotlightOverlay() method |
| EditorView.ToolbarHandlers.cs | 86369123f | Presentation/Views/ | Medium | ✅ Ported | ApplySelectedEffectStrength now calls RefreshSpotlightOverlay() |
| EditorInputController.cs | 86369123f | Presentation/Controllers/ | Medium | ✅ Ported | 3× spotlightControl.InvalidateVisual() replaced with _view.RefreshSpotlightOverlay() |

### Medium Risk — Core SKBitmap Reuse (dc794cd6b)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| EditorView.CoreBridge.cs | dc794cd6b | Presentation/Views/ | Medium | ✅ Ported | OnRequestUpdateEffect uses _editorCore.SourceImage when available; temporarySource disposed in finally |

### Medium Risk — Host Copy/Save Handlers (9bad64d52)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| MainViewModel.cs | 9bad64d52 | Presentation/ViewModels/ | Medium | ✅ Ported | Added HasHostCopyHandler, HasHostSaveHandler, HasHostSaveAsHandler flags |
| AvaloniaIntegration.cs | 9bad64d52 | Hosting/ | Medium | ✅ Ported | Set host handler flags after wiring events |
| EditorView.axaml.cs | 9bad64d52 | Presentation/Views/ | Medium | ✅ Ported | OnCopyImageRequested handler with SkiaSharp snapshot → clipboard; early returns for host handlers |

### Low Risk — Use ImageFilePath Instead of LastSavedPath (879f2b5e1)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| MainViewModel.cs | 879f2b5e1 | Presentation/ViewModels/ | Low | ✅ Ported | Removed _lastSavedPath field; CanSave() uses ImageFilePath |
| EditorView.axaml.cs | 879f2b5e1 | Presentation/Views/ | Low | ✅ Ported | OnSaveRequested/OnSaveAsRequested use ImageFilePath |
| EditorWindow.axaml.cs | 879f2b5e1 | Presentation/Views/ | Low | ✅ Ported | Removed LastSavedPath assignment |
| AvaloniaUIService.cs | 879f2b5e1 | Services/ | Low | ✅ Ported | Use ImageFilePath instead of LastSavedPath |
| MainViewModelHelper.cs | 879f2b5e1 | Services/ | Low | ✅ Ported | All LastSavedPath → ImageFilePath |
| MainWindow.axaml.cs (TEMP2) | 879f2b5e1 | Views_TEMP2/ | Low | ✅ Ported | LastSavedPath → ImageFilePath |
| EditorCloseConfirmationTests.cs | 879f2b5e1 | Tests/ | Low | ✅ Ported | LastSavedPath removed from test |

### Medium Risk — Tool-Specific Shape Selection and Hover (846eee26a)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| EditorSelectionController.cs | 846eee26a | Presentation/Controllers/ | Medium | ✅ Ported | Added GetControlToolType helper; updated hit-testing logic |
| EditorInputController.cs | 846eee26a | Presentation/Controllers/ | Medium | ✅ Ported | Tool-specific filtering for annotation creation |

### Low Risk — Reset IsDirty After Saving (f7e4029b1)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| EditorView.axaml.cs | f7e4029b1 | Presentation/Views/ | Low | ✅ Ported | Added vm.IsDirty = false in OnSaveRequested and OnSaveAsRequested |
| AvaloniaUIService.cs | f7e4029b1 | Hosting/ | Low | ✅ Ported | Added vm.IsDirty = false after setting ImageFilePath |

### Low Risk — Disabled ContentPresenter Style (64ab3590d)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| ImageEditorStyles.axaml | 64ab3590d | Presentation/Styles/ | Low | ✅ Ported | Added disabled ContentPresenter style |

### Medium Risk — Text Editor KeyUp and Caret Fix (59f48dfba)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| EditorInputController.cs | 59f48dfba | Presentation/Controllers/ | Medium | ✅ Ported | Changed KeyDown → KeyUp; added Dispatcher.UIThread.Post for caret reset |

### Low Risk — Preserve Scroll Offset When Focusing Text Box (bbfd59cd8)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| EditorInputController.cs | bbfd59cd8 | Presentation/Controllers/ | Low | ✅ Ported | Added Dispatcher.UIThread.Post with preserved scroll offset |

### Medium Risk — Snap Rotation to 45° with Shift (adb34c82b)
| File | ShareX Commit | XerahS Location | Risk | Status | Notes |
|------|--------------|-----------------|------|--------|-------|
| EditorSelectionController.cs | adb34c82b | Presentation/Controllers/ | Medium | ✅ Ported | Shift key snaps rotation to 45° increments |
| EditorSelectionController.cs | adb34c82b | Presentation/Controllers/ | Medium | ✅ Ported | Hover outline fix: TextBox → OutlinedTextControl cast |

## Remaining ShareX Commits (not yet reviewed)
No remaining ShareX.ImageEditor commits were pending beyond `c6e3c5260` at the time of this catch-up.

## Notes
- All ShareX.ImageEditor code uses WPF; XerahS uses Avalonia + SkiaSharp
- Interaction cache system (a9d829b9f + 10433d15e) was ported together as they are interdependent
- Spotlight overlay (86369123f) was already partially ported before this session (SpotlightOverlayControl existed)
