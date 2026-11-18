"""
The Character Creator's kit: every piece a custom kid is built from, sculpted the same way
as tools/kid_model.py, but as separate parts the game snaps together:

  bodies    3 frames (slim / average / sturdy) x 2 bottoms (pants / skirt) — skinned to the
            kid skeleton, neck included; outfits recolor them like the preset kids
  heads     3 shapes (round / oval / wide): skull, jaw, ears — rigid on the head bone
  hair      7 styles, built for the round head; the game scales them to the other heads
  features  eyes, brows, noses, mouths — small rigid meshes, centered at 0 and facing +Z;
            the game places them on the face (and sliders move them around)
  extras    glasses (one side each, placed like an eye), a band-aid, a freckle dot and
            the dark circle under an eye
  faces     for each head, a grid of where the face surface is (z for each x, y), so a
            feature moved by a slider still sits right on the face

Writes assets/creator_kit.bin (KIT1, positions/normals/weights quantized to stay small) and,
for looking at it, <out>/creator_preview_N.glb: a few assembled kids.

    python tools/creator_kit.py [preview_dir]
"""
import math, os, struct, sys
import numpy as np
from skimage import measure

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kid_model as km
from kid_model import V, sd_ellipsoid, sd_capsule, smin

km.HEIGHT = 1.1
H = km.HEIGHT
VOXEL = 0.0085                       # a bit coarser than the preset kids: the kit has many pieces
SKULL_C = V(0, 0.905, 0.0)
FACE = {"eyeX": 0.036, "eyeY": 0.898, "browY": 0.93, "noseY": 0.878, "mouthY": 0.848}  # default anchors

for extra in (("iris", (0.3, 0.55, 0.85), 0.5), ("sclera", (0.96, 0.96, 0.95), 0.3),
              ("frame", (0.1, 0.08, 0.08), 0.5), ("lens", (0.04, 0.04, 0.06), 0.9),
              ("freckle", (0.7, 0.45, 0.32), 0.1), ("circles", (0.5, 0.35, 0.45), 0.1),
              ("bandaid", (0.9, 0.72, 0.56), 0.2), ("bandpad", (0.98, 0.93, 0.86), 0.1)):
    if extra[0] not in km.MAT:
        km.MATERIALS.append(extra)
        km.MAT[extra[0]] = len(km.MATERIALS) - 1

# ── Sculpting helpers ──────────────────────────────────────────────────────
def sculpt_in(lo, hi, voxel=VOXEL):
    n = np.ceil((hi - lo) / voxel).astype(int) + 1
    xs, ys, zs = [lo[i] + np.arange(n[i]) * voxel for i in range(3)]
    grid = np.stack(np.meshgrid(xs, ys, zs, indexing="ij"), axis=-1)
    field = np.empty(tuple(n))
    for i in range(n[0]):
        field[i] = km.body_sd(grid[i])
    verts, faces, _, _ = measure.marching_cubes(field, 0.0, spacing=(voxel,) * 3)
    verts += lo
    e = voxel * 0.5
    nrm = np.stack([km.body_sd(verts + V(*(e if j == i else 0 for j in range(3)))) - km.body_sd(verts - V(*(e if j == i else 0 for j in range(3)))) for i in range(3)], axis=-1)
    nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True) + 1e-12
    a, b, c = verts[faces[:, 0]], verts[faces[:, 1]], verts[faces[:, 2]]
    if np.mean(np.sum(np.cross(b - a, c - a) * nrm[faces[:, 0]], axis=-1)) < 0:
        faces = faces[:, [0, 2, 1]]
    return verts, nrm, faces

def split_by_material(mats, faces):
    fm = mats[faces]
    tri_mat = np.where(fm[:, 1] == fm[:, 2], fm[:, 1], fm[:, 0])
    return [(km.MATERIALS[m][0], faces[tri_mat == m]) for m in range(len(km.MATERIALS)) if np.any(tri_mat == m)]

def reset():
    del km.SHAPES[:]
    km.KIND = "kit"
    km.make_skeleton(adult=False)

# ── Bodies ─────────────────────────────────────────────────────────────────
FRAMES = {"slim": (0.9, 0.88, 0.95), "average": (1.0, 1.0, 1.0), "sturdy": (1.14, 1.15, 1.18)}  # width, limb thickness, belly

