"""Builds 'Install Tattletail After Hours.exe' next to the release zips.

payload.zip = the Steam release zip (32-bit loader at the root) + the itch.io zip's 64-bit loader
under x64/ (the installer picks by the game exe's CPU type). Also makes installer.ico from the logo.
Usage: python tools/installer/make_installer.py [version]   (default 3.5)
"""
import os, sys, zipfile, subprocess, shutil
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
VER = sys.argv[1] if len(sys.argv) > 1 else '3.5'
STEAM_ZIP = os.path.join(ROOT, 'TattletailAfterHours_v%s.zip' % VER)
ITCH_ZIP = os.path.join(ROOT, 'TattletailAfterHours_v%s_itchio.zip' % VER)
X64_ONLY = ('winhttp.dll', '.doorstop_version')   # the files that differ for the 64-bit game

# The payload.
payload = os.path.join(HERE, 'payload.zip')
with zipfile.ZipFile(STEAM_ZIP) as steam, zipfile.ZipFile(ITCH_ZIP) as itch, \
        zipfile.ZipFile(payload, 'w', zipfile.ZIP_DEFLATED) as out:
    for n in steam.namelist():
        out.writestr(n, steam.read(n))
    for n in X64_ONLY:
        out.writestr('x64/' + n, itch.read(n))
    names = out.namelist()
assert 'BepInEx/plugins/KaleidoscopeEndless/KaleidoscopeEndless.dll' in names and 'x64/winhttp.dll' in names
assert not any('com.kaleidoscope' in n for n in names), 'dev config in the payload'
print('payload:', len(names), 'files,', os.path.getsize(payload) // 1024, 'KB')

# The icon: Tattletail's face from the logo, on a round purple badge.
logo = Image.open(os.path.join(HERE, 'logo.png')).convert('RGBA')
face = logo.crop((220, 50, 640, 470)).resize((256, 256), Image.LANCZOS)
badge = Image.new('RGBA', (256, 256), (0, 0, 0, 0))
disc = Image.new('L', (256, 256), 0)
from PIL import ImageDraw
ImageDraw.Draw(disc).ellipse((4, 4, 252, 252), fill=255)
badge.paste(Image.new('RGBA', (256, 256), (58, 16, 84, 255)), (0, 0), disc)
badge.alpha_composite(face)
icon = Image.composite(badge, Image.new('RGBA', (256, 256), (0, 0, 0, 0)), disc)
icon.save(os.path.join(HERE, 'installer.ico'), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

# Build.
r = subprocess.run(['dotnet', 'build', os.path.join(HERE, 'AfterHoursInstaller.csproj'), '-c', 'Release', '-nologo', '-v', 'q'], capture_output=True, text=True)
if r.returncode != 0:
    print(r.stdout[-4000:], r.stderr[-2000:])
    sys.exit(1)
exe = os.path.join(HERE, 'bin', 'Release', 'net48', 'Install Tattletail After Hours.exe')
dest = os.path.join(ROOT, 'Install Tattletail After Hours.exe')
shutil.copy(exe, dest)
print('built:', dest, os.path.getsize(dest) // 1024, 'KB')
