# Annotation capture

User request: add a mode that holds a capture in memory for pen, line and arrow
annotations, with color and stroke width controls. Save button or Ctrl+S saves
the final image to the existing selected folder and copies its full path.

## Approach

Use the existing WinForms/GDI+ stack and save pipeline. Add a separate editor
window after selection rather than extend the full-desktop selector with an
editing toolbar. This keeps the selector small and frees the desktop buffer
while editing. No added packages. Original instant capture remains available.

Implementation defaults: Ctrl+Shift+F9 is instant capture; Ctrl+Shift+F10 and a
new Capture & annotate tray command open the editor. Default pen is red, 3 px;
width ranges from 1 to 32 original-image pixels. Use a native ColorDialog.
Provide Undo/Ctrl+Z for correcting strokes. Preview fits the window, while saved
PNG dimensions remain those of the original capture. Resizing must not move
annotations relative to the image.

No disk output or clipboard writes before Save. Closing the editor, Escape or
Discard immediately discards the unsaved capture; save errors leave the editor available for retry. After
a successful image save, copy its path and close the editor. Clipboard failure
retains the PNG and reports its path, as in instant mode. Do not close or replace
the user's currently running tray app while delivering the update.

No text, shape selection/editing, eraser, redo, persistence of unsaved work,
or extra formats. Reuse all existing capture, settings and save contracts.