def define_body(frame, bottom):
    reset()
    w, t, b = FRAMES[frame]
    J = km.JOINT
    km.shape(lambda p: sd_ellipsoid(p, V(0, 0.672, -0.004), V(0.112 * w, 0.092, 0.075 * (0.5 + 0.5 * w))), "shirt", "torso")
    km.shape(lambda p: sd_ellipsoid(p, V(0, 0.585, 0.006 + 0.01 * (b - 1)), V(0.104 * w * b, 0.085, 0.075 * b)), "shirt", "torso")
    km.shape(lambda p: sd_capsule(p, V(-0.092 * w, 0.725, -0.006), V(0.092 * w, 0.725, -0.006), 0.042 * t, 0.042 * t), "shirt", "torso")
    km.shape(lambda p: sd_ellipsoid(p, V(0, 0.49, 0.0), V(0.108 * w, 0.075, 0.077 * b)), "pants", "torso", -0.002)
    km.shape(lambda p: sd_capsule(p, V(0, 0.74, -0.004), V(0, 0.85, 0.0), 0.034 * t, 0.032 * t), "skin", "head")
    if bottom == "skirt":
        km.shape(lambda p: km.skirt_sd(p / V(w, 1, w * 0.5 + 0.5)), "pants", "skirt", -0.003, hard=True)
    for s, sx in (("R", 1), ("L", -1)):
        j = (lambda s: lambda n: J[n + s])(s)
        legs = "skin" if bottom == "skirt" else "pants"
        t0, t1, s0, s1 = [r * t for r in ((0.044, 0.037, 0.035, 0.03) if bottom == "skirt" else (0.05, 0.041, 0.04, 0.034))]
        km.shape((lambda j, t0, t1: lambda p: sd_capsule(p, j("upperleg") + V(0, 0.01, 0), j("lowerleg"), t0, t1))(j, t0, t1), legs, "leg" + s)
        km.shape((lambda j, s0, s1: lambda p: sd_capsule(p, j("lowerleg"), j("foot") + V(0, 0.01, 0), s0, s1))(j, s0, s1), legs, "leg" + s)
        shoe_r = V(0.043, 0.038, 0.083) * (0.5 + 0.5 * t)
        def shoe(p, j=j, r=shoe_r):
            c = j("foot") * np.array([1, 0, 1]) + V(0, r[1] - 0.004, 0.03)
            return np.maximum(sd_ellipsoid(p, c, r), -p[..., 1])
        km.shape(shoe, "shoe", "leg" + s, -0.003)
        km.shape((lambda j: lambda p: sd_capsule(p, j("upperarm"), j("upperarm") + (j("lowerarm") - j("upperarm")) * 0.55, 0.039 * t, 0.036 * t))(j), "shirt", "arm" + s, -0.002)
        km.shape((lambda j: lambda p: sd_capsule(p, j("upperarm"), j("lowerarm"), 0.031 * t, 0.027 * t))(j), "skin", "arm" + s)
        km.shape((lambda j: lambda p: sd_capsule(p, j("lowerarm"), j("hand"), 0.026 * t, 0.022 * t))(j), "skin", "arm" + s)
        km.shape((lambda j: lambda p: sd_capsule(p, j("hand") + V(0, -0.012, 0), j("handEnd"), 0.024, 0.02))(j), "skin", "arm" + s)
        km.shape((lambda j, sx: lambda p: sd_capsule(p, j("hand") + V(0, -0.02, 0.012), j("hand") + V(-0.006 * sx, -0.05, 0.03), 0.01, 0.009))(j, sx), "skin", "arm" + s)

def build_body(frame, bottom):
    define_body(frame, bottom)
    verts, nrms, faces = sculpt_in(V(-0.36, -0.01, -0.17), V(0.36, 0.86, 0.17))
    mats, regions = km.label(verts)
    bidx, bw = km.skin_weights(verts, regions)
    return dict(name="body_%s_%s" % (frame, bottom), skinned=True, verts=verts, nrms=nrms, bidx=bidx, bw=bw, subs=split_by_material(mats, faces))

