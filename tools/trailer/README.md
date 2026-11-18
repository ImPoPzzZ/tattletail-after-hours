# Trailer toolkit (one trailer per major update)

1. **Script the trailer** in `src/Trailer.cs` (shots, cards, captions, cues) and set
   `TrailerDirector.Enabled = true`. Dev builds only: F9 in the Arcade plays it.
2. **Record**: Win+Alt+R, F9, hands off until the logo fades, Win+Alt+R.
3. **Cut**: `cut_trailer.py` (ffmpeg via `pip install imageio-ffmpeg`). Set `SRC`, find the
   take's start with ffmpeg `blackdetect` (the black opening), and pick the teaser `SEGMENTS`
   from a contact sheet. Outputs the full trailer + a ~20 s teaser, loudness-leveled for YouTube.
4. **Thumbnail**: `make_thumbnail.py` (Pillow) composes 1280x720 from frames pulled out of the
   recording, with the trailer's 3D cartoon letters (Segoe UI Black, gradient, white outline, 3D side).
5. Set `TrailerDirector.Enabled = false` again before shipping.

The 3.3 versions of both scripts are here as they were used; frame times and file names are per take.

## 3.5 (THE RECALLED UPDATE)

This one is shot in the real Recalled town (src/TrailerRecalled.cs): `TrailerDirector.RecalledTake = true`, `Enabled = true`,
dev build with `DevTools = true`. F9 in the Arcade walks through the door; the screen stays black until the whole town is
built (every zone would hitch the take if it streamed in), then the music and the script start together. Toys and friends
are puppets the script moves (mirrored toys, RcRemote kids); the kid, HUD and the town's own music are off. Esc stops it.
`cut_trailer.py <take>` finds time 0 from the heartbeat, cuts the full trailer and the teaser and draws the logo lines.
Set `Enabled = false` (and `RecalledTake` doesn't matter) before any release build.

The 3.5 trailer is done (cut with `cut_trailer.py`, thumbnail with `make_thumbnail_35.py`). `TrailerDirector.Enabled` is false again: F9 in the Arcade does nothing until the next trailer is scripted and it's switched back on.
