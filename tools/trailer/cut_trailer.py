"""
Cuts a recorded trailer take into the full trailer + a teaser (loudness-leveled for YouTube).
This is the 3.5 version (THE RECALLED UPDATE); git history has the 3.3 and 3.4 ones.

Usage: python cut_trailer.py <take> [start seconds] [--teaser]

The take starts wherever the recording started (the door, the loading screen, a black wait while the town builds), so the
trailer's time 0 is found from the sound: the music starts with a heartbeat on black, the first thump 0.3 s in.
(The logo's lines draw in the game this time; the 3.4 take lost them to Unity's 65k-vertex limit and the script drew them.)
"""
import os, shutil, subprocess, sys
import numpy as np

import imageio_ffmpeg
FF = imageio_ffmpeg.get_ffmpeg_exe()

ARGS = [a for a in sys.argv[1:] if not a.startswith('--')]
TEASER = '--teaser' in sys.argv      # the ~20 s teaser is only made when asked for
SRC = ARGS[0] if ARGS else r'D:\videos\take.mp4'
LOGO_T, END_T = 46.4, 54.0     # trailer times (src/TrailerRecalled.cs R_LOGO_T, R_END)
OUT_FULL = r'D:\videos\TattletailAfterHours_3.5_Trailer.mp4'
OUT_TEASER = r'D:\videos\TattletailAfterHours_3.5_Teaser.mp4'
TMP = os.path.join(os.environ['TEMP'], 'ke_trailer35')
os.makedirs(TMP, exist_ok=True)

ENC = ['-c:v', 'libx264', '-preset', 'slow', '-crf', '18', '-pix_fmt', 'yuv420p',
       '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-movflags', '+faststart']
LOUD = 'loudnorm=I=-14:TP=-1.0:LRA=11'

# The teaser: the best beats, back to back (trailer times). Adjust from a contact sheet of the take.
SEGMENTS = [
    (4.6, 7.6),    # Main Street, the toys in the fog
    (12.4, 16.2),  # the pack rushes the camera, winding up
    (16.2, 20.2),  # FIGHT BACK. A perfect block
    (31.0, 34.4),  # four friends run up the street
    (37.4, 42.0),  # the first bosses
    (46.4, 51.0),  # the logo and its lines
]


def run(args):
    print(' '.join(a if ' ' not in a else '"%s"' % a for a in args[:6]), '...')
    r = subprocess.run(args, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-3000:])
        sys.exit(1)
    return r


def find_start():
    """
    Trailer time 0. The game is on black from when the town starts building until the first shot (the wait, then the opening card,
    which ends at OPEN_END = 3.6 s), so the last long black stretch in the take ends at trailer time 3.6. (Sound is no help here:
    the Arcade and the loading screen make noise before it.) Pass a second argument to set the start by hand instead.
    """
    if len(ARGS) > 1:
        return float(ARGS[1])
    r = run([FF, '-hide_banner', '-i', SRC, '-vf', 'blackdetect=d=3.5:pix_th=0.12:pic_th=0.97', '-an', '-f', 'null', '-'])
    import re
    segs = [(float(a), float(b)) for a, b in re.findall(r'black_start:([\d.]+) black_end:([\d.]+)', r.stderr)]
    if not segs:
        print('no black stretch found; give the start (seconds into the take) as the second argument'); sys.exit(1)
    segs = [g for g in segs if g[1] - g[0] >= 6.0] or segs   # (the wait for the town and the opening card are 9 s or more)
    end = segs[-1][1]
    t0 = end - 3.6
    print('the last long black stretch ends at %.2f s in the take -> trailer time 0 is %.2f s' % (end, t0))
    return t0


T0 = find_start()
# ── The master: the take from trailer time 0 to the end ──
dur = END_T
master = os.path.join(TMP, 'master.mov')
run([FF, '-hide_banner', '-y', '-ss', '%.3f' % T0, '-i', SRC, '-t', '%.2f' % dur,
     '-c:v', 'libx264', '-preset', 'fast', '-crf', '12', '-pix_fmt', 'yuv420p', '-c:a', 'pcm_s16le', master])

# ── The full trailer ──
run([FF, '-hide_banner', '-y', '-i', master, '-t', '%.2f' % dur,
     '-vf', 'fade=t=out:st=%.2f:d=1.0' % (dur - 1.0),
     '-af', 'afade=t=in:d=0.05,afade=t=out:st=%.2f:d=1.0,%s' % (dur - 1.0, LOUD)] + ENC + [OUT_FULL])

# ── The teaser (only with --teaser) ──
if TEASER:
    parts, labels = [], []
    for i, (a, b) in enumerate(SEGMENTS):
        d = b - a
        parts.append('[0:v]trim=%.2f:%.2f,setpts=PTS-STARTPTS[v%d];' % (a, b, i))
        parts.append('[0:a]atrim=%.2f:%.2f,asetpts=PTS-STARTPTS,afade=t=in:d=0.03,afade=t=out:st=%.2f:d=0.03[a%d];' % (a, b, d - 0.03, i))
        labels.append('[v%d][a%d]' % (i, i))
    total = sum(b - a for a, b in SEGMENTS)
    graph = ''.join(parts) + ''.join(labels) + 'concat=n=%d:v=1:a=1[cv][ca];' % len(SEGMENTS)
    graph += '[cv]fade=t=out:st=%.2f:d=0.8[vout];[ca]afade=t=out:st=%.2f:d=0.8,%s[aout]' % (total - 0.8, total - 0.8, LOUD)
    run([FF, '-hide_banner', '-y', '-i', master, '-filter_complex', graph, '-map', '[vout]', '-map', '[aout]'] + ENC + [OUT_TEASER])
    print('full: %.1f s, teaser: %.1f s' % (dur, total))
else:
    print('full: %.1f s (no teaser: pass --teaser for one)' % dur)