# ── Heads ──────────────────────────────────────────────────────────────────
HEADS = {  # skull radii, jaw center, jaw radii, ear x
    "round": (V(0.097, 0.104, 0.1), V(0, 0.86, 0.022), V(0.078, 0.058, 0.076), 0.097),
    "oval":  (V(0.09, 0.113, 0.097), V(0, 0.853, 0.02), V(0.068, 0.066, 0.072), 0.09),
    "wide":  (V(0.106, 0.1, 0.1), V(0, 0.862, 0.022), V(0.088, 0.056, 0.08), 0.106),
}

def define_head(name):
    reset()
    skull, jc, jr, ex = HEADS[name]
    km.shape(lambda p: sd_ellipsoid(p, SKULL_C, skull), "skin", "head")
    km.shape(lambda p: sd_ellipsoid(p, jc, jr), "skin", "head")
    for sx in (1, -1):
        km.shape((lambda sx: lambda p: sd_ellipsoid(p, V(ex * sx, 0.888, -0.004), V(0.014, 0.026, 0.02)))(sx), "skin", "head")

HEAD_BOX = (V(-0.16, 0.76, -0.14), V(0.16, 1.04, 0.14))

def build_head(name):
    define_head(name)
    verts, nrms, faces = sculpt_in(*HEAD_BOX)
    mats, _ = km.label(verts)
    return dict(name="head_" + name, skinned=False, verts=verts, nrms=nrms, subs=split_by_material(mats, faces))

def face_grid(name):
    """z of the face surface for a grid of (x, y), marching in from the front, all points at once."""
    define_head(name)
    xs = np.arange(-0.1, 0.1001, 0.005)
    ys = np.arange(0.8, 0.9801, 0.005)
    X, Y = np.meshgrid(xs, ys)
    pts = np.stack([X.ravel(), Y.ravel(), np.full(X.size, 0.2)], axis=-1)
    hit = np.zeros(len(pts), bool)
    for _ in range(500):
        d = km.body_sd(pts)
        hit |= d < 1e-4
        step = np.where(hit, 0.0, np.maximum(d * 0.5, 1e-4))
        pts[:, 2] -= step
        pts[:, 2] = np.maximum(pts[:, 2], -0.2)
    z = np.where(hit, pts[:, 2], 0.0)
    return dict(name="face_" + name, x0=xs[0], y0=ys[0], step=0.005, nx=len(xs), ny=len(ys), z=z)

# ── Hair (built on the round head; the game scales it to the others) ──────────
def hairline(z, lo_back, lo_side, front, fringe):
    return np.interp(z, [-0.12, -0.02, 0.05, 0.12], [lo_back, lo_side, front, fringe])

def cap_sd(p, grow=0.0, line=(0.83, 0.87, 0.925, 0.94)):
    d = sd_ellipsoid(p, V(0, 0.918, -0.01), V(0.107 + grow, 0.103 + grow, 0.11 + grow))
    ln = hairline(p[..., 2], *line)
    side = np.abs(p[..., 0])
    ln = ln - np.clip((side - 0.07) * 0.8, 0, 0.03) * (p[..., 2] < 0.06)
    return np.maximum(d, ln - p[..., 1])

def spike(base, tip, r):
    return lambda p: sd_capsule(p, base, tip, r, 0.004)

