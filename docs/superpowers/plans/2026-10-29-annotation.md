# Annotation Capture Implementation Plan

> **For agentic workers:** Execute inline using superpowers:executing-plans.

**Goal:** Add capture -> annotate -> explicit save alongside instant capture.
**Architecture:** The existing tray owns both capture commands and a shared
save/copy callback. A WinForms editor owns a preview canvas and stroke list;
one renderer draws the same strokes for preview and original-size PNG export.
**Tech Stack:** .NET 10, WinForms, System.Drawing; no additional dependencies.
**Spec:** ../specs/2026-10-29-annotation-design.md

## Global Constraints

- Preserve Ctrl+Shift+F9 instant capture and existing folder settings.
- No saves or clipboard writes before Save/Ctrl+S in annotation mode.
- Preserve image dimensions and point/width alignment across preview resizing.
- Save failure retains the editable capture; closing discards without confirmation.
- Compiler/analyzer first; stop if unsuccessful. Compile only to deliver update.

## Review Focus

- Scaled/letterboxed preview maps mouse positions to source-image pixels.
- Arrow direction and width/color are preserved in exported PNG.
- Pen clicks, reverse drags, and mouse release outside the canvas remain valid.
- Ctrl+S while dragging commits the pending stroke exactly once.
- Editor buffers are released; existing running app is not terminated/replaced.

## Tasks

- [x] Add rendering/coordinate assertions to checks/Program.cs.
- [x] Add AnnotationStroke.cs and AnnotationCanvas.cs for drawing and export.
- [x] Add AnnotationEditor.cs with tool/color/width/undo/save controls and shortcuts.
- [x] Copy the existing capture/save code into shared helpers before adapting
  TrayApplication.cs; register F10 via HotkeyWindow.cs; adapt overlay hint copy.
- [x] Update README.md, compile with warnings as errors, and check output.
- [x] Review changes and deliver to dist-update if the existing dist app is running.

## Execution record

- User requested the annotation feature directly; proceed within the existing
  C# WinForms app and preserve working instant capture.
- Existing app is running from dist; deliver update separately to avoid
  interrupting the user's capture session or overwriting loaded binaries.
- First compiler check identified WFO1000 designer metadata requirements on
  three runtime canvas properties. Marked them DesignerSerializationVisibility.Hidden.
- Compiler/analyzers: zero warnings/errors after the metadata fix.
- Dependency-free assertion runner: all 33 checks passed (21 existing, 12 new).
- No actual desktop interaction or mixed-DPI UI verification claimed.
- Independent static review: no important findings. Update delivered to
  dist-update; existing distribution was not overwritten or stopped.
- Later user request: removed the discard confirmation; closing, Escape and
  Discard now immediately release the unsaved capture.
