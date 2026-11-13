# Changelog

All notable changes to this package are written here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the versions follow [Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-11-12

The first version: what one game's developer had been using every day, taken out of the game and made to stand alone.

### Added

- A session started and stopped with one key (F8): every real microphone to its own file, pictures of the game's own window (faster while a voice is heard, and after each click, with the pointer ringed), and a trail of place, clicks with the words on them, watched keys, camera position and long frames.
- `PlayNotes.Recorder`: `Where`, `Mark`, `AddSnapshotFolder`, `Begin`, `Stop`, `Toggle`, `IsRecording`, `IsAvailable`, `SessionFolder`, `NoteClickAt`, `Status`, and the events `Began` and `Stopped`.
- Asleep by default outside the editor: a build wakes only with `playnotes.on` beside the exe, written for development builds and taken away from others; `PLAYNOTES_DISABLED` compiles everything out.
- Project Settings > PlayNotes, kept in `ProjectSettings/PlayNotesSettings.json`.
- Names of clicked things read from uGUI (Text, TextMeshPro) and UI Toolkit; both input systems.
- `Transcribe~/notes.py`: speech to text on the PC (faster-whisper through uv), the clearest microphone picked, each remark laid beside its place, clicks and pictures.
- `Skill~/playnotes`: how an AI coding agent should read a session, installable into a project from the Tools menu.
- The session folder's layout written down as format 1.
- No dialog that waits for a click anywhere in the package (the editor answers with a Console line and a fading notice), so an editor driven by a script or an agent is never left waiting; a test keeps it so.

### Known limits

- Windows only. On other platforms the package compiles to nothing.
- In the editor the pictures are of the whole editor window.
- Clicks on things in the 3D world are noted as `outside`; use `Recorder.Mark` to name them.
