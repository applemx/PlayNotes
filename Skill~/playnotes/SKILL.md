---
name: playnotes
description: Read the developer's voice notes from playing their own Unity game (PlayNotes sessions, recorded with the PlayNotes key, F8 unless changed) and turn them into a checked list of what to change. Use when the user says they left play notes or voice notes, asks you to read or look at their play notes or a PlayNotes session, or has just played the game and says something like "have a look", 「見て」 or 「メモ見て」.
---

# Reading play notes

The developer played their game and talked while playing. PlayNotes kept each session in a folder of its own:

| In the folder | What it is |
|---|---|
| `mic<n>.wav` | their voice, one file for each microphone that was recorded |
| `shots/s<seconds>.jpg` | pictures of the game window, named by the second they were taken |
| `trail.txt` | what happened, one line each: `seconds <tab> kind <tab> words` |
| `save_start/`, `save_end/` | copies of the game's save files from before and after, if the project asked for them |
| `words.txt`, `notes.md` | made by step 1 below |

Where this project's sessions are is the one line of `project.txt` beside this file (`sessions: ...`). Pass that path to the script as it is; it puts the user's home in place of `%USERPROFILE%` itself. If the folder does not exist, no session has been recorded on this PC yet: say so and stop.

## 1. Write the voice out

From the project's folder (or give the script's full path). First ask what a run would do; this writes and fetches nothing:

```
uv run .claude/skills/playnotes/notes.py "<the sessions folder>" --check
```

It says how many sessions wait and whether the speech model is on this PC. If it is not, tell the developer before going on: the first run downloads about 1.6 GB and can take a long while (`--model small` is a third of that, with more mistakes; `--model "<folder>"` uses a model they already have). Then the same command without `--check`:

```
uv run .claude/skills/playnotes/notes.py "<the sessions folder>"
```

It does only the sessions that have no `notes.md` yet. Nothing the developer said or showed leaves the PC. It runs at low priority and on the processor alone, so the game and the editor can stay open; the speech takes about as long to write out as it took to say.

- `uv` missing: say that it comes from https://docs.astral.sh/uv/ and stop. Do not install it yourself unasked.
- Many sessions waiting and they only want the last: `--latest`, or give one session's folder.
- `nothing to write out`: someone has written them out already. Read the `notes.md` of the sessions since the last list you gave back; if you cannot tell which, ask.
- `no speech` on every microphone of a session: tell the developer that their voice was not picked up, and which inputs were found (the `mic` lines of `trail.txt`).

## 2. Read

Read `notes.md` of each new session. Under each remark:

- **place**: where the game said the player was. `A → B` means it changed while they spoke.
- **just before**: the player's own clicks and keys from eight seconds before the remark to its end, the newest five first, each with how long before the remark it was (or how far into it).
- **the game noted**: lines the game wrote itself in that time. They are events, not things the player did.
- **pictures**: every picture from a second and a half before the remark to its end (six at most). One marked `(taken 2.6 s before the remark)` shows the screen as it was then, not as they spoke.

**Open the pictures before you decide what a remark is about.** Do not go by the words alone.

- The **red ring** is the mouse pointer. The filled **red dot in the top left corner** is not: it is PlayNotes' sign that it was recording, and for the first seconds a line of words stands beside it ("Play notes: recording", with the project's own key). 
- A click in the trail does not promise a ring: the pointer may have been hidden or locked, or a test pressed the button. Then find the thing by the click's `@x,y`, which is its place in the picture, counted from the top left.
- "This", "here", 「これ」, 「ここ」 usually mean what the ring rests on, or what was pressed a moment before. **What the remark itself says outranks timing**: if only one thing on the screen fits the words ("the text is too long", "this green"), that is the one, even when something else was pressed after it. Say which you took it for, name the runner-up, and ask for a yes. If nothing settles it, list the candidates and ask; do not pick.
- A remark with no subject ("a calmer colour would be better") may carry on from the remark before it rather than be about the last thing pressed. When the two readings differ, give both.
- A remark may name its subject and still leave open what is wrong with it ("the green is harsh"). Give your best reading and one change, and name the other reading.
- A remark marked `(?)` was too short or faint for the speech model to be sure of. List it, say that it is unsure, and do not act on it.

