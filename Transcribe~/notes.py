# /// script
# requires-python = ">=3.9"
# dependencies = [
#     "faster-whisper>=1.1.0",
#     "numpy",
# ]
# ///
"""PlayNotes: the voice of a session written out as words.

    uv run notes.py <a session folder, or the folder that holds the sessions>

For each session this picks the microphone that heard the speaker best, writes out what was said (words.txt), and lays
every remark beside the place the player was in, what they had just pressed and the pictures of that moment (notes.md).

Everything runs on this computer; no recording, picture or word is sent anywhere. The first run fetches the speech
model from Hugging Face (about 1.6 GB for the default one) and keeps it in the usual cache; after that the script does
not touch the network at all.

Given the folder that holds the sessions, only those not yet written out are done. Options: --help.
"""
import argparse
import bisect
import glob
import os
import sys
import time

# (the model fetch is the one thing that goes out; it says nothing about the user on the way)
os.environ.setdefault('HF_HUB_DISABLE_TELEMETRY', '1')
os.environ.setdefault('HF_HUB_DISABLE_SYMLINKS_WARNING', '1')

import numpy as np

RATE = 16000
DEFAULT_MODEL = 'large-v3-turbo'

# trail kinds that describe the session itself, not something the player did
BOOKKEEPING = {'format', 'start', 'app', 'unity', 'screen', 'language', 'words', 'mic', 'place', 'at', 'hitch', 'trouble', 'end'}

LABELS = {
    'en': {
        'title': 'Play notes', 'app': 'game', 'length': 'length', 'remarks': 'remarks', 'mic': 'microphone', 'screen': 'screen',
        'pictures_note': 'Pictures are in shots/, named by the second they were taken. A red ring in a picture is the mouse pointer; '
                         'the red dot in the top left corner is only the sign that PlayNotes was recording. '
                         'The @x,y of a click is counted from the top left, as in the pictures.',
        'place': 'place', 'before': 'just before', 'shots': 'pictures', 'hitch': 'long frames', 'none': 'nothing',
        'no_speech': 'No speech was heard in this session.', 'open': '"', 'close': '"',
        'ago': '(%.1f s before)', 'during': '(%.1f s into the remark)', 'earlier': '(taken %.1f s before the remark)', 'later': '(taken %.1f s after it)',
        'unsure': 'unsure: very short or faint, may be misheard', 'game': 'the game noted', 'more': '... and %d earlier',
    },
    'ja': {
        'title': '遊びメモ', 'app': 'ゲーム', 'length': '長さ', 'remarks': '言ったこと', 'mic': 'マイク', 'screen': '画面',
        'pictures_note': '写真は shots/ にあり、名前は撮った秒です。写真の赤い輪はマウスの位置です。'
                         '左上の赤い点は PlayNotes が録音中だった印で、マウスではありません。'
                         'クリックの @x,y は写真と同じく左上から数えます。',
        'place': '場所', 'before': '直前の操作', 'shots': '写真', 'hitch': '長いコマ', 'none': 'なし',
        'no_speech': 'この回では声が聞き取れませんでした。', 'open': '「', 'close': '」',
        'ago': '(%.1f 秒前)', 'during': '(話し始めて %.1f 秒後)', 'earlier': '(この一言の %.1f 秒前の絵)', 'later': '(この一言の %.1f 秒後の絵)',
        'unsure': '確かさ: 低い（短いか小さい声。聞き違いかもしれません）', 'game': 'ゲームの記録', 'more': '… ほかに、それより前の %d 件',
    },
}