HAIR = {
    "messy": [cap_sd] + [(lambda c, r: lambda p: sd_ellipsoid(p, c, V(r * 1.2, r * 0.7, r * 1.2)))(c, r)
                         for c, r in ((V(0.02, 1.005, 0.03), 0.028), (V(-0.035, 0.998, 0.0), 0.03), (V(0.01, 0.99, -0.06), 0.032), (V(0.06, 0.975, 0.045), 0.022))],
    "bob": [km.long_hair_sd],
    "buzz": [lambda p: cap_sd(p, -0.004, (0.845, 0.88, 0.94, 0.955))],
    "long": [lambda p: np.maximum(smin(sd_ellipsoid(p, V(0, 0.915, -0.012), V(0.108, 0.106, 0.112)),
                                       sd_ellipsoid(p, V(0, 0.8, -0.04), V(0.122, 0.17, 0.095)), 0.025),
                                  np.interp(p[..., 2], [-0.2, 0.0, 0.04, 0.12], [0.66, 0.68, 0.93, 0.945]) - p[..., 1])],
    "pigtails": [cap_sd] + [(lambda sx: lambda p: smin(sd_ellipsoid(p, V(0.118 * sx, 0.89, -0.03), V(0.03, 0.06, 0.03)),
                                                       sd_ellipsoid(p, V(0.105 * sx, 0.935, -0.02), V(0.028, 0.028, 0.028)), 0.015))(sx) for sx in (1, -1)],
    "spiky": [cap_sd] + [spike(b, t, r) for b, t, r in (
        (V(0, 0.99, 0.02), V(0.0, 1.07, 0.06), 0.03), (V(0.05, 0.98, 0.0), V(0.1, 1.05, 0.02), 0.028),
        (V(-0.05, 0.98, 0.0), V(-0.1, 1.05, 0.02), 0.028), (V(0.0, 0.99, -0.05), V(0.0, 1.06, -0.1), 0.03),
        (V(0.05, 0.97, -0.05), V(0.1, 1.02, -0.1), 0.026), (V(-0.05, 0.97, -0.05), V(-0.1, 1.02, -0.1), 0.026))],
    "ponytail": [cap_sd, lambda p: sd_capsule(p, V(0, 0.96, -0.11), V(0, 0.8, -0.15), 0.032, 0.02)],
}

def build_hair(style):
    reset()
    for fn in HAIR[style]:
        km.shape(fn, "hair", "head", -0.004)
    # A closed shell on its own; the part of it inside the head is simply hidden by the head.
    verts, nrms, faces = sculpt_in(V(-0.2, 0.62, -0.24), V(0.2, 1.12, 0.17))
    return dict(name="hair_" + style, skinned=False, verts=verts, nrms=nrms, subs=[("hair", faces)])

# ── Face features (local: centered at 0, facing +Z, in model units) ─────────
def em(center, radii, mat, rot=np.eye(3), seg=12, rings=8):
    v, n, f = km.ellipsoid_mesh(V(*center), V(*radii), rot, seg, rings)
    return (v, n, f, mat)

def stroke(points, r, mat):
    out = []
    for a, b in zip(points, points[1:]):
        v, n, f = km.capsule_mesh(V(*a), V(*b), r)
        out.append((v, n, f, mat))
    return out

def rz(deg):
    a = math.radians(deg)
    return np.array([[math.cos(a), -math.sin(a), 0], [math.sin(a), math.cos(a), 0], [0, 0, 1]])

