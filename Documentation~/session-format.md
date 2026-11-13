# The session folder (format 1)

One session is one folder. The recorder writes it, the speech-to-text step adds two files to it, and whoever reads it (a person, an AI agent, a script of yours) needs nothing else. This page is the promise between those three: a new recorder for another engine, or another speech-to-text step, fits in by keeping to it.

```
20261112-080012/              named by the local time the session began
  trail.txt                   what happened, one line each
  mic0.wav  mic2.wav          the voice, one file for each real microphone (the number is Windows' device number)
  shots/
    s000012.5.jpg             pictures of the game window; the number is the session's seconds when it was taken
  save_start/<folder name>/   copies of the folders the project asked for, at the start...
  save_end/<folder name>/     ...and at the end
  words.txt                   added by notes.py: what was said
  notes.md                    added by notes.py: each remark beside its place, clicks and pictures
```

## trail.txt

UTF-8, no byte order mark. Each line is

```
seconds <tab> kind <tab> words
```

`seconds` counts from the start of the session with two decimals and a point (`12.50`). `kind` is one word. `words` never contain a tab or a line break. Lines are in the order things happened.

| kind | words | written |
|---|---|---|
| `format` | `1` | first line |
| `start` | local date and time, `2026-11-12 08:00:12` | once |
| `app` | product name and version, then ` (editor)` in the editor | once |
| `unity` | the Unity version | once |
| `screen` | `2560x1440` | once |
| `language` | the language spoken, as a code, if the project set one | once or never |
| `words` | the game's own words, if the project set any | once or never |
| `mic` | device number, two spaces, device name; `  (skipped)` after a device left alone | once for each sound input |
| `place` | where the player is: `Recorder.Where`, or the scene's name | whenever it changes |
| `click` | `press "<label>" <path>  @x,y` on a control, `touch "<label>" <path>  @x,y` on other interface, `outside  @x,y` elsewhere | each left click |
| `key` | the key's name | each press of a key the project watches |
| `at` | `x y z yaw <degrees>` of the main camera | when it has moved 1 m or turned 20°, at most once a second |
| `hitch` | milliseconds | each frame longer than 50 ms |
| `trouble` | what went wrong (a microphone that would not open, a full disk) | when it happens |
| `end` | `<n> shots` | last line; missing if the game crashed |
| anything else | the game's own lines, from `Recorder.Mark(kind, words)` | when the game says |

In a `click` line the label is the text on the thing clicked, without rich-text tags and cut to 24 characters, and the path is its name with up to two parents (`Canvas/Menu/StartButton`). `x,y` are pixels from the top left of the game view, so in a build they are the click's place in the pictures.

A reader should pass over kinds it does not know.

## mic&lt;n&gt;.wav

16 kHz, 16 bits, one channel, a 44-byte header. Sample 0 is second 0 of the trail, within a few hundredths. If the game crashed, the header still says "0 bytes of sound" although the sound is there: read past the header by hand, as notes.py does.

## shots/

JPEG, the size of the game window. A red ring with a dark rim marks the mouse pointer when it was free (not locked to the view) and over the window. The filled red dot in the top left corner is PlayNotes' own sign that it was recording, not the pointer. In the editor the picture is of the whole editor window.

## words.txt

```
   12.5-   15.0  this button looks cheap
   18.2-   18.9  nice (?)
```

Start and end in the session's seconds, then the words, one remark to a line. `(?)` after a remark means the speech model was unsure of it (a word or two in under a second, or a faint voice).

## notes.md

For reading, by people and agents; not meant to be parsed. A short head (date, game, length, the microphone used), then one section for each remark:

```
## 12.5-15.0 "this button looks cheap"
- place: Workbench / order 12 → Workbench / filling
- just before: (0.6 s into the remark) press "Pour" Canvas/Bench/PourButton  @1040,610 / (0.8 s before) key Tab / (2.1 s before) press "Start" Canvas/Menu/StartButton  @812,280
- the game noted: (3.0 s before) wave: 3 began
- pictures: shots/s000011.0.jpg , shots/s000012.5.jpg , shots/s000014.0.jpg
- long frames: 83 ms
```

- **place** is where the player was when the remark began, then each place it changed to before the remark ended.
- **just before** is the player's own clicks and keys from eight seconds before the remark to its end: the newest five, the newest first, each with how long before the remark it happened or how far into it. A cut says how many were left out.
- **the game noted** is the game's own lines (`Recorder.Mark`) in the same time, kept apart because they are events and not things the player did. The line is left out when there are none.
- **pictures** are all those taken from a second and a half before the remark to half a second after it, six at most. When none was taken then, the nearest one is given and says how far off it is.
- **long frames** are the `hitch` lines from three seconds before the remark to its end.