def read_wav(path):
    # (past the 44 bytes of its header by hand: a session cut short by a crash has a header that still says 0 bytes)
    with open(path, 'rb') as f:
        f.seek(44)
        raw = f.read()
    return np.frombuffer(raw[:len(raw) // 2 * 2], dtype='<i2').astype(np.float32) / 32768.0


def pick_mic(paths):
    """Every real microphone was kept: the one that heard the speaker best is the one with the most speech standing clearest over its own quiet."""
    from faster_whisper.vad import VadOptions, get_speech_timestamps
    best, best_score, lines = None, -1.0, []
    for p in paths:
        a = read_wav(p)
        name = os.path.basename(p)
        if len(a) < RATE:
            lines.append('%s: too short' % name)
            continue
        spans = get_speech_timestamps(a, VadOptions(min_silence_duration_ms=500))
        if not spans:
            lines.append('%s: no speech (peak %.4f)' % (name, float(np.abs(a).max())))
            continue
        speech = sum(s['end'] - s['start'] for s in spans) / RATE
        mask = np.zeros(len(a), bool)
        for s in spans:
            mask[s['start']:s['end']] = True
        voice = float(np.sqrt(np.mean(a[mask] ** 2)))
        quiet = float(np.sqrt(np.mean(a[~mask] ** 2))) if (~mask).any() else 1e-6
        snr = 20 * np.log10(max(voice, 1e-6) / max(quiet, 1e-6))
        lines.append('%s: speech %.0f s, voice %.4f, quiet %.5f, %.0f dB' % (name, speech, voice, quiet, snr))
        score = speech * max(snr, 1.0)
        if score > best_score:
            best, best_score = p, score
    return best, lines


def read_trail(path):
    rows = []
    if not os.path.exists(path):
        return rows
    with open(path, encoding='utf-8', errors='replace') as f:
        for line in f:
            parts = line.rstrip('\r\n').split('\t')
            if len(parts) >= 3:
                try:
                    rows.append((float(parts[0]), parts[1], parts[2]))
                except ValueError:
                    pass
    return rows


def first(trail, kind, default=''):
    for _, k, w in trail:
        if k == kind:
            return w
    return default


def is_session(folder):
    return os.path.isdir(folder) and (os.path.exists(os.path.join(folder, 'trail.txt')) or bool(glob.glob(os.path.join(folder, 'mic*.wav'))))


def find_sessions(path, again, latest):
    """The sessions to write out, and those already written out (which --delete-audio still has work in)."""
    if is_session(path):
        found = [path]
    else:
        found = sorted(p for p in glob.glob(os.path.join(path, '*')) if is_session(p))
        if latest:
            found = found[-1:]
    todo = [p for p in found if again or not os.path.exists(os.path.join(p, 'notes.md'))]
    return todo, [p for p in found if p not in todo]


def fetch_model(name):
    """The folder a model is in: the one given, the one already in the cache, or one fetched now with its progress showing."""
    if os.path.isdir(name):
        return name
    from faster_whisper import utils
    try:
        # (already here: no word to the network, so the script works with the cable out)
        return utils.download_model(name, local_files_only=True)
    except Exception:
        pass
    print('fetching the speech model %s from Hugging Face. This happens once; the large models are over a gigabyte.' % name, flush=True)
    try:
        # (the library's own fetch shows nothing while it works, and minutes of nothing look like a hang)
        import huggingface_hub
        repo = getattr(utils, '_MODELS', {}).get(name, name)
        return huggingface_hub.snapshot_download(repo, allow_patterns=['config.json', 'preprocessor_config.json', 'model.bin', 'tokenizer.json', 'vocabulary.*'])
    except Exception:
        return utils.download_model(name)


def model_here(name):
    """Whether a model is on this PC already, as a folder or in the cache. Asks nothing of the network."""
    if os.path.isdir(name):
        return True
    try:
        from faster_whisper import utils
        utils.download_model(name, local_files_only=True)
        return True
    except Exception:
        return False


def load_model(name, device):
    from faster_whisper import WhisperModel
    name = fetch_model(name)
    if device == 'auto':
        try:
            import ctranslate2
            device = 'cuda' if ctranslate2.get_cuda_device_count() > 0 else 'cpu'
        except Exception:
            device = 'cpu'
    threads = max(4, (os.cpu_count() or 8) - 4)
    if device == 'cuda':
        try:
            return WhisperModel(name, device='cuda', compute_type='float16'), 'cuda'
        except Exception as e:
            print('the graphics card could not be used (%s); going on with the processor' % e, flush=True)
    return WhisperModel(name, device='cpu', compute_type='int8', cpu_threads=threads), 'cpu'


def listen(model, audio, language, hotwords):
    """What was said, as (start, end, text), and the language it was said in."""
    from faster_whisper.vad import VadOptions, get_speech_timestamps
    # (each stretch of speech written out on its own: left to itself, with the game's words as hints, the model ran half a minute of
    # separate remarks together under one time, and the pictures are found by the time)
    spans = get_speech_timestamps(audio, VadOptions(min_silence_duration_ms=500, speech_pad_ms=200, max_speech_duration_s=25))
    if not spans:
        return [], language
    if not language:
        # (decided once, from the first half minute of speech: a two-second remark alone is too little to tell a language by)
        sample = np.concatenate([audio[s['start']:s['end']] for s in spans])[:RATE * 30]
        _, info = model.transcribe(sample, beam_size=1, vad_filter=False)
        language = info.language
    said = []
    for span in spans:
        a, b = span['start'], span['end']
        segments, _ = model.transcribe(audio[a:b], language=language, beam_size=5, vad_filter=False, hotwords=hotwords or None, condition_on_previous_text=False)
        segments = list(segments)
        text = ' '.join(s.text.strip() for s in segments).strip()
        if not text:
            continue
        # (a word or two said in under a second is where a speech model guesses: a lone "suki" came out as "tsuki" more than once.
        # Marked, so that whoever reads it does not act on a guess)
        sure = sum(s.avg_logprob for s in segments) / len(segments)
        unsure = sure < -1.0 or ((b - a) / RATE < 1.3 and len(text) <= 3)
        said.append((a / RATE, b / RATE, text, unsure))
    return said, language


def write_notes(folder, said, trail, language, mic_line, seconds):
    L = LABELS['ja' if (language or '').startswith('ja') else 'en']
    places = [(t, w) for t, k, w in trail if k == 'place']
    place_times = [t for t, _ in places]
    shots = sorted(glob.glob(os.path.join(folder, 'shots', 's*.jpg')))
    shot_times = [float(os.path.basename(p)[1:-4]) for p in shots]

    def shot_at(t):
        if not shots:
            return None
        i = bisect.bisect_left(shot_times, t)
        return min(range(max(0, i - 1), min(len(shots), i + 1)), key=lambda j: abs(shot_times[j] - t))

    def shot_named(i, a, b):
        # (a picture is "of that moment" only if it was taken about then: one from seconds before says so)
        name, t = 'shots/' + os.path.basename(shots[i]), shot_times[i]
        if t < a - 2:
            return name + ' ' + L['earlier'] % (a - t)
        if t > b + .5:
            return name + ' ' + L['later'] % (t - b)
        return name

    def shots_for(a, b):
        # every picture from a second and a half before the remark to its end: the one taken in the middle of it, just after a click,
        # is the one most likely to show what was meant. A long remark keeps six, spread evenly.
        inside = [j for j, t in enumerate(shot_times) if a - 1.5 <= t <= b + .5]
        if len(inside) > 6:
            step = (len(inside) - 1) / 5.0
            inside = sorted({inside[int(round(n * step))] for n in range(6)})
        if not inside and shots:
            inside = [shot_at(a)]
        return inside

    def told(t, kind, words, a):
        what = words if kind == 'click' else 'key ' + words if kind == 'key' else '%s: %s' % (kind, words)
        return (L['ago'] % (a - t) if t < a else L['during'] % (t - a)) + ' ' + what

    def newest(rows, a):
        # (the newest five, the newest first, each with how long before the remark it was; a cut is said, never silent)
        line = ' / '.join(told(t, k, w, a) for t, k, w in rows[-5:][::-1])
        return line + (' ' + L['more'] % (len(rows) - 5) if len(rows) > 5 else '')

    with open(os.path.join(folder, 'notes.md'), 'w', encoding='utf-8') as f:
        f.write('# %s %s\n\n' % (L['title'], first(trail, 'start', os.path.basename(folder))))
        if first(trail, 'app'):
            f.write('- %s: %s\n' % (L['app'], first(trail, 'app')))
        f.write('- %s: %d:%02d, %s %d\n' % (L['length'], seconds // 60, seconds % 60, L['remarks'], len(said)))
        if mic_line:
            f.write('- %s: %s\n' % (L['mic'], mic_line))
        if first(trail, 'screen'):
            f.write('- %s: %s\n' % (L['screen'], first(trail, 'screen')))
        f.write('\n%s\n\n' % L['pictures_note'])
        if not said:
            f.write('%s\n' % L['no_speech'])
        for a, b, text, unsure in said:
            i = bisect.bisect_right(place_times, a) - 1
            where = [places[i][1]] if i >= 0 else []
            # (a remark begun in one place and ended in another belongs to both)
            for t, w in places:
                if a < t <= b and (not where or where[-1] != w):
                    where.append(w)
            # (what the player did, and what the game noted of its own, apart: under one heading the game's lines pushed the player's presses out)
            acts = [(t, k, w) for t, k, w in trail if k in ('click', 'key') and a - 8 <= t <= b]
            marks = [(t, k, w) for t, k, w in trail if k not in BOOKKEEPING and k not in ('click', 'key') and a - 8 <= t <= b]
            hitches = [w for t, k, w in trail if k == 'hitch' and a - 3 <= t <= b]
            picked = shots_for(a, b)
            f.write('## %.1f-%.1f %s%s%s%s\n' % (a, b, L['open'], text, L['close'], ' (?)' if unsure else ''))
            if unsure:
                f.write('- %s\n' % L['unsure'])
            f.write('- %s: %s\n' % (L['place'], ' → '.join(where)))
            f.write('- %s: %s\n' % (L['before'], newest(acts, a) or L['none']))
            if marks:
                f.write('- %s: %s\n' % (L['game'], newest(marks, a)))
            f.write('- %s: %s\n' % (L['shots'], ' , '.join(shot_named(j, a, b) for j in picked) or L['none']))
            if hitches:
                f.write('- %s: %s ms\n' % (L['hitch'], ', '.join(hitches)))
            f.write('\n')


def do_session(folder, model, args):
    trail = read_trail(os.path.join(folder, 'trail.txt'))
    mics = sorted(glob.glob(os.path.join(folder, 'mic*.wav')))
    language = args.language or first(trail, 'language')
    hotwords = ' '.join(w for w in (first(trail, 'words'), args.words) if w)
    said, mic_line, seconds = [], '', 0
    chosen, lines = pick_mic(mics) if mics else (None, ['no microphone files'])
    for line in lines:
        print('  ' + line)
    if chosen is None and mics:
        print('  no voice on any microphone: the one spoken into may be muted, or closed to desktop apps in Windows; trail.txt lists those that were found')
    if chosen is not None:
        audio = read_wav(chosen)
        seconds = len(audio) // RATE
        ready = model()
        t0 = time.time()
        said, language = listen(ready, audio, language, hotwords)
        number = os.path.basename(chosen)[3:-4]
        names = dict(w.split('  ', 1) for _, k, w in trail if k == 'mic' and '  ' in w)
        mic_line = '%s (%s)' % (os.path.basename(chosen), names.get(number, '').replace('  (skipped)', '')) if number in names else os.path.basename(chosen)
        print('  %s, %s: %d remarks from %d s of sound in %.0f s' % (os.path.basename(chosen), language, len(said), seconds, time.time() - t0), flush=True)
    if trail and not seconds:
        seconds = int(trail[-1][0])
    with open(os.path.join(folder, 'words.txt'), 'w', encoding='utf-8') as f:
        for a, b, text, unsure in said:
            f.write('%7.1f-%7.1f  %s%s\n' % (a, b, text, ' (?)' if unsure else ''))
    write_notes(folder, said, trail, language, mic_line, seconds)
    if args.delete_audio:
        delete_audio(folder)


def delete_audio(folder):
    mics = glob.glob(os.path.join(folder, 'mic*.wav'))
    for p in mics:
        os.remove(p)
    if mics:
        print('  the recordings were deleted (%d file%s); the words, the pictures and the trail stay' % (len(mics), '' if len(mics) == 1 else 's'))
    return len(mics)


def main():
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding='utf-8', errors='replace')
        except Exception:
            pass
    parser = argparse.ArgumentParser(description='PlayNotes: write the voice of play sessions out as words, beside the place, the clicks and the pictures.')
    parser.add_argument('path', help='a session folder, or the folder that holds the sessions')
    parser.add_argument('--again', action='store_true', help='also redo sessions that already have notes.md')
    parser.add_argument('--latest', action='store_true', help='only the newest session')
    parser.add_argument('--model', default=DEFAULT_MODEL, help='a Whisper model by name (tiny, small, large-v3-turbo...) or a folder holding one; default %(default)s')
    parser.add_argument('--check', action='store_true', help='say what a run would do and do nothing: how many sessions wait, and whether the speech model is on this PC or would be fetched')
    parser.add_argument('--device', default='cpu', choices=['cpu', 'cuda', 'auto'], help='default cpu: the processor alone, so the game and the editor keep the graphics card to themselves. '
                                                                                         'cuda is faster where there is an NVIDIA card with CUDA set up; auto takes it if it is there')
    parser.add_argument('--language', default='', help='the language spoken, as a code (ja, en...); default: the session\'s own setting, else detected')
    parser.add_argument('--words', default='', help='more of the game\'s own words, besides those in the session')
    parser.add_argument('--delete-audio', action='store_true', help='delete the recordings of every session named, once its words are written. A session already written out only has its '
                                                                     'recordings deleted (nothing is listened to again). Give one session\'s folder to delete just that one\'s')
    args = parser.parse_args()

    if os.name == 'nt':
        # (below normal priority: the game may still be open while this writes the words out)
        import ctypes
        ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)

    path = os.path.abspath(os.path.expandvars(os.path.expanduser(args.path)))
    if not os.path.isdir(path):
        print('no such folder: ' + path)
        return 1
    sessions, written = find_sessions(path, args.again, args.latest)
    if args.check:
        # (before anything is written, fetched or deleted)
        print('%d session%s to write out in %s' % (len(sessions), '' if len(sessions) == 1 else 's', path))
        print('speech model %s: %s' % (args.model, 'on this PC, nothing to fetch' if model_here(args.model) else 'not on this PC yet. The first run fetches it; the large models are over a gigabyte'))
        return 0
    if args.delete_audio:
        # (already written out: only the recordings go, and no model is loaded for it)
        for folder in written:
            if glob.glob(os.path.join(folder, 'mic*.wav')):
                print(os.path.basename(folder), flush=True)
                delete_audio(folder)
    if not sessions:
        if not args.delete_audio:
            print('nothing to write out in ' + path + ' (sessions that already have notes.md are left alone; --again redoes them)')
        return 0

    loaded = {}

    def model():
        # (loaded at the first session that has a voice in it, once for all of them)
        if 'model' not in loaded:
            t0 = time.time()
            loaded['model'], device = load_model(args.model, args.device)
            print('speech model %s ready in %.0f s, on the %s' % (os.path.basename(args.model.rstrip('/\\')), time.time() - t0, 'graphics card' if device == 'cuda' else 'processor'), flush=True)
        return loaded['model']

    for folder in sessions:
        print(os.path.basename(folder), flush=True)
        do_session(folder, model, args)
    print('done: %d session%s. Read notes.md in each.' % (len(sessions), '' if len(sessions) == 1 else 's'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