FEATURES = {
    # eyes: one eye, centered; the game places it twice (mirrored for the other side)
    "eye_round": [em((0, 0, -0.002), (0.0125, 0.017, 0.006), "eye"), em((0.004, 0.006, 0.0035), (0.0035, 0.0035, 0.002), "glint", seg=8, rings=6)],
    "eye_big": [em((0, 0, -0.002), (0.02, 0.022, 0.006), "sclera"), em((0, -0.001, 0.0025), (0.012, 0.014, 0.004), "iris"),
                em((0, -0.001, 0.005), (0.006, 0.007, 0.003), "eye"), em((0.005, 0.006, 0.007), (0.0035, 0.0035, 0.002), "glint", seg=8, rings=6)],
    "eye_sleepy": stroke([(-0.015, 0.001, 0.0), (0.0, 0.004, 0.0), (0.014, 0.002, 0.0)], 0.0028, "eye") + [em((0.001, -0.004, -0.001), (0.009, 0.006, 0.005), "eye")],
    "eye_narrow": [em((0, 0, -0.002), (0.014, 0.0058, 0.004), "sclera"), em((0.0, 0.0, 0.0008), (0.0045, 0.005, 0.003), "eye", seg=8, rings=6)]
                  + stroke([(-0.014, 0.003, 0.001), (0.0, 0.006, 0.001), (0.015, 0.004, 0.001)], 0.0018, "eye"),
    "eye_dot": [em((0, 0, -0.001), (0.0075, 0.0095, 0.004), "eye")],
    # brows (hair colored); the game tilts them with the angle slider
    "brow_thin": stroke([(-0.017, -0.002, 0), (-0.002, 0.003, 0), (0.016, 0.0, 0)], 0.0028, "hair"),
    "brow_thick": stroke([(-0.017, -0.002, 0), (-0.002, 0.003, 0), (0.016, 0.0, 0)], 0.0048, "hair"),
    "brow_bushy": [em((x, y, 0), (0.009, 0.005, 0.004), "hair") for x, y in ((-0.012, -0.001), (0.0, 0.002), (0.012, 0.0))],
    # noses
    "nose_button": [em((0, 0, -0.004), (0.012, 0.014, 0.012), "skin")],
    "nose_small": [em((0, 0, -0.004), (0.008, 0.009, 0.008), "skin")],
    "nose_pointy": [(lambda v, n, f: (v, n, f, "skin"))(*km.capsule_mesh(V(0, 0.01, -0.006), V(0, -0.006, 0.012), 0.0075))],
    # mouths
    "mouth_smile": stroke([(-0.018, 0.004, 0), (-0.009, -0.002, 0), (0.0, -0.004, 0), (0.009, -0.002, 0), (0.018, 0.004, 0)], 0.0028, "mouth"),
    "mouth_grin": [em((0, -0.002, -0.001), (0.021, 0.011, 0.004), "eye"), em((0, 0.004, 0.0015), (0.016, 0.0035, 0.003), "glint")],
    "mouth_frown": stroke([(-0.016, -0.004, 0), (-0.006, 0.001, 0), (0.006, 0.001, 0), (0.016, -0.004, 0)], 0.0026, "mouth"),
    "mouth_flat": stroke([(-0.014, 0, 0), (0.014, 0, 0)], 0.0025, "mouth"),
    "mouth_gasp": [em((0, 0, -0.001), (0.009, 0.012, 0.004), "eye")],
    "mouth_cat": stroke([(-0.02, 0.004, 0), (-0.011, -0.004, 0), (-0.003, 0.0, 0), (0.0, 0.003, 0), (0.003, 0.0, 0), (0.011, -0.004, 0), (0.02, 0.004, 0)], 0.0022, "mouth"),
}

# ── Extras ─────────────────────────────────────────────────────────────────
def slab(rx, ry, t, mat, center=(0, 0, 0), bend=0.1):
    """A flat rounded rectangle, bent along x to lie on a cheek (band-aids)."""
    v, n, f = km.ellipsoid_mesh(V(0, 0, 0), V(1, 1, 1), np.eye(3), 24, 12)
    x, y, z = v[:, 0], v[:, 1], v[:, 2]
    out = np.stack([np.sign(x) * np.abs(x) ** 0.35 * rx, np.sign(y) * np.abs(y) ** 0.35 * ry, z * t], axis=-1)
    nrm = np.stack([x / rx, y / ry, z / t], axis=-1)
    nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True)
    out[:, 2] -= out[:, 0] ** 2 / (2 * bend)
    return (out + V(*center), nrm, f, mat)

def ring(rx, ry, z, r, mat, square=False, n=40):
    pts = []
    for k in range(n + 1):
        a = 2 * math.pi * k / n
        c, s = math.cos(a), math.sin(a)
        if square:  # a rounded rectangle
            c, s = math.copysign(abs(c) ** 0.4, c), math.copysign(abs(s) ** 0.4, s)
        pts.append((rx * c, ry * s, z))
    return stroke(pts, r, mat)

GLASS_Z = 0.011   # rims sit in front of the eyes

def add_glasses(g):
    """One side of the glasses, placed at the eye like an eye is. Its arm is aimed (for the
    round head, default eye) back over the top of the ear."""
    ex, ey = FACE["eyeX"], FACE["eyeY"]
    zx = (grid_z(g, ex + 0.004, ey) - grid_z(g, ex - 0.004, ey)) / 0.008
    zy = (grid_z(g, ex, ey + 0.004) - grid_z(g, ex, ey - 0.004)) / 0.008
    nz = V(-zx, -zy, 1.0); nz /= np.linalg.norm(nz)
    R = km.rot_facing(nz)
    E = V(ex, ey, grid_z(g, ex, ey) + 0.0015)
    local = lambda w: tuple(R.T @ (V(*w) - E))
    for style, rx, ry, square, shades in (("round", 0.021, 0.02, False, False), ("square", 0.023, 0.017, True, False),
                                         ("shades", 0.023, 0.018, True, True)):
        parts = ring(rx, ry, GLASS_Z, 0.0021 if not shades else 0.0026, "frame", square)
        parts += stroke([(-rx, 0.004, GLASS_Z), (-ex + 0.004, 0.007, GLASS_Z + 0.004), (-ex - 0.008, 0.007, GLASS_Z + 0.004)], 0.0019, "frame")
        hinge = (rx + 0.001, 0.004, GLASS_Z)
        parts += stroke([hinge, local((0.097, ey + 0.008, 0.035)), local((0.106, 0.914, -0.01)), local((0.1, 0.905, -0.06))], 0.0019, "frame")
        if shades:
            parts.append(em((0, 0, GLASS_Z - 0.0005), (rx * (0.93 if square else 0.95), ry * 0.93, 0.0012), "lens", seg=20, rings=8))
        FEATURES["glasses_" + style] = parts

