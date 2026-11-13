# Third Party Notices

This package contains no third-party code and no third-party files.

`Transcribe~/notes.py` is a script that, when you run it, has `uv` fetch the following to your PC. They are not part of this package, and their own terms apply to them:

| What | Used for | Where |
|---|---|---|
| faster-whisper (MIT License) | speech to text | https://github.com/SYSTRAN/faster-whisper |
| CTranslate2 | the engine faster-whisper runs on | https://github.com/OpenNMT/CTranslate2 |
| A Whisper model by OpenAI, converted for CTranslate2 (by default `large-v3-turbo`; MIT License, 1.6 GB) | the speech model itself, fetched from Hugging Face on first use | https://huggingface.co/mobiuslabsgmbh/faster-whisper-large-v3-turbo (the name faster-whisper asks for; Hugging Face sends it on to where the files now live) |
| Silero VAD, as shipped inside faster-whisper | finding where speech is in a recording | https://github.com/snakers4/silero-vad |
| NumPy | reading the sound files | https://numpy.org |

The runtime part of the package (what goes into the Unity editor and into a build) uses only Unity and Windows' own libraries (`winmm`, `user32`, `gdi32`).