How to read a `click`: `press "Start" Canvas/Menu/StartButton  @640,180` is a button or other control, with the words on it (cut at 24 characters) and where it sits in the scene; `touch` is interface that is not a control; `outside` is no interface at all (the 3D world, or nothing).

Other kinds in `trail.txt`: `key`; `at` (camera position and heading); `hitch` (a frame longer than 50 ms, in ms; worth mentioning only when a remark is about stutter or lag); `mic` (the inputs found: `(skipped)` is a loopback device left alone, and an input listed without a `.wav` could not be opened or has had its recording deleted); `trouble` (something PlayNotes could not do); `words` (the game's own words given to the speech model). Any other kind is a line the game wrote through `Recorder.Mark("kind", ...)`; search the code for the kind to see what it means (a space in a kind is written `_`).

A session with no speech still has its pictures and its trail. Say that it was silent; do not invent remarks.

## 3. Give the list back before changing anything

Answer in the developer's language, under one heading for each session, one entry for each remark in the order they were made. Each entry has four parts, named in their language, on separate lines when one line would not read well:

> **where** (place, picture) · **what they said** (their words) · **how I read it** · **what I would change** (or "nothing", with the reason)

Remarks that are not about the game are left out; if there were any, say how many.

After the list, only where there is something to put there:

- **Disagreements**: a remark that says the opposite of an earlier one, both quoted ("earlier: ... / now: ..."), for them to choose.
- **Seen but not said**: something plainly wrong in the pictures that they did not mention (an error on screen, a label that is missing). If its cause is quick to find, say what you found (a build writes `Player.log` in the folder above the sessions folder); do not fix it unasked.
- **Checked / not checked**: what you looked at in the code or the pictures, and what you are going by without having checked.

Then wait for the corrections. If the developer has already told you to carry on without asking, still write the list first, then act only on the entries whose reading is settled; leave the open ones (two candidates, an unsure remark, a disagreement) and say that you left them.

## The rules that make this work

1. **A remark is a reaction, not an order.** It was said in a second, in the middle of play. "This is ugly" does not say what would be beautiful. Work out what they were looking at and what they felt, then decide what to change.
2. **Speech-to-text is wrong about the game's own words.** Names and terms come out as other words that sound the same. Read by the context and the picture; mark a word you are unsure of with (?). When the same word keeps coming out wrong, suggest adding it to "The game's own words" in Project Settings > PlayNotes.
3. **When a remark disagrees with an earlier one, show both** and let them choose. Never quietly follow the newest, and never quietly keep the old.
4. **Check a claimed bug before fixing it.** Find it in the code first. If the game is doing what it was built to do, say that; the remark may then be about the design, not a bug. To reproduce one from `save_start`, never put those files over the developer's real save without asking: copy the real ones aside first, or use a spare save slot.
5. **If you think a change would make the game worse, say so with the reason** before building. They decide.
6. **Praise counts.** Note what they liked, so that it is not changed by accident while fixing something beside it. Praise marked `(?)`, or praise with nothing to pin it to, is noted as that and nothing more.
7. **The same spot missed twice** (they complain again after your fix): stop guessing and show two or three options side by side.
8. **Say what you checked and what you did not**, in the list and again after any change. "Changed and looked at in the game" and "changed, not yet looked at" are different reports.

## Their voice is theirs

- The recordings hold a person's voice and their room. Use them for the game only, and do not copy the `.wav` files anywhere.
- When the list has been corrected and nothing needs writing out again, offer to delete the recordings of the sessions you read, one session at a time:

  ```
  uv run .claude/skills/playnotes/notes.py "<that session's folder>" --delete-audio
  ```

  For a session already written out this only deletes its `.wav` files; the words, the pictures and the trail stay. Give a session's folder, not the folder of all sessions, or every session's recordings go.