FEATURES.update({
    "extra_bandaid": [slab(0.017, 0.0062, 0.0014, "bandaid"), slab(0.0055, 0.0052, 0.0018, "bandpad", (0, 0, 0.0004))],
    "extra_dot": [em((0, 0, 0), (0.0021, 0.0021, 0.0008), "freckle", seg=8, rings=6)],
    "extra_circles": [em((0, -0.021, 0), (0.0165, 0.0085, 0.0009), "circles", seg=16, rings=8)],
})

def build_feature(name):
    verts, nrms, subs = [], [], {}
    base = 0
    for v, n, f, m in FEATURES[name]:
        verts.append(v); nrms.append(n)
        subs.setdefault(m, []).append(f + base)
        base += len(v)
    return dict(name="feature_" + name.split("_", 1)[0] + "_" + name.split("_", 1)[1], skinned=False,
                verts=np.vstack(verts), nrms=np.vstack(nrms), subs=[(m, np.vstack(fs)) for m, fs in subs.items()])

# ── Writing ────────────────────────────────────────────────────────────────
def write_kit(path, meshes, grids, clips):
    out = bytearray(b"KIT1")
    w = lambda fmt, *v: out.extend(struct.pack("<" + fmt, *v))
    def wstr(s):
        b = s.encode("utf-8"); w("i", len(b)); out.extend(b)
    w("f", H)
    w("i", len(km.BONES))
    for name, parent in km.BONES:
        wstr(name); w("i", km.BI[parent] if parent else -1); w("3f", *(km.local_pos(name) * H))
    w("i", len(clips))
    for name, period, rots, hips in clips:
        wstr(name); w("i", km.FRAMES); w("f", period)
        for f in range(km.FRAMES):
            w("3f", *(hips[f] * H))
            for b in range(len(km.BONES)):
                w("4f", *rots[f, b])
    w("i", len(meshes))
    for m in meshes:
        verts = m["verts"] * H
        lo, hi = verts.min(axis=0), verts.max(axis=0)
        span = np.maximum(hi - lo, 1e-6)
        wstr(m["name"]); w("B", 1 if m["skinned"] else 0); w("i", len(verts))
        w("3f", *lo); w("3f", *hi)
        q = np.round((verts - lo) / span * 65535).astype("<u2")
        out.extend(q.tobytes())
        nq = np.clip(np.round(m["nrms"] * 127), -127, 127).astype("<i1")
        out.extend(nq.tobytes())
        if m["skinned"]:
            out.extend(m["bidx"].astype("<u1").tobytes())
            wq = np.round(m["bw"] * 255).astype(int)
            wq[:, 0] += 255 - wq.sum(axis=1)  # the four weights still add up to exactly 1
            out.extend(np.clip(wq, 0, 255).astype("<u1").tobytes())
        subs = [(mat, tris) for mat, tris in m["subs"] if len(tris)]
        w("i", len(subs))
        for mat, tris in subs:
            wstr(mat)
            _, col, gloss = km.MATERIALS[km.MAT[mat]]
            w("3f", *col); w("f", gloss)
            idx = np.asarray(tris).reshape(-1).astype("<u2")
            w("i", len(idx)); out.extend(idx.tobytes())
    w("i", len(grids))
    for g in grids:
        wstr(g["name"]); w("i", g["nx"]); w("i", g["ny"]); w("f", g["x0"] * H); w("f", g["y0"] * H); w("f", g["step"] * H)
        out.extend((np.asarray(g["z"]) * H).astype("<f4").tobytes())
    w("f", SKULL_C[1] * H)
    for k in ("eyeX", "eyeY", "browY", "noseY", "mouthY"):
        w("f", FACE[k] * H)
    for name in ("round", "oval", "wide"):
        w("3f", *(HEADS[name][0] * H))
    with open(path, "wb") as fh:
        fh.write(out)
    return len(out)

# ── Assembled previews (all in one glb) ────────────────────────────────────
def grid_z(g, x, y):
    fx, fy = (x - g["x0"]) / g["step"], (y - g["y0"]) / g["step"]
    i, j = int(np.clip(fx, 0, g["nx"] - 2)), int(np.clip(fy, 0, g["ny"] - 2))
    tx, ty = fx - i, fy - j
    z = np.asarray(g["z"]).reshape(g["ny"], g["nx"])
    return (z[j, i] * (1 - tx) + z[j, i + 1] * tx) * (1 - ty) + (z[j + 1, i] * (1 - tx) + z[j + 1, i + 1] * tx) * ty

def place(feature, g, x, y, mirror=False, lift=0.0015, angle=0.0):
    v = feature["verts"].copy(); n = feature["nrms"].copy()
    if mirror: v[:, 0] *= -1; n[:, 0] *= -1
    if angle:
        r = rz(-angle if mirror else angle); v = v @ r.T; n = n @ r.T
    # tilt to the face's slope
    zx = (grid_z(g, x + 0.004, y) - grid_z(g, x - 0.004, y)) / 0.008
    zy = (grid_z(g, x, y + 0.004) - grid_z(g, x, y - 0.004)) / 0.008
    nz = V(-zx, -zy, 1.0); nz /= np.linalg.norm(nz)
    rot = km.rot_facing(nz)
    v = v @ rot.T; n = n @ rot.T
    v += V(x, y, grid_z(g, x, y) + lift)
    subs = [(m, t) for m, t in feature["subs"]]
    return dict(verts=v, nrms=n, subs=subs, mirror=mirror)

def assemble(spec, parts):
    body = parts["body_%s_%s" % (spec["frame"], spec["bottom"])]
    verts, nrms = [body["verts"]], [body["nrms"]]
    bidx, bw = [body["bidx"]], [body["bw"]]
    subs = {}
    base = 0
    def add(v, n, ss, flip=False, rigid=True):
        nonlocal base
        verts.append(v); nrms.append(n)
        if rigid:
            bidx.append(np.tile([km.BI["head"], 0, 0, 0], (len(v), 1))); bw.append(np.tile([1.0, 0, 0, 0], (len(v), 1)))
        for m, t in ss:
            t = np.asarray(t)
            if flip: t = t[:, [0, 2, 1]]
            subs.setdefault(m, []).append(t + base)
        base += len(v)
    for m, t in body["subs"]:
        subs.setdefault(m, []).append(np.asarray(t))
    base = len(body["verts"])
    head = parts["head_" + spec["head"]]
    add(head["verts"], head["nrms"], head["subs"])
    if spec["hair"] != "bald":
        hair = parts["hair_" + spec["hair"]]
        s = HEADS[spec["head"]][0] / HEADS["round"][0]
        add(SKULL_C + (hair["verts"] - SKULL_C) * s, hair["nrms"] / s / np.linalg.norm(hair["nrms"] / s, axis=-1, keepdims=True), hair["subs"])
    g = parts["face_" + spec["head"]]
    for side in (1, -1):
        p = place(parts["feature_eye_" + spec["eyes"]], g, FACE["eyeX"] * side, FACE["eyeY"], side < 0)
        add(p["verts"], p["nrms"], p["subs"], flip=side < 0)
        if spec["brows"] != "none":
            p = place(parts["feature_brow_" + spec["brows"]], g, FACE["eyeX"] * side, FACE["browY"], side < 0, angle=spec.get("browAngle", 0))
            add(p["verts"], p["nrms"], p["subs"], flip=side < 0)
        if spec.get("glasses"):
            p = place(parts["feature_glasses_" + spec["glasses"]], g, FACE["eyeX"] * side, FACE["eyeY"], side < 0)
            add(p["verts"], p["nrms"], p["subs"], flip=side < 0)
        if spec.get("circles"):
            p = place(parts["feature_extra_circles"], g, FACE["eyeX"] * side, FACE["eyeY"], side < 0)
            add(p["verts"], p["nrms"], p["subs"], flip=side < 0)
        if spec.get("freckles"):
            for dx, dy in FRECKLES:
                p = place(parts["feature_extra_dot"], g, (FACE["eyeX"] + dx) * side, FACE["eyeY"] + dy, side < 0)
                add(p["verts"], p["nrms"], p["subs"], flip=side < 0)
    if spec.get("bandaid"):
        p = place(parts["feature_extra_bandaid"], g, -(FACE["eyeX"] + 0.012), FACE["eyeY"] - 0.034, angle=-20)
        add(p["verts"], p["nrms"], p["subs"])
    if spec["nose"] != "none":
        p = place(parts["feature_nose_" + spec["nose"]], g, 0, FACE["noseY"]); add(p["verts"], p["nrms"], p["subs"])
    p = place(parts["feature_mouth_" + spec["mouth"]], g, 0, FACE["mouthY"]); add(p["verts"], p["nrms"], p["subs"])
    allsubs = [(km.MAT[m], np.vstack(ts)) for m, ts in subs.items()]
    return np.vstack(verts), np.vstack(nrms), np.vstack(bidx), np.vstack(bw), allsubs

FRECKLES = [(-0.006, -0.03), (0.004, -0.027), (0.013, -0.032), (-0.001, -0.036), (0.009, -0.039)]  # same as the game's

PREVIEWS = [
    dict(frame="average", bottom="pants", head="round", hair="messy", eyes="round", brows="thin", nose="button", mouth="smile", glasses="round", freckles=1),
    dict(frame="slim", bottom="skirt", head="oval", hair="pigtails", eyes="big", brows="thin", nose="small", mouth="grin", bandaid=1, circles=1),
    dict(frame="sturdy", bottom="pants", head="wide", hair="spiky", eyes="narrow", brows="thick", nose="pointy", mouth="frown", browAngle=14, glasses="shades"),
    dict(frame="average", bottom="skirt", head="round", hair="long", eyes="sleepy", brows="bushy", nose="button", mouth="cat"),
    dict(frame="slim", bottom="pants", head="oval", hair="ponytail", eyes="dot", brows="thin", nose="none", mouth="gasp", glasses="square", circles=1),
    dict(frame="sturdy", bottom="skirt", head="round", hair="buzz", eyes="big", brows="thick", nose="small", mouth="flat"),
]

def main():
    preview_dir = sys.argv[1] if len(sys.argv) > 1 else None
    parts, meshes, grids = {}, [], []
    for frame in FRAMES:
        for bottom in ("pants", "skirt"):
            m = build_body(frame, bottom); meshes.append(m); parts[m["name"]] = m
            print(m["name"], len(m["verts"]))
    for name in HEADS:
        m = build_head(name); meshes.append(m); parts[m["name"]] = m
        g = face_grid(name); grids.append(g); parts[g["name"]] = g
        print(m["name"], len(m["verts"]), "grid hits", int(np.sum(np.asarray(g["z"]) != 0)), "/", g["nx"] * g["ny"])
    for style in HAIR:
        m = build_hair(style); meshes.append(m); parts[m["name"]] = m
        print(m["name"], len(m["verts"]))
    add_glasses(parts["face_round"])
    for name in FEATURES:
        m = build_feature(name); meshes.append(m); parts[m["name"]] = m
    for m in meshes:
        assert len(m["verts"]) < 65000, m["name"]
    km.make_skeleton(adult=False)
    clips = [(name, period, *km.bake(fn)) for name, period, fn in km.CLIPS]
    size = write_kit(os.path.join(HERE, "..", "assets", "creator_kit.bin"), meshes, grids, clips)
    print("creator_kit.bin", size // 1024, "KB,", len(meshes), "meshes")
    if preview_dir:
        for i, spec in enumerate(PREVIEWS):
            v, n, bi, bw, subs = assemble(spec, parts)
            km.write_glb(os.path.join(preview_dir, "creator_%d.glb" % i), v, n, bi, bw, subs, clips)

if __name__ == "__main__":
    main()
