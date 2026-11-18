"""
Builds the Versus characters (a boy, a girl, and DLC characters like Dez): a smooth sculpted body (signed-distance shapes -> marching cubes),
a real skeleton with skin weights, and baked Idle / Walk / Run loops.

Writes:
  assets/kid_boy.bin, assets/kid_girl.bin  - loaded by the mod (src/ChildModel.cs), Unity space
  <out>/kid_boy.glb, <out>/kid_girl.glb    - the same models for previewing (any glTF viewer)

Coordinates while building: +X = the kid's right, +Y up, +Z forward (Unity's space).
Built 1 unit tall, then scaled to HEIGHT.

    python tools/kid_model.py [preview_dir]
"""
import json, math, os, struct, sys
import numpy as np
from skimage import measure

HEIGHT = 1.1       # final height in meters of the model being built (see HEIGHTS)
HEIGHTS = {"boy": 1.1, "girl": 1.1, "dez": 1.0, "kaz": 1.75, "rat": 1.0, "plague": 1.72, "fish": 1.65}  # dez/rat: 1 unit = 1 m (dez ears ~1.45 m); kaz: a grown-up
KIND = "boy"
VOXEL = 0.007     # sculpt resolution (at 1 unit tall)

# ── Colors (r, g, b, gloss) ────────────────────────────────────────────────
MATERIALS = [
    ("skin",   (0.96, 0.76, 0.62), 0.15),
    ("shirtA", (0.74, 0.13, 0.14), 0.10),
    ("shirtB", (0.93, 0.88, 0.76), 0.10),
    ("sleeve", (0.74, 0.13, 0.14), 0.10),
    ("pants",  (0.22, 0.31, 0.52), 0.10),
    ("shoe",   (0.92, 0.92, 0.90), 0.25),
    ("sole",   (0.30, 0.30, 0.33), 0.10),
    ("hair",   (0.30, 0.18, 0.09), 0.30),
    ("eye",    (0.06, 0.04, 0.04), 0.70),
    ("glint",  (1.00, 1.00, 1.00), 0.60),
    ("mouth",  (0.55, 0.24, 0.24), 0.20),
    ("bow",    (0.90, 0.35, 0.60), 0.30),
    ("sock",   (0.95, 0.95, 0.95), 0.10),
    ("earInner", (0.05, 0.47, 0.78), 0.20),
    ("tongue", (0.08, 0.45, 0.72), 0.40),
    ("nose",   (0.72, 0.86, 0.96), 0.20),
    ("letters", (0.04, 0.04, 0.05), 0.10),
    ("badge", (0.96, 0.96, 0.95), 0.15),
    ("tail", (0.88, 0.58, 0.58), 0.25),
    ("tailRing", (0.62, 0.32, 0.33), 0.20),
    ("lens", (1.0, 0.55, 0.12), 0.85),          # the plague doctor's glass
    ("scale", (0.62, 0.76, 0.92), 0.85),        # Sa1monfr's fish head: silvery blue
    ("scaleDark", (0.33, 0.46, 0.63), 0.40),
    ("lip", (0.68, 0.79, 0.93), 0.85),
    ("fin", (0.46, 0.62, 0.80), 0.45),
    ("skinDark", (0.72, 0.50, 0.35), 0.15),     # the lines between a strong man's muscles
]
HAIR = {"boy": (0.30, 0.18, 0.09), "girl": (0.42, 0.22, 0.10), "kaz": (0.36, 0.22, 0.11)}
MAT = {m[0]: i for i, m in enumerate(MATERIALS)}
STRIPE = 0.034     # shirt stripe height

# ── Skeleton (rest pose: A-pose, all rotations identity) ──────────────────
V = lambda x, y, z: np.array([x, y, z], dtype=float)
JOINT, SEG = {}, {}

def make_skeleton(adult=False, rat=False):
    """Kid proportions (big head, short legs), a grown-up's (Kaz) or the rat's (a blob on stubby feet).
    Same bones every way, so every animation works on all. Filled in place: the shapes read JOINT when sculpted."""
    JOINT.clear()
    if rat:
        JOINT.update({"hips": V(0, 0.22, 0), "spine": V(0, 0.35, 0), "chest": V(0, 0.5, 0),
                      "neck": V(0, 0.58, 0), "head": V(0, 0.6, 0.01), "headTop": V(0, 0.9, 0.01)})
        for s, sx in (("R", 1), ("L", -1)):
            JOINT["upperleg" + s] = V(0.12 * sx, 0.2, 0.04)
            JOINT["lowerleg" + s] = V(0.12 * sx, 0.12, 0.06)
            JOINT["foot" + s]     = V(0.13 * sx, 0.05, 0.1)
            JOINT["toe" + s]      = V(0.13 * sx, 0.015, 0.24)
            JOINT["upperarm" + s] = V(0.26 * sx, 0.47, 0.12)
            JOINT["lowerarm" + s] = V(0.18 * sx, 0.42, 0.24)
            JOINT["hand" + s]     = V(0.09 * sx, 0.37, 0.34)
            JOINT["handEnd" + s]  = V(0.07 * sx, 0.36, 0.37)
    elif adult:
        JOINT.update({"hips": V(0, 0.53, 0), "spine": V(0, 0.61, 0), "chest": V(0, 0.70, 0),
                      "neck": V(0, 0.83, 0), "head": V(0, 0.865, 0.005), "headTop": V(0, 1.0, 0.005)})
        for s, sx in (("R", 1), ("L", -1)):
            JOINT["upperleg" + s] = V(0.064 * sx, 0.515, 0)
            JOINT["lowerleg" + s] = V(0.066 * sx, 0.285, 0.006)
            JOINT["foot" + s]     = V(0.066 * sx, 0.045, 0)
            JOINT["toe" + s]      = V(0.066 * sx, 0.012, 0.1)
            JOINT["upperarm" + s] = V(0.155 * sx, 0.80, -0.008)
            JOINT["lowerarm" + s] = V(0.225 * sx, 0.64, -0.008)
            JOINT["hand" + s]     = V(0.282 * sx, 0.5, 0.0)
            JOINT["handEnd" + s]  = V(0.3 * sx, 0.43, 0.005)
    else:
        JOINT.update({"hips": V(0, 0.50, 0), "spine": V(0, 0.575, 0), "chest": V(0, 0.665, 0),
                      "neck": V(0, 0.765, 0), "head": V(0, 0.815, 0.005), "headTop": V(0, 1.0, 0.005)})
        for s, sx in (("R", 1), ("L", -1)):
            JOINT["upperleg" + s] = V(0.058 * sx, 0.485, 0)
            JOINT["lowerleg" + s] = V(0.058 * sx, 0.27, 0.006)
            JOINT["foot" + s]     = V(0.058 * sx, 0.055, 0)
            JOINT["toe" + s]      = V(0.058 * sx, 0.018, 0.085)
            JOINT["upperarm" + s] = V(0.118 * sx, 0.735, -0.005)
            JOINT["lowerarm" + s] = V(0.19 * sx, 0.60, -0.005)
            JOINT["hand" + s]     = V(0.252 * sx, 0.475, 0.0)
            JOINT["handEnd" + s]  = V(0.278 * sx, 0.405, 0.005)
    JOINT["root"] = V(0, 0, 0)
    SEG.clear()
    SEG.update({
        "hips": (V(0, JOINT["hips"][1] - 0.06, 0), JOINT["spine"]), "spine": (JOINT["spine"], JOINT["chest"]),
        "chest": (JOINT["chest"], JOINT["neck"]), "neck": (JOINT["neck"], JOINT["head"]),
        "head": (JOINT["head"], JOINT["headTop"]),
    })
    for s in ("R", "L"):
        SEG["upperleg" + s] = (JOINT["upperleg" + s], JOINT["lowerleg" + s])
        SEG["lowerleg" + s] = (JOINT["lowerleg" + s], JOINT["foot" + s])
        SEG["foot" + s] = (JOINT["foot" + s], JOINT["toe" + s])
        SEG["upperarm" + s] = (JOINT["upperarm" + s], JOINT["lowerarm" + s])
        SEG["lowerarm" + s] = (JOINT["lowerarm" + s], JOINT["hand" + s])
        SEG["hand" + s] = (JOINT["hand" + s], JOINT["handEnd" + s])

make_skeleton()

BONES = [  # name, parent
    ("root", None), ("hips", "root"), ("spine", "hips"), ("chest", "spine"),
    ("neck", "chest"), ("head", "neck"),
]
for s in ("R", "L"):
    BONES += [("upperleg" + s, "hips"), ("lowerleg" + s, "upperleg" + s), ("foot" + s, "lowerleg" + s),
              ("upperarm" + s, "chest"), ("lowerarm" + s, "upperarm" + s), ("hand" + s, "lowerarm" + s)]
BI = {b[0]: i for i, b in enumerate(BONES)}

# Each body region may only use its own bones (so the legs never drag each other).
REGION_BONES = {
    "torso": ["hips", "spine", "chest", "neck"],
    "head": ["neck", "head"],
}
for s in ("R", "L"):
    REGION_BONES["leg" + s] = ["hips", "upperleg" + s, "lowerleg" + s, "foot" + s]
    REGION_BONES["arm" + s] = ["chest", "upperarm" + s, "lowerarm" + s, "hand" + s]

# ── Signed distance shapes ────────────────────────────────────────────────
def sd_ellipsoid(p, c, r):
    q = (p - c) / r
    k0 = np.linalg.norm(q, axis=-1)
    k1 = np.linalg.norm(q / r, axis=-1)
    return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)

def sd_capsule(p, a, b, ra, rb):
    ba = b - a
    t = np.clip(((p - a) @ ba) / (ba @ ba), 0.0, 1.0)
    closest = a + t[..., None] * ba
    return np.linalg.norm(p - closest, axis=-1) - (ra + (rb - ra) * t)

def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b + (a - b) * h - k * h * (1.0 - h)

SHAPES = []  # (fn(p) -> d, material, region, bias, hard)
def shape(fn, mat, region, bias=0.0, hard=False):
    SHAPES.append((fn, mat, region, bias, hard or mat == "hair"))

def define_shapes(kind):
    global KIND, SMOOTH, FACE_Z0, VOXEL
    del SHAPES[:]
    KIND = kind
    SMOOTH = 0.05 if kind == "rat" else 0.016   # the rat is one soft blob
    VOXEL = 0.0085 if kind == "rat" else 0.007  # and a big one (Unity meshes stop at 65k vertices)
    FACE_Z0 = 0.6 if kind == "rat" else 0.36 if kind in ("plague", "fish") else 0.2     # his snout sticks out past 0.2 (and so do a beak and a pair of lips)
    if kind in ("plague", "fish"): VOXEL = 0.0072
    if kind == "rat":
        define_rat()
        return
    if kind == "dez":
        define_dez()
        return
    if kind == "kaz":
        define_kaz()
        return
    if kind == "plague":
        define_plague()
        return
    if kind == "fish":
        define_fish()
        return
    girl = kind == "girl"
    # Torso (the girl a touch narrower in the shoulders)
    cw = 0.105 if girl else 0.112
    shape(lambda p: sd_ellipsoid(p, V(0, 0.672, -0.004), V(cw, 0.092, 0.075)), "shirt", "torso")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.585, 0.006), V(0.1 if girl else 0.104, 0.085, 0.075)), "shirt", "torso")
    sh = 0.086 if girl else 0.092
    shape(lambda p: sd_capsule(p, V(-sh, 0.725, -0.006), V(sh, 0.725, -0.006), 0.04 if girl else 0.042, 0.04 if girl else 0.042), "shirt", "torso")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.49, 0.0), V(0.104 if girl else 0.108, 0.075, 0.077)), "pants", "torso", -0.002)
    if girl:
        shape(skirt_sd, "pants", "skirt", -0.003, hard=True)
    head_shapes(girl)
    limb_shapes(girl)

# ── Dez (DrDezmen's character): a white cat-bunny in an oversized red "DEZ" tee ──
DEZ_FUR = "skin"  # his white fur uses the skin slot

def define_dez():
    # Big egg head sitting right on the shirt (no neck), wider toward the cheeks.
    shape(lambda p: sd_ellipsoid(p, V(0, 0.91, 0.0), V(0.185, 0.215, 0.168)), DEZ_FUR, "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.815, 0.012), V(0.188, 0.13, 0.165)), DEZ_FUR, "head")
    # Tall pointy ears, flattened front to back, a blue inside showing on the front.
    for sx in (1, -1):
        shape((lambda sx: lambda p: ear_sd(p, sx, 1.0))(sx), DEZ_FUR, "head")
    # The tee: a boxy top over the chest and shoulders, then a loose hem down past the hips.
    shape(lambda p: sd_ellipsoid(p, V(0, 0.625, 0.0), V(0.15, 0.16, 0.108)), "shirt", "torso")
    shape(lambda p: sd_capsule(p, V(-0.105, 0.715, -0.004), V(0.105, 0.715, -0.004), 0.055, 0.055), "shirt", "torso")
    shape(tunic_sd, "shirt", "skirt", -0.003, hard=True)
    shape(lambda p: sd_ellipsoid(p, V(0, 0.49, 0.0), V(0.1, 0.07, 0.075)), DEZ_FUR, "torso", 0.01)
    for s_, sx in (("R", 1), ("L", -1)):
        J = (lambda s_: lambda n: JOINT[n + s_])(s_)
        # Wide short sleeves, stubby white arms, big round paws with three toe bumps.
        shape((lambda J: lambda p: sd_capsule(p, J("upperarm"), J("upperarm") + (J("lowerarm") - J("upperarm")) * 0.68, 0.068, 0.064))(J), "shirt", "arm" + s_, -0.003)
        shape((lambda J: lambda p: sd_capsule(p, J("upperarm"), J("hand"), 0.03, 0.027))(J), DEZ_FUR, "arm" + s_)
        shape((lambda J: lambda p: sd_ellipsoid(p, J("hand") + V(0, -0.04, 0.005), V(0.058, 0.062, 0.054)))(J), DEZ_FUR, "arm" + s_)
        for k in (-1, 0, 1):
            shape((lambda J, k, sx: lambda p: sd_ellipsoid(p, J("hand") + V(0.026 * k * sx - 0.004 * sx, -0.095, 0.022 + 0.008 * (1 - abs(k))), V(0.023, 0.021, 0.022)))(J, k, sx), DEZ_FUR, "arm" + s_)
        # Short legs out of the hem, big paw feet with toe bumps.
        shape((lambda J: lambda p: sd_capsule(p, J("upperleg") + V(0, -0.05, 0), J("foot") + V(0, 0.02, 0), 0.054, 0.05))(J), DEZ_FUR, "leg" + s_)
        def foot(p, J=J):
            c = J("foot") * np.array([1, 0, 1]) + V(0, 0.042, 0.03)
            return np.maximum(sd_ellipsoid(p, c, V(0.066, 0.05, 0.09)), -p[..., 1])
        shape(foot, DEZ_FUR, "leg" + s_)
        for k in (-1, 0, 1):
            shape((lambda J, k: lambda p: np.maximum(sd_ellipsoid(p, J("foot") * np.array([1, 0, 1]) + V(0.028 * k, 0.026, 0.1), V(0.022, 0.024, 0.022)), -p[..., 1]))(J, k), DEZ_FUR, "leg" + s_)

EAR_BASE, EAR_TIP = V(0.095, 1.06, -0.015), V(0.14, 1.44, -0.025)

# ── Kaz (Kazyzone): a muscular grown-up in an orange tank top with a KAZ badge ──
def define_kaz():
    # Head: skull, a strong square jaw, nose, ears.
    shape(lambda p: sd_ellipsoid(p, V(0, 0.94, 0.002), V(0.054, 0.072, 0.064)), "skin", "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.893, 0.018), V(0.044, 0.05, 0.05)), "skin", "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.868, 0.032), V(0.026, 0.02, 0.028)), "skin", "head")  # chin
    shape(lambda p: sd_capsule(p, V(0, 0.935, 0.064), V(0, 0.908, 0.074), 0.008, 0.01), "skin", "head")
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.054 * sx, 0.93, 0.0), V(0.011, 0.02, 0.014)))(sx), "skin", "head")
    # Hair: short on the sides, a swept-back pompadour on top.
    shape(kaz_hair_sd, "hair", "head", -0.004)
    shape(lambda p: sd_ellipsoid(p, V(0.012, 0.995, 0.03), V(0.056, 0.032, 0.05)), "hair", "head", -0.004)
    shape(lambda p: sd_ellipsoid(p, V(-0.01, 1.0, -0.01), V(0.058, 0.03, 0.06)), "hair", "head", -0.004)
    # Thick neck, traps and big round shoulders (skin: it's a tank top).
    shape(lambda p: sd_capsule(p, V(0, 0.8, -0.006), V(0, 0.9, -0.004), 0.04, 0.036), "skin", "head")
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_capsule(p, V(0.03 * sx, 0.84, -0.01), V(0.13 * sx, 0.81, -0.01), 0.032, 0.03))(sx), "skin", "torso", -0.004)
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.158 * sx, 0.785, -0.004), V(0.056, 0.06, 0.056)))(sx), "skin", "arm" + ("R" if sx > 0 else "L"), -0.006)
    # Torso in the tank top: broad chest, pecs, narrower waist.
    shape(lambda p: sd_ellipsoid(p, V(0, 0.735, 0.0), V(0.15, 0.1, 0.088)), "shirt", "torso")
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.058 * sx, 0.74, 0.04), V(0.07, 0.055, 0.055)))(sx), "shirt", "torso")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.635, 0.0), V(0.108, 0.095, 0.066)), "shirt", "torso")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.545, 0.0), V(0.118, 0.07, 0.085)), "pants", "torso", -0.002)
    for s_, sx in (("R", 1), ("L", -1)):
        J = (lambda s_: lambda n: JOINT[n + s_])(s_)
        # Muscular arms: a big bicep, thick forearm, a hand with a thumb.
        shape((lambda J: lambda p: sd_capsule(p, J("upperarm"), J("lowerarm"), 0.05, 0.04))(J), "skin", "arm" + s_)
        shape((lambda J: lambda p: sd_ellipsoid(p, J("upperarm") + (J("lowerarm") - J("upperarm")) * 0.5 + V(0, 0, 0.018), V(0.045, 0.06, 0.045)))(J), "skin", "arm" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("lowerarm"), J("hand"), 0.04, 0.03))(J), "skin", "arm" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("hand") + V(0, -0.01, 0), J("handEnd"), 0.03, 0.024))(J), "skin", "arm" + s_)
        shape((lambda J, sx: lambda p: sd_capsule(p, J("hand") + V(0, -0.018, 0.014), J("hand") + V(-0.008 * sx, -0.05, 0.034), 0.012, 0.01))(J, sx), "skin", "arm" + s_)
        # Pants and shoes.
        shape((lambda J: lambda p: sd_capsule(p, J("upperleg") + V(0, 0.01, 0), J("lowerleg"), 0.07, 0.052))(J), "pants", "leg" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("lowerleg"), J("foot") + V(0, 0.012, 0), 0.05, 0.042))(J), "pants", "leg" + s_)
        def shoe(p, J=J):
            c = J("foot") * np.array([1, 0, 1]) + V(0, 0.03, 0.032)
            return np.maximum(sd_ellipsoid(p, c, V(0.046, 0.036, 0.095)), -p[..., 1])
        shape(shoe, "shoe", "leg" + s_, -0.003)

# ── The Rat King (fan art): a fat golden rat with a tiny crown, a drooling grin and a game controller ──
def sd_ellipsoid_rot(p, c, r, R):
    """An ellipsoid whose local axes are R's columns."""
    return sd_ellipsoid((p - c) @ R, V(0, 0, 0), r)

def tilt_axes(sx, deg):
    """Local side / up / forward axes leaning out toward +X (sx=1) or -X (sx=-1)."""
    a = math.radians(deg)
    side, up, fwd = V(math.cos(a), -sx * math.sin(a), 0), V(sx * math.sin(a), math.cos(a), 0), V(0, 0, 1)
    return np.stack([side, up, fwd], axis=-1)

EAR_C = V(0.2, 0.86, -0.02)
EAR_R = V(0.085, 0.095, 0.035)
CROWN_C = V(0.06, 0.85, 0.02)
CROWN_AX = tilt_axes(1, 20)
TAIL = [V(0.0, 0.14, -0.24), V(0.06, 0.075, -0.42), V(0.2, 0.048, -0.53), V(0.36, 0.042, -0.5),
        V(0.46, 0.037, -0.36), V(0.5, 0.032, -0.18), V(0.48, 0.028, -0.02), V(0.43, 0.024, 0.1)]
TAIL_R = (0.05, 0.012)
RING = 0.042  # tail ring spacing

def tail_r(k):
    return TAIL_R[0] + (TAIL_R[1] - TAIL_R[0]) * k / (len(TAIL) - 1)

def tail_sd(p):
    d = None
    for k in range(len(TAIL) - 1):
        dk = sd_capsule(p, TAIL[k], TAIL[k + 1], tail_r(k), tail_r(k + 1))
        d = dk if d is None else np.minimum(d, dk)
    return d

def tail_ring(p):
    """True where a point on the tail is on one of its dark rings."""
    best, s_at, run = 1e9, 0.0, 0.0
    for k in range(len(TAIL) - 1):
        a, b = TAIL[k], TAIL[k + 1]
        L = np.linalg.norm(b - a)
        t = float(np.clip(np.dot(p - a, b - a) / (L * L), 0, 1))
        d = np.linalg.norm(p - (a + (b - a) * t))
        if d < best: best, s_at = d, run + t * L
        run += L
    return (s_at / RING) % 1.0 < 0.2 and s_at > 0.14

def crown_sd(p):
    q = (p - CROWN_C) @ CROWN_AX
    rad = np.sqrt(q[..., 0] ** 2 + q[..., 2] ** 2)
    d = np.maximum(rad - 0.055, np.abs(q[..., 1] - 0.018) - 0.018)
    for k in range(5):
        a = 2 * math.pi * k / 5 + 0.3
        lo = CROWN_C + CROWN_AX @ V(0.05 * math.cos(a), 0.03, 0.05 * math.sin(a))
        hi = CROWN_C + CROWN_AX @ V(0.044 * math.cos(a), 0.078, 0.044 * math.sin(a))
        d = np.minimum(d, sd_capsule(p, lo, hi, 0.014, 0.002))
    return d

def define_rat():
    F = "skin"  # his golden fur uses the skin slot
    # One soft blob: the bottom (flat on the floor), the belly, the upper body, the head, a double chin, the snout.
    shape(lambda p: np.maximum(sd_ellipsoid(p, V(0, 0.14, 0.02), V(0.36, 0.15, 0.3)), -p[..., 1]), F, "torso")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.27, 0.0), V(0.4, 0.26, 0.33)), F, "torso")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.47, 0.0), V(0.32, 0.2, 0.27)), F, "torso")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.66, 0.02), V(0.25, 0.2, 0.22)), F, "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.53, 0.14), V(0.2, 0.07, 0.12)), F, "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.6, 0.2), V(0.1, 0.085, 0.13)), F, "head")
    # Round ears leaning out.
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid_rot(p, EAR_C * V(sx, 1, 1), EAR_R, tilt_axes(sx, 25)))(sx), F, "head")
    # The crown, tilted over his right ear (the viewer's left).
    shape(crown_sd, "shirtA", "head", -0.004, hard=True)
    # Stubby arms around the belly, paws on the controller.
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_capsule(p, V(0.26 * sx, 0.47, 0.12), V(0.085 * sx, 0.37, 0.33), 0.052, 0.04))(sx), F, "torso")
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.085 * sx, 0.365, 0.35), V(0.042, 0.036, 0.04)))(sx), F, "torso")
    # The controller: a body and two grips.
    shape(lambda p: sd_ellipsoid(p, V(0, 0.375, 0.372), V(0.08, 0.024, 0.042)), "pants", "torso", -0.004, hard=True)
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.062 * sx, 0.358, 0.36), V(0.032, 0.03, 0.046)))(sx), "pants", "torso", -0.004, hard=True)
    # Little feet poking out under the belly.
    for s_, sx in (("R", 1), ("L", -1)):
        shape((lambda sx: lambda p: np.maximum(sd_ellipsoid(p, V(0.13 * sx, 0.04, 0.23), V(0.075, 0.045, 0.1)), -p[..., 1]))(sx), F, "leg" + s_)
    # The long pink tail, curled around on the floor.
    shape(tail_sd, "tail", "torso")

def rat_details():
    parts = []
    # Ears: the pink inside.
    for sx in (1, -1):
        R = tilt_axes(sx, 25)
        c = EAR_C * V(sx, 1, 1) + V(0, -0.005, EAR_R[2] - 0.004)
        v, n, f = ellipsoid_mesh(c, V(0.058, 0.068, 0.007), R, 14, 10); parts.append((v, n, f, "earInner", "head"))
    # Eyes: whites, pupils glancing to his right (the viewer's left), bags underneath. The left one is half shut.
    for sx in (1, -1):
        ex = 0.1 * sx
        v, n, f = ellipsoid_mesh(on_surface(ex, 0.72, -0.006), V(0.042, 0.036, 0.014), rot_facing(V(ex * 2.0, 0.1, 1)))
        parts.append((v, n, f, "glint", "head"))
        pc = on_surface(ex + 0.02, 0.722 if sx > 0 else 0.712, 0.013)
        v, n, f = ellipsoid_mesh(pc, V(0.012, 0.013, 0.005), rot_facing(V(ex * 2.0, 0.1, 1)), 10, 6); parts.append((v, n, f, "eye", "head"))
        bag = [on_surface(ex + dx, 0.684 + dy, 0.001) for dx, dy in ((-0.045, 0.012), (-0.02, -0.004), (0.01, -0.008), (0.04, 0.0))]
        for a, b in zip(bag, bag[1:]):
            v, n, f = capsule_mesh(a, b, 0.0032); parts.append((v, n, f, "eye", "head"))
        if sx < 0:  # the droopy lid, and its edge
            v, n, f = ellipsoid_mesh(on_surface(ex, 0.738, 0.002), V(0.048, 0.024, 0.012), rot_facing(V(ex * 2.0, 0.1, 1)))
            parts.append((v, n, f, "skin", "head"))
            lid = [on_surface(ex + dx, 0.717 + dy, 0.003) for dx, dy in ((-0.046, 0.006), (-0.015, 0.0), (0.015, 0.0), (0.046, 0.006))]
            for a, b in zip(lid, lid[1:]):
                v, n, f = capsule_mesh(a, b, 0.0034); parts.append((v, n, f, "eye", "head"))
    # Shiny black nose, the open mouth, and the drool.
    nc = on_surface(0, 0.622, -0.012)
    v, n, f = ellipsoid_mesh(nc, V(0.038, 0.028, 0.028), rot_facing(V(0, 0.2, 1))); parts.append((v, n, f, "nose", "head"))
    v, n, f = ellipsoid_mesh(nc + V(0.012, 0.012, 0.024), V(0.008, 0.006, 0.004), rot_facing(V(0, 0.3, 1)), 8, 6); parts.append((v, n, f, "glint", "head"))
    mc = on_surface(0, 0.553, -0.004)
    v, n, f = ellipsoid_mesh(mc, V(0.052, 0.03, 0.016), rot_facing(V(0, -0.5, 1))); parts.append((v, n, f, "mouth", "head"))
    d0, d1 = mc + V(-0.018, -0.012, 0.004), mc + V(-0.026, -0.075, 0.01)
    v, n, f = capsule_mesh(d0, d1, 0.009); parts.append((v, n, f, "tongue", "head"))
    v, n, f = ellipsoid_mesh(d1 + V(0, -0.008, 0), V(0.016, 0.02, 0.016)); parts.append((v, n, f, "tongue", "head"))
    # Whiskers.
    for sx in (1, -1):
        for dy, spread in ((0.018, 0.03), (0.0, 0.0), (-0.018, -0.03)):
            a = on_surface(0.075 * sx, 0.6 + dy, -0.004)
            b = V(0.25 * sx, 0.6 + dy + spread, a[2] - 0.06)
            v, n, f = capsule_mesh(a, b, 0.0024); parts.append((v, n, f, "eye", "head"))
    # Two thumbsticks on the controller.
    for sx in (1, -1):
        v, n, f = ellipsoid_mesh(V(0.03 * sx, 0.398, 0.38), V(0.011, 0.006, 0.011), np.eye(3), 10, 6); parts.append((v, n, f, "eye", "chest"))
    # Claws on each foot.
    for s_, sx in (("R", 1), ("L", -1)):
        for k in (-1, 0, 1):
            x = 0.13 * sx + 0.026 * k
            a = on_surface(x, 0.05, -0.012)
            b = a + V(0.004 * k, -0.02, 0.018)
            v, n, f = capsule_mesh(a, b, 0.005); parts.append((v, n, f, "eye", "foot" + s_))
    return parts

# ── ImPoPzzZ: a plague doctor. Beaked mask with amber lenses, wide hat, long black coat, cape, gloves and boots ──
def sd_cyl(p, c, r, hh):
    """An upright cylinder: radius r round its middle c, half height hh."""
    dx, dz = p[..., 0] - c[0], p[..., 2] - c[2]
    return np.maximum(np.sqrt(dx * dx + dz * dz) - r, np.abs(p[..., 1] - c[1]) - hh)

def rot_x(deg):
    """Local axes turned about X (pitch), as columns."""
    a = math.radians(deg)
    return np.stack([V(1, 0, 0), V(0, math.cos(a), math.sin(a)), V(0, -math.sin(a), math.cos(a))], axis=-1)

def plague_cape_sd(p):
    """A short cloak over the shoulders: a cone flaring from the neck to the elbows."""
    y = p[..., 1]
    top, bot = 0.815, 0.655
    t = np.clip((top - y) / (top - bot), 0.0, 1.0)
    rx, rz = 0.1 + 0.115 * t, 0.078 + 0.075 * t
    q = np.sqrt((p[..., 0] / rx) ** 2 + ((p[..., 2] + 0.004) / rz) ** 2)
    return np.maximum((q - 1.0) * np.minimum(rx, rz), np.maximum(y - top, bot - y))

def plague_coat_sd(p):
    y = p[..., 1]
    top, bot = 0.62, 0.21
    t = np.clip((top - y) / (top - bot), 0.0, 1.0)
    rx, rz = 0.118 + 0.055 * t, 0.084 + 0.04 * t
    q = np.sqrt((p[..., 0] / rx) ** 2 + ((p[..., 2] - 0.002) / rz) ** 2)
    return np.maximum((q - 1.0) * np.minimum(rx, rz), np.maximum(y - top, bot - y))

def define_plague():
    # The head: a dark hood, the pale mask over the face, and the long curved beak.
    shape(lambda p: sd_ellipsoid(p, V(0, 0.935, -0.004), V(0.055, 0.064, 0.058)), "shoe", "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.935, 0.034), V(0.056, 0.063, 0.05)), "skin", "head")
    beak = [(V(0, 0.93, 0.07), 0.042), (V(0, 0.92, 0.13), 0.034), (V(0, 0.9, 0.19), 0.022), (V(0, 0.87, 0.24), 0.007)]
    for (a, ra), (b, rb) in zip(beak, beak[1:]):
        shape((lambda a, b, ra, rb: lambda p: sd_capsule(p, a, b, ra, rb))(a, b, ra, rb), "skin", "head")
    # A high collar round the neck.
    shape(lambda p: sd_capsule(p, V(0, 0.79, -0.006), V(0, 0.885, -0.004), 0.034, 0.03), "shoe", "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.835, 0.0), V(0.05, 0.022, 0.046)), "shirtB", "head", -0.004)
    # The hat: a wide flat brim, a tall crown, and a band.
    shape(lambda p: sd_cyl(p, V(0, 0.99, -0.004), 0.15, 0.008), "shoe", "head", -0.004, hard=True)
    shape(lambda p: sd_cyl(p, V(0, 1.02, -0.004), 0.07, 0.036), "shoe", "head", -0.004, hard=True)
    shape(lambda p: sd_cyl(p, V(0, 1.006, -0.004), 0.073, 0.01), "shirtB", "head", -0.007, hard=True)
    # The coat: shoulders and chest, a cape over the shoulders, then the long skirts.
    shape(lambda p: sd_ellipsoid(p, V(0, 0.725, 0.0), V(0.13, 0.105, 0.08)), "shirt", "torso")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.63, 0.004), V(0.108, 0.09, 0.072)), "shirt", "torso")
    shape(lambda p: sd_capsule(p, V(-0.12, 0.775, -0.004), V(0.12, 0.775, -0.004), 0.042, 0.042), "shirt", "torso")
    shape(plague_cape_sd, "shirtB", "torso", -0.004, hard=True)
    shape(plague_coat_sd, "shirt", "skirt", -0.003, hard=True)
    # A belt with a pouch on each hip.
    shape(lambda p: sd_ellipsoid(p, V(0, 0.585, 0.004), V(0.115, 0.016, 0.079)), "shirtB", "torso", -0.006)
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.098 * sx, 0.54, 0.05), V(0.032, 0.045, 0.028)))(sx), "shoe", "torso", -0.004)
    for s_, sx in (("R", 1), ("L", -1)):
        J = (lambda s_: lambda n: JOINT[n + s_])(s_)
        # Long sleeves, a cuff, then gloves.
        shape((lambda J: lambda p: sd_capsule(p, J("upperarm"), J("lowerarm"), 0.042, 0.035))(J), "shirt", "arm" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("lowerarm"), J("hand"), 0.035, 0.031))(J), "shirt", "arm" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("lowerarm") + (J("hand") - J("lowerarm")) * 0.76, J("hand"), 0.039, 0.037))(J), "shirtB", "arm" + s_, -0.004)
        shape((lambda J: lambda p: sd_capsule(p, J("hand") + V(0, -0.006, 0), J("handEnd"), 0.03, 0.022))(J), "shoe", "arm" + s_)
        shape((lambda J, sx: lambda p: sd_capsule(p, J("hand") + V(0, -0.018, 0.014), J("hand") + V(-0.008 * sx, -0.05, 0.034), 0.012, 0.01))(J, sx), "shoe", "arm" + s_)
        # Trousers, tall boots with a flared top.
        shape((lambda J: lambda p: sd_capsule(p, J("upperleg") + V(0, 0.01, 0), J("lowerleg"), 0.05, 0.042))(J), "pants", "leg" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("lowerleg") + V(0, 0.02, 0), J("foot") + V(0, 0.012, 0), 0.047, 0.04))(J), "shoe", "leg" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("lowerleg") + V(0, 0.035, 0), J("lowerleg") + V(0, -0.03, 0), 0.058, 0.054))(J), "shoe", "leg" + s_)
        def boot(p, J=J):
            c = J("foot") * np.array([1, 0, 1]) + V(0, 0.03, 0.034)
            return np.maximum(sd_ellipsoid(p, c, V(0.047, 0.037, 0.098)), -p[..., 1])
        shape(boot, "shoe", "leg" + s_, -0.003)

def plague_details():
    parts = []
    # Two round glass lenses in dark rims, on the mask either side of the beak.
    for sx in (1, -1):
        ex, ey = 0.033 * sx, 0.955
        r = rot_facing(V(ex * 2.2, 0, 1))
        v, n, f = ellipsoid_mesh(on_surface(ex, ey, 0.001), V(0.03, 0.03, 0.008), r)
        parts.append((v, n, f, "shoe", "head"))
        v, n, f = ellipsoid_mesh(on_surface(ex, ey, 0.005), V(0.0235, 0.0235, 0.006), r)
        parts.append((v, n, f, "lens", "head"))
        v, n, f = ellipsoid_mesh(on_surface(ex - 0.007 * sx, ey + 0.008, 0.012), V(0.007, 0.005, 0.002), r, 8, 6)
        parts.append((v, n, f, "glint", "head"))
    # A row of buttons down the front of the coat.
    for i in range(8):
        y = 0.69 - i * 0.055
        bone = "chest" if y > 0.6 else "hips"
        v, n, f = ellipsoid_mesh(on_surface(0.0, y, 0.001), V(0.0095, 0.0095, 0.004), rot_facing(V(0, 0, 1)), 8, 6)
        parts.append((v, n, f, "shirtB", bone))
    return parts

# ── Sa1monfr: a very strong man with a silver fish's head ──
def rot_fin(sx, deg):
    return tilt_axes(sx, deg)

def fish_head_y(z):
    """About where the top of the head is, at this z (for standing the fin's spikes on it)."""
    return 0.92 + 0.108 * math.sqrt(max(0.02, 1.0 - ((z - 0.02) / 0.125) ** 2))

def define_fish():
    C = V(0, 0.92, 0.02)
    # ── The head: one big round fish head, bulging eyes, thick puffy lips ──
    shape(lambda p: sd_ellipsoid(p, C, V(0.118, 0.108, 0.125)), "scale", "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.965, 0.05), V(0.09, 0.065, 0.085)), "scale", "head")          # the brow
    shape(lambda p: sd_ellipsoid(p, V(0, 0.85, 0.06), V(0.085, 0.05, 0.085)), "scale", "head")          # the jowl
    shape(lambda p: sd_ellipsoid(p, V(0, 0.89, 0.1), V(0.066, 0.05, 0.05)), "scale", "head")             # the snout
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.078 * sx, 0.88, 0.055), V(0.058, 0.055, 0.07)))(sx), "scale", "head")    # cheeks
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.062 * sx, 0.955, 0.1), V(0.05, 0.047, 0.04)))(sx), "scale", "head")      # the eyes bulge out of their own sockets
    shape(lambda p: sd_ellipsoid(p, V(0, 0.887, 0.152), V(0.05, 0.014, 0.027)), "lip", "head")           # upper lip
    shape(lambda p: sd_ellipsoid(p, V(0, 0.861, 0.146), V(0.047, 0.018, 0.027)), "lip", "head")          # lower lip
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.05 * sx, 0.874, 0.138), V(0.013, 0.013, 0.018)))(sx), "lip", "head")
    # ── Fins: a crest along the top of the head, one on each side, a tail at the back ──
    zs, hs = [0.07, 0.03, -0.01, -0.05, -0.09], [0.085, 0.108, 0.1, 0.078, 0.05]
    tips = []
    for z, h in zip(zs, hs):
        base, tip = V(0, fish_head_y(z) - 0.022, z), V(0, fish_head_y(z) + h, z - 0.035)
        tips.append((base, tip))
        shape((lambda a, b: lambda p: sd_capsule(p, a, b, 0.014, 0.004))(base, tip), "fin", "head")
    for (b0, t0), (b1, t1) in zip(tips, tips[1:]):
        c = (b0 + b1 + t0 + t1) / 4 + V(0, -0.006, 0)
        half = (np.linalg.norm(t0 - b0) + np.linalg.norm(t1 - b1)) / 4
        shape((lambda c, half, dz: lambda p: sd_ellipsoid(p, c, V(0.011, half, dz)))(c, half * 0.95, abs(b0[2] - b1[2]) * 0.6), "fin", "head")
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid_rot(p, V(0.132 * sx, 0.86, -0.03), V(0.012, 0.058, 0.064), tilt_axes(sx, 28)))(sx), "fin", "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.925, -0.14), V(0.03, 0.04, 0.04)), "scale", "head")
    shape(lambda p: sd_ellipsoid_rot(p, V(0, 0.985, -0.185), V(0.013, 0.052, 0.036), rot_x(38)), "fin", "head")
    shape(lambda p: sd_ellipsoid_rot(p, V(0, 0.875, -0.185), V(0.013, 0.052, 0.036), rot_x(-38)), "fin", "head")
    # ── The body: a thick neck, trapezius, huge shoulders; a cropped tank top; shorts ──
    shape(lambda p: sd_capsule(p, V(0, 0.76, -0.006), V(0, 0.86, -0.004), 0.05, 0.046), "skin", "head")
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_capsule(p, V(0.03 * sx, 0.84, -0.01), V(0.13 * sx, 0.81, -0.01), 0.034, 0.032))(sx), "skin", "torso", -0.004)
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.162 * sx, 0.785, -0.004), V(0.06, 0.064, 0.06)))(sx), "skin", "arm" + ("R" if sx > 0 else "L"), -0.006)
    shape(lambda p: sd_ellipsoid(p, V(0, 0.735, 0.0), V(0.152, 0.1, 0.09)), "skin", "torso")            # the chest
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.06 * sx, 0.74, 0.044), V(0.072, 0.056, 0.056)))(sx), "skin", "torso")   # the pecs
    shape(lambda p: sd_ellipsoid(p, V(0, 0.635, 0.0), V(0.11, 0.095, 0.068)), "skin", "torso")           # the abs
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.098 * sx, 0.66, 0.0), V(0.04, 0.08, 0.052)))(sx), "skin", "torso")   # the lats
    # A wide championship belt with a big buckle.
    shape(lambda p: sd_ellipsoid(p, V(0, 0.572, 0.002), V(0.126, 0.026, 0.092)), "shirt", "torso", -0.006, hard=True)
    shape(lambda p: sd_ellipsoid(p, V(0, 0.572, 0.088), V(0.042, 0.03, 0.014)), "sleeve", "torso", -0.008, hard=True)
    shape(lambda p: sd_ellipsoid(p, V(0, 0.545, 0.0), V(0.122, 0.072, 0.088)), "pants", "torso", -0.002)
    for s_, sx in (("R", 1), ("L", -1)):
        J = (lambda s_: lambda n: JOINT[n + s_])(s_)
        # Arms: a huge bicep, thick forearm, a fist with a thumb, a wristband.
        shape((lambda J: lambda p: sd_capsule(p, J("upperarm"), J("lowerarm"), 0.054, 0.042))(J), "skin", "arm" + s_)
        shape((lambda J: lambda p: sd_ellipsoid(p, J("upperarm") + (J("lowerarm") - J("upperarm")) * 0.5 + V(0, 0, 0.02), V(0.05, 0.065, 0.05)))(J), "skin", "arm" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("lowerarm"), J("hand"), 0.043, 0.032))(J), "skin", "arm" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("hand") + V(0, -0.01, 0), J("handEnd"), 0.032, 0.026))(J), "skin", "arm" + s_)
        shape((lambda J, sx: lambda p: sd_capsule(p, J("hand") + V(0, -0.018, 0.014), J("hand") + V(-0.008 * sx, -0.05, 0.034), 0.013, 0.011))(J, sx), "skin", "arm" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("lowerarm") + (J("hand") - J("lowerarm")) * 0.7, J("hand"), 0.04, 0.037))(J), "shirt", "arm" + s_, -0.004)
        # Legs: board shorts to the knee, strong bare calves, sneakers.
        shape((lambda J: lambda p: sd_capsule(p, J("upperleg") + V(0, 0.01, 0), J("upperleg") + (J("lowerleg") - J("upperleg")) * 0.72, 0.078, 0.07))(J), "pants", "leg" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("upperleg") + (J("lowerleg") - J("upperleg")) * 0.55, J("lowerleg"), 0.068, 0.056))(J), "skin", "leg" + s_)
        shape((lambda J: lambda p: sd_capsule(p, J("lowerleg"), J("foot") + V(0, 0.012, 0), 0.058, 0.042))(J), "skin", "leg" + s_)
        def shoe_(p, J=J):
            c = J("foot") * np.array([1, 0, 1]) + V(0, 0.032, 0.034)
            return np.maximum(sd_ellipsoid(p, c, V(0.05, 0.038, 0.1)), -p[..., 1])
        shape(shoe_, "shoe", "leg" + s_, -0.003)

def fish_details():
    parts = []
    for sx in (1, -1):
        ex, ey = 0.062 * sx, 0.955
        z = face_z(ex, ey)
        r = rot_facing(V(ex * 1.5, 0.1, 1))
        # A big round eye looking off to one side, under a heavy drooping lid, with bags beneath.
        v, n, f = ellipsoid_mesh(V(ex, ey, z + 0.002), V(0.041, 0.044, 0.02), r)
        parts.append((v, n, f, "glint", "head"))
        v, n, f = ellipsoid_mesh(V(ex + 0.009, ey - 0.007, z + 0.016), V(0.019, 0.021, 0.008), r, 10, 8)
        parts.append((v, n, f, "eye", "head"))
        v, n, f = ellipsoid_mesh(V(ex + 0.014, ey + 0.001, z + 0.022), V(0.0055, 0.0055, 0.003), r, 8, 6)
        parts.append((v, n, f, "glint", "head"))
        v, n, f = ellipsoid_mesh(V(ex, ey + 0.026, z + 0.012), V(0.048, 0.027, 0.027), r)
        parts.append((v, n, f, "scale", "head"))
        lid = [V(ex + dx, ey - 0.001 - 0.004 * (1 - (dx / 0.044) ** 2), z + 0.017) for dx in (-0.044, -0.022, 0.0, 0.022, 0.044)]
        for a, b in zip(lid, lid[1:]):
            v, n, f = capsule_mesh(a, b, 0.0028); parts.append((v, n, f, "scaleDark", "head"))
        bag = [on_surface(ex + dx, ey - 0.04 + 0.004 * (1 - (dx / 0.035) ** 2) * -1, 0.001) for dx in (-0.035, -0.012, 0.012, 0.035)]
        for a, b in zip(bag, bag[1:]):
            v, n, f = capsule_mesh(a, b, 0.0028); parts.append((v, n, f, "scaleDark", "head"))
    # The mouth: a dark seam between the lips, nostrils, and gill lines down each side.
    m = [on_surface(x, 0.8735 - 0.007 * (x / 0.05) ** 2 * 0.5, 0.0) for x in (-0.05, -0.025, 0.0, 0.025, 0.05)]
    for a, b in zip(m, m[1:]):
        v, n, f = capsule_mesh(a, b, 0.0022); parts.append((v, n, f, "eye", "head"))
    for sx in (1, -1):
        v, n, f = ellipsoid_mesh(on_surface(0.022 * sx, 0.908, 0.0), V(0.0055, 0.0045, 0.003), rot_facing(V(0, 0.2, 1)), 8, 6); parts.append((v, n, f, "scaleDark", "head"))
        try:
            gill = [on_surface(x * sx, y, 0.0005) for x, y in ((0.104, 0.93), (0.111, 0.9), (0.112, 0.87), (0.105, 0.84))]
            for a, b in zip(gill, gill[1:]):
                v, n, f = capsule_mesh(a, b, 0.0028); parts.append((v, n, f, "scaleDark", "head"))
        except ValueError:
            pass
    # Lines between the muscles: the breastbone, under each pec, and the stomach.
    sb = [on_surface(0, y, 0.0) for y in (0.715, 0.69, 0.665)]
    for a, b in zip(sb, sb[1:]):
        v, n, f = capsule_mesh(a, b, 0.0026); parts.append((v, n, f, "skinDark", "chest"))
    for sx in (1, -1):
        pec = [on_surface(x * sx, y, 0.0) for x, y in ((0.012, 0.688), (0.045, 0.683), (0.085, 0.692), (0.118, 0.712))]
        for a, b in zip(pec, pec[1:]):
            v, n, f = capsule_mesh(a, b, 0.0026); parts.append((v, n, f, "skinDark", "chest"))
    ab = [on_surface(0, y, 0.0) for y in (0.662, 0.62, 0.598)]
    for a, b in zip(ab, ab[1:]):
        v, n, f = capsule_mesh(a, b, 0.0026); parts.append((v, n, f, "skinDark", "chest"))
    for y in (0.648, 0.622, 0.598):
        for sx in (1, -1):
            a, b = on_surface(0.008 * sx, y, 0.0), on_surface(0.056 * sx, y - 0.005, 0.0)
            v, n, f = capsule_mesh(a, b, 0.0024); parts.append((v, n, f, "skinDark", "chest"))
    return parts

def kaz_hair_sd(p):
    d = sd_ellipsoid(p, V(0, 0.96, -0.006), V(0.06, 0.058, 0.07))
    z = p[..., 2]
    line = np.interp(z, [-0.08, -0.02, 0.03, 0.08], [0.9, 0.925, 0.965, 0.975])
    return np.maximum(d, line - p[..., 1])

def kaz_details():
    parts = []
    # Narrowed eyes (white with a black pupil) under heavy angry brows that slant down to the middle.
    for sx in (1, -1):
        ex, ey = 0.023 * sx, 0.935
        r = rot_facing(V(ex * 2.0, 0, 1))
        v, n, f = ellipsoid_mesh(on_surface(ex, ey, -0.002), V(0.012, 0.0048, 0.004), r)
        parts.append((v, n, f, "glint", "head"))
        v, n, f = ellipsoid_mesh(on_surface(ex - 0.0015 * sx, ey + 0.0005, 0.0006), V(0.0038, 0.0042, 0.003), r, 8, 6)
        parts.append((v, n, f, "eye", "head"))
        lid = [on_surface(ex + dx * sx, ey + dy, 0.0008) for dx, dy in ((-0.012, 0.002), (0.0, 0.005), (0.013, 0.003))]
        for a, b in zip(lid, lid[1:]):
            v, n, f = capsule_mesh(a, b, 0.0017); parts.append((v, n, f, "eye", "head"))
        brow = [on_surface(ex + dx * sx, ey + dy, 0.0) for dx, dy in ((-0.017, 0.006), (-0.002, 0.0125), (0.017, 0.019))]
        for a, b in zip(brow, brow[1:]):
            v, n, f = capsule_mesh(a, b, 0.0048); parts.append((v, n, f, "hair", "head"))
    # A small stern frown: a thin dark line, corners down.
    m = [on_surface(x, y, 0.0) for x, y in ((-0.014, 0.876), (-0.005, 0.8795), (0.005, 0.8795), (0.014, 0.876))]
    for a, b in zip(m, m[1:]):
        v, n, f = capsule_mesh(a, b, 0.0014); parts.append((v, n, f, "eye", "head"))
    # The KAZ badge: a white disc on the left of his chest (the viewer's right), black letters.
    cx, cy, R = -0.07, 0.725, 0.036
    c = on_surface(cx, cy, 0.001)
    rot = rot_facing(V(cx * 3.0, 0, 1))
    ax, ay, az = rot[:, 0], rot[:, 1], rot[:, 2]  # the disc's own flat plane (ax = the model's +X)
    v, n, f = ellipsoid_mesh(c, V(R, R, 0.003), rot, 18, 8)
    parts.append((v, n, f, "badge", "chest"))
    # Everything on the badge sits on its flat face (the chest curves, the disc doesn't).
    def P(vx, vy, up=0.0042): return c - ax * vx + ay * vy + az * up  # the viewer's right is the model's -X
    ring = [P(R * np.cos(t), R * np.sin(t), 0.0025) for t in np.linspace(0, 2 * np.pi, 17)]
    for a_, b_ in zip(ring, ring[1:]):
        v, n, f = capsule_mesh(a_, b_, 0.0024); parts.append((v, n, f, "letters", "chest"))
    h, sw = 0.03, 0.0032
    strokes = [
        ((-0.024, -h / 2), (-0.024, h / 2)), ((-0.022, -0.001), (-0.011, h / 2)), ((-0.02, 0.002), (-0.01, -h / 2)),   # K
        ((-0.007, -h / 2), (0.001, h / 2)), ((0.001, h / 2), (0.009, -h / 2)), ((-0.004, -0.004), (0.006, -0.004)),    # A
        ((0.012, h / 2), (0.025, h / 2)), ((0.025, h / 2), (0.012, -h / 2)), ((0.012, -h / 2), (0.025, -h / 2)),        # Z
    ]
    for a, b in strokes:
        v, n, f = capsule_mesh(P(*a), P(*b), sw); parts.append((v, n, f, "letters", "chest"))
    return parts

def ear_sd(p, sx, scale, forward=0.0):
    # A tapered capsule leaning slightly out, squashed to half its depth.
    m = V(sx, 1, 1)
    base, tip = EAR_BASE * m + V(0, 0, forward), EAR_TIP * m + V(0, 0, forward)
    q = p.copy()
    q[..., 2] = (p[..., 2] - base[2]) * 2.4 + base[2]
    return sd_capsule(q, base, tip, 0.075 * scale, 0.008 * scale) / 1.4

def tunic_sd(p):
    y = p[..., 1]
    top, bot = 0.6, 0.255
    t = np.clip((top - y) / (top - bot), 0.0, 1.0)
    rx, rz = 0.152 + 0.04 * t, 0.11 + 0.024 * t
    q = np.sqrt((p[..., 0] / rx) ** 2 + ((p[..., 2] - 0.002) / rz) ** 2)
    return np.maximum((q - 1.0) * np.minimum(rx, rz), np.maximum(y - top, bot - y))

def skirt_sd(p):
    y = p[..., 1]
    top, bot = 0.54, 0.35
    t = np.clip((top - y) / (top - bot), 0.0, 1.0)
    rx, rz = 0.102 + 0.062 * t, 0.082 + 0.052 * t
    q = np.sqrt((p[..., 0] / rx) ** 2 + ((p[..., 2] - 0.004) / rz) ** 2)
    return np.maximum((q - 1.0) * np.minimum(rx, rz), np.maximum(y - top, bot - y))

def head_shapes(girl):
    shape(lambda p: sd_capsule(p, V(0, 0.74, -0.004), V(0, 0.83, 0.0), 0.034, 0.032), "skin", "head")
    # Head: skull, cheeks/jaw, nose, ears
    shape(lambda p: sd_ellipsoid(p, V(0, 0.905, 0.0), V(0.097, 0.104, 0.1)), "skin", "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.86, 0.022), V(0.078, 0.058, 0.076)), "skin", "head")
    shape(lambda p: sd_ellipsoid(p, V(0, 0.878, 0.098), V(0.012, 0.016, 0.014)), "skin", "head")
    for sx in (1, -1):
        shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.097 * sx, 0.888, -0.004), V(0.014, 0.026, 0.02)))(sx), "skin", "head")
    if girl:
        shape(long_hair_sd, "hair", "head", -0.004)
        # a bow on top: two lobes and a knot
        for sx in (1, -1):
            shape((lambda sx: lambda p: sd_ellipsoid(p, V(0.042 * sx, 1.024, -0.012), V(0.037, 0.027, 0.015)))(sx), "bow", "head", -0.006, hard=True)
        shape(lambda p: sd_ellipsoid(p, V(0, 1.026, -0.01), V(0.016, 0.017, 0.017)), "bow", "head", -0.006, hard=True)
        return
    shape(hair_sd, "hair", "head", -0.004)
    # a few tufts on top
    for c, r in ((V(0.02, 1.005, 0.03), 0.028), (V(-0.035, 0.998, 0.0), 0.03), (V(0.01, 0.99, -0.06), 0.032), (V(0.06, 0.975, 0.045), 0.022)):
        shape((lambda c, r: lambda p: sd_ellipsoid(p, c, V(r * 1.2, r * 0.7, r * 1.2)))(c, r), "hair", "head", -0.004)

def long_hair_sd(p):
    # A bob down to the shoulders with bangs; the face stays open.
    cap = sd_ellipsoid(p, V(0, 0.915, -0.012), V(0.108, 0.106, 0.112))
    back = sd_ellipsoid(p, V(0, 0.855, -0.032), V(0.12, 0.13, 0.1))
    d = smin(cap, back, 0.02)
    line = np.interp(p[..., 2], [-0.2, 0.0, 0.04, 0.12], [0.745, 0.755, 0.93, 0.945])
    return np.maximum(d, line - p[..., 1])

def hair_sd(p):
    # A cap a little bigger than the skull, cut along a hairline (low at the back, bangs in front).
    d = sd_ellipsoid(p, V(0, 0.918, -0.01), V(0.107, 0.103, 0.11))
    z = p[..., 2]
    line = np.interp(z, [-0.12, -0.02, 0.05, 0.12], [0.83, 0.87, 0.925, 0.94])
    side = np.abs(p[..., 0])
    line = line - np.clip((side - 0.07) * 0.8, 0, 0.03) * (z < 0.06)  # sideburns
    return np.maximum(d, line - p[..., 1])

def limb_shapes(girl):
  for s, sx in (("R", 1), ("L", -1)):
    J = (lambda s: lambda n: JOINT[n + s])(s)
    # Legs: jeans (boy) or bare legs with socks under the skirt (girl); sneakers / flats
    legs = "skin" if girl else "pants"
    t0, t1, s0, s1 = (0.044, 0.037, 0.035, 0.03) if girl else (0.05, 0.041, 0.04, 0.034)
    shape((lambda J: lambda p: sd_capsule(p, J("upperleg") + V(0, 0.01, 0), J("lowerleg"), t0, t1))(J), legs, "leg" + s)
    shape((lambda J: lambda p: sd_capsule(p, J("lowerleg"), J("foot") + V(0, 0.01, 0), s0, s1))(J), legs, "leg" + s)
    shoe_r = V(0.039, 0.032, 0.077) if girl else V(0.043, 0.038, 0.083)
    def shoe(p, J=J, r=shoe_r):
        c = J("foot") * np.array([1, 0, 1]) + V(0, r[1] - 0.004, 0.03)
        d = sd_ellipsoid(p, c, r)
        return np.maximum(d, -p[..., 1])  # flat sole
    shape(shoe, "shoe", "leg" + s, -0.003)
    # Arms: short sleeve over skin, mitten hands with a thumb
    shape((lambda J: lambda p: sd_capsule(p, J("upperarm"), J("upperarm") + (J("lowerarm") - J("upperarm")) * 0.55, 0.039, 0.036))(J), "shirt", "arm" + s, -0.002)
    shape((lambda J: lambda p: sd_capsule(p, J("upperarm"), J("lowerarm"), 0.031, 0.027))(J), "skin", "arm" + s)
    shape((lambda J: lambda p: sd_capsule(p, J("lowerarm"), J("hand"), 0.026, 0.022))(J), "skin", "arm" + s)
    shape((lambda J: lambda p: sd_capsule(p, J("hand") + V(0, -0.012, 0), J("handEnd"), 0.024, 0.02))(J), "skin", "arm" + s)
    shape((lambda J, sx: lambda p: sd_capsule(p, J("hand") + V(0, -0.02, 0.012), J("hand") + V(-0.006 * sx, -0.05, 0.03), 0.01, 0.009))(J, sx), "skin", "arm" + s)

SMOOTH = 0.016

def body_sd(p):
    d = None
    hard = None
    for fn, mat, region, bias, is_hard in SHAPES:
        di = fn(p)
        if is_hard:
            hard = di if hard is None else np.minimum(hard, di)
        else:
            d = di if d is None else smin(d, di, SMOOTH)
    if hard is None: return d      # a piece with only blended shapes (a creator body or head)
    if d is None: return hard      # only hard ones (a creator hairstyle)
    return np.minimum(d, hard)

# ── Sculpt ─────────────────────────────────────────────────────────────────
def sculpt():
    lo, hi = V(-0.36, -0.01, -0.17), V(0.36, 1.5 if KIND == "dez" else 1.07, 0.2)
    if KIND == "rat":
        lo, hi = V(-0.5, -0.01, -0.62), V(0.58, 1.0, 0.46)
    if KIND in ("plague", "fish"):
        lo, hi = V(-0.4, -0.01, -0.3), V(0.4, 1.2, 0.34)
    n = np.ceil((hi - lo) / VOXEL).astype(int) + 1
    xs, ys, zs = [lo[i] + np.arange(n[i]) * VOXEL for i in range(3)]
    grid = np.stack(np.meshgrid(xs, ys, zs, indexing="ij"), axis=-1)
    field = np.empty(tuple(n))
    for i in range(n[0]):  # slab by slab keeps memory low
        field[i] = body_sd(grid[i])
    verts, faces, _, _ = measure.marching_cubes(field, 0.0, spacing=(VOXEL,) * 3)
    verts += lo
    # Normals from the field's gradient.
    e = VOXEL * 0.5
    nrm = np.stack([body_sd(verts + V(*(e if j == i else 0 for j in range(3)))) - body_sd(verts - V(*(e if j == i else 0 for j in range(3)))) for i in range(3)], axis=-1)
    nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True) + 1e-12
    # Outward winding: cross(b-a, c-a) along the normal.
    a, b, c = verts[faces[:, 0]], verts[faces[:, 1]], verts[faces[:, 2]]
    fn = np.cross(b - a, c - a)
    if np.mean(np.sum(fn * nrm[faces[:, 0]], axis=-1)) < 0:
        faces = faces[:, [0, 2, 1]]
    return verts, nrm, faces

def label(verts):
    """Which shape owns each vertex -> its material and body region."""
    ds = np.stack([fn(verts) + bias for fn, _, _, bias, _ in SHAPES], axis=-1)
    owner = np.argmin(ds, axis=-1)
    mats, regions = [], []
    for i, o in enumerate(owner):
        _, mat, region, _, _ = SHAPES[o]
        if mat == "shirt" and region.startswith("arm"):
            mat = "sleeve"
        elif mat == "shirt" and KIND == "dez":
            mat = "shirtA"
        elif mat == "shirt" and KIND == "plague":
            mat = "shirtA"
        elif mat == "shirt" and KIND in ("kaz", "fish"):
            x, y = abs(verts[i, 0]), verts[i, 1]
            neck = y > 0.775 and x < 0.085 and verts[i, 2] > -0.02
            mat = "skin" if (neck or (x > 0.118 and y > 0.735)) else "shirtA"
        elif mat == "shirt":
            mat = "shirtA" if int(math.floor((verts[i, 1] - 0.5) / STRIPE)) % 2 == 0 else "shirtB"
        if KIND == "kaz" and region in ("torso", "armR", "armL") and verts[i, 1] > 0.745 and 0.068 < abs(verts[i, 0]) < 0.116:
            mat = "shirtA"  # the tank top's straps, over the top of each shoulder, front to back
        if mat == "tail" and tail_ring(verts[i]):
            mat = "tailRing"
        if mat == "shoe" and verts[i, 1] < 0.012:
            mat = "sole"
        if mat == "skin" and region.startswith("leg") and verts[i, 1] < 0.13 and KIND not in ("dez", "rat"):
            mat = "sock"
        mats.append(MAT[mat])
        regions.append(region)
    return np.array(mats), regions

def seg_dist(p, a, b):
    ba = b - a
    t = np.clip(((p - a) @ ba) / (ba @ ba), 0.0, 1.0)
    return np.linalg.norm(p - (a + t[..., None] * ba), axis=-1)

def skin_weights(verts, regions):
    idx = np.zeros((len(verts), 4), dtype=int)
    wts = np.zeros((len(verts), 4))
    SIGMA = 0.012
    for region, bones in REGION_BONES.items():
        sel = np.array([r == region for r in regions])
        if not sel.any():
            continue
        p = verts[sel]
        d = np.stack([seg_dist(p, *SEG[b]) for b in bones], axis=-1)
        w = np.exp(-(d - d.min(axis=-1, keepdims=True)) / SIGMA)
        w[w < 0.02] = 0
        order = np.argsort(-w, axis=-1)[:, :4]
        top = np.take_along_axis(w, order, axis=-1)
        top /= top.sum(axis=-1, keepdims=True)
        idx[sel] = np.array([BI[b] for b in bones])[order] if len(bones) >= 4 else np.pad(np.array([BI[b] for b in bones])[order], ((0, 0), (0, 4 - order.shape[1])))
        wts[sel] = top if top.shape[1] == 4 else np.pad(top, ((0, 0), (0, 4 - top.shape[1])))
    # The skirt follows the hips, and each side partly follows its leg toward the hem.
    for i in [k for k, r in enumerate(regions) if r == "skirt"]:
        x, y = verts[i, 0], verts[i, 1]
        a = 0.5 * np.clip((0.5 - y) / 0.14, 0, 1) * np.clip(abs(x) / 0.06, 0, 1)
        idx[i] = [BI["hips"], BI["upperleg" + ("R" if x > 0 else "L")], 0, 0]
        wts[i] = [1 - a, a, 0, 0]
    return idx, wts

# ── Face details (separate little meshes on the head bone) ────────────────
def ellipsoid_mesh(center, radii, rot=np.eye(3), seg=12, rings=8):
    vs, ns, fs = [], [], []
    for i in range(rings + 1):
        th = math.pi * i / rings
        for j in range(seg):
            ph = 2 * math.pi * j / seg
            unit = np.array([math.sin(th) * math.cos(ph), math.cos(th), math.sin(th) * math.sin(ph)])
            vs.append(center + rot @ (unit * radii))
            n = rot @ (unit / radii)
            ns.append(n / np.linalg.norm(n))
    for i in range(rings):
        for j in range(seg):
            a, b = i * seg + j, i * seg + (j + 1) % seg
            c, d = a + seg, b + seg
            fs += [[a, b, c], [b, d, c]]
    vs, ns, fs = np.array(vs), np.array(ns), np.array(fs)
    fn = np.cross(vs[fs[:, 1]] - vs[fs[:, 0]], vs[fs[:, 2]] - vs[fs[:, 0]])
    if np.mean(np.sum(fn * ns[fs[:, 0]], axis=-1)) < 0:
        fs = fs[:, [0, 2, 1]]
    return vs, ns, fs

FACE_Z0 = 0.2

def face_z(x, y):
    """Where the face surface is, marching in from the front."""
    z = FACE_Z0
    for _ in range(600):
        d = float(body_sd(np.array([[x, y, z]]))[0])
        if d < 1e-4:
            break
        z -= max(d * 0.5, 1e-4)  # half steps: squashed shapes (Dez's ears) overstate their distance
        if z < -0.4:
            raise ValueError("face_z missed the surface at %.3f, %.3f" % (x, y))
    return z

def rot_facing(nrm):
    """Rotation whose local +Z points along nrm (for flattened discs on the face)."""
    z = nrm / np.linalg.norm(nrm)
    x = np.cross(V(0, 1, 0), z); x /= np.linalg.norm(x)
    y = np.cross(z, x)
    return np.stack([x, y, z], axis=-1)

def capsule_mesh(a, b, r, surface_offset=0.0):
    """A thin stroke between two points (for letters, lines on the face)."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    d = b - a
    L = max(np.linalg.norm(d), 1e-6)
    y = d / L
    z = V(0, 0, 1)
    x = np.cross(y, z); x /= np.linalg.norm(x) + 1e-12
    z = np.cross(x, y)
    rot = np.stack([x, y, z], axis=-1)
    return ellipsoid_mesh((a + b) / 2, V(r, L / 2 + r, r * 0.6), rot, 8, 6)

def on_surface(x, y, lift=0.002):
    return V(x, y, face_z(x, y) + lift)

def dez_details():
    parts = []
    # Sleepy eyes: a heavy lid line with the pupil peeking under it, eyebrows raised above.
    for sx in (1, -1):
        ex = 0.075 * sx
        lid = [on_surface(ex + dx * sx, 0.875 + dy, 0.0) for dx, dy in ((-0.05, -0.004), (-0.015, 0.003), (0.02, 0.003), (0.048, -0.002))]
        for a, b in zip(lid, lid[1:]):
            v, n, f = capsule_mesh(a, b, 0.0062); parts.append((v, n, f, "eye", "head"))
        v, n, f = ellipsoid_mesh(on_surface(ex + 0.006 * sx, 0.862, -0.002), V(0.018, 0.011, 0.007), rot_facing(V(ex * 2.0, 0, 1)))
        parts.append((v, n, f, "eye", "head"))
        brow = [on_surface(ex + dx * sx, 0.945 + dy, 0.0) for dx, dy in ((-0.05, -0.012), (-0.015, 0.006), (0.03, 0.008), (0.055, -0.002))]
        for a, b in zip(brow, brow[1:]):
            v, n, f = capsule_mesh(a, b, 0.0036); parts.append((v, n, f, "eye", "head"))
    # The blue inside of each ear: a smooth pointed oval lying on the ear's front.
    for sx in (1, -1):
        m = V(sx, 1, 1)
        base, tip = EAR_BASE * m, EAR_TIP * m
        # lie along the ear's own front surface (the ear leans back, so a flat oval got buried at the base)
        lo = base + (tip - base) * 0.1; lo[2] = face_z(lo[0], lo[1]) + 0.003
        hi = base + (tip - base) * 0.8; hi[2] = face_z(hi[0], hi[1]) + 0.003
        c = (lo + hi) / 2
        up = (hi - lo) / np.linalg.norm(hi - lo)
        side = np.cross(up, V(0, 0, 1)); side /= np.linalg.norm(side)
        fwd = np.cross(side, up)
        v, n, f = ellipsoid_mesh(c, V(0.036, np.linalg.norm(hi - lo) / 2, 0.005), np.stack([side, up, fwd], axis=-1), 14, 10)
        parts.append((v, n, f, "earInner", "head"))
    # A tiny nose, a "w" smile, and the blue tongue sticking out.
    v, n, f = ellipsoid_mesh(on_surface(0, 0.825, -0.004), V(0.02, 0.012, 0.01), rot_facing(V(0, 0, 1)))
    parts.append((v, n, f, "nose", "head"))
    w = [on_surface(x, y, 0.0) for x, y in ((-0.075, 0.8), (-0.045, 0.776), (-0.015, 0.785), (0.0, 0.795), (0.015, 0.785), (0.045, 0.776), (0.075, 0.8))]
    for a, b in zip(w, w[1:]):
        v, n, f = capsule_mesh(a, b, 0.0038); parts.append((v, n, f, "eye", "head"))
    v, n, f = ellipsoid_mesh(on_surface(0, 0.752, 0.006), V(0.03, 0.043, 0.014), rot_facing(V(0, -0.35, 1)))
    parts.append((v, n, f, "tongue", "head"))
    # "DEZ" across the chest, readable from the front (the viewer's left is the model's +X).
    H, top, sw = 0.092, 0.665, 0.012
    def P(vx, vy): return on_surface(-vx, top - vy, 0.003)
    strokes = []
    x0 = -0.118  # D
    strokes += [((x0, 0), (x0, H))]
    arc = [(x0 + 0.05 * np.sin(a * np.pi), H / 2 - H / 2 * np.cos(a * np.pi)) for a in np.linspace(0, 1, 8)]
    strokes += list(zip(arc, arc[1:]))
    x1 = -0.03  # E
    strokes += [((x1, 0), (x1, H)), ((x1, 0), (x1 + 0.052, 0)), ((x1, H / 2), (x1 + 0.042, H / 2)), ((x1, H), (x1 + 0.052, H))]
    x2 = 0.05  # Z
    strokes += [((x2, 0), (x2 + 0.064, 0)), ((x2 + 0.064, 0), (x2, H)), ((x2, H), (x2 + 0.064, H))]
    for a, b in strokes:
        v, n, f = capsule_mesh(P(*a), P(*b), sw); parts.append((v, n, f, "letters", "chest"))
    return parts

def face_details(girl):
    if KIND == "rat":
        return rat_details()
    if KIND == "dez":
        return dez_details()
    if KIND == "kaz":
        return kaz_details()
    if KIND == "plague":
        return plague_details()
    if KIND == "fish":
        return fish_details()
    parts = []  # (verts, normals, faces, material)
    for sx in (1, -1):
        ex, ey = 0.036 * sx, 0.898
        z = face_z(ex, ey)
        n = V(ex * 2.2, 0.0, 1.0)
        r = rot_facing(n)
        v, nn, f = ellipsoid_mesh(V(ex, ey, z - 0.002), V(0.0125, 0.017, 0.006), r)
        parts.append((v, nn, f, "eye"))
        v, nn, f = ellipsoid_mesh(V(ex + 0.004 * sx, ey + 0.006, z + 0.0035), V(0.0035, 0.0035, 0.002), r, 8, 6)
        parts.append((v, nn, f, "glint"))
        if girl:  # eyelashes at the outer corners
            for k, (dx, dy, ang) in enumerate(((0.012, 0.013, 0.5), (0.016, 0.006, 0.9))):
                c, sn = math.cos(ang * sx), math.sin(ang * sx)
                v, nn, f = ellipsoid_mesh(V(ex + dx * sx, ey + dy, z - 0.0015), V(0.0075, 0.0022, 0.003), r @ np.array([[c, -sn, 0], [sn, c, 0], [0, 0, 1]]), 8, 6)
                parts.append((v, nn, f, "eye"))
        # eyebrow
        bz = face_z(ex, ey + 0.032)
        v, nn, f = ellipsoid_mesh(V(ex, ey + 0.031, bz - 0.001), V(0.017, 0.0042, 0.004), r @ np.array([[math.cos(0.12 * sx), -math.sin(0.12 * sx), 0], [math.sin(0.12 * sx), math.cos(0.12 * sx), 0], [0, 0, 1]]))
        parts.append((v, nn, f, "hair"))
    mz = face_z(0.0, 0.848)
    v, nn, f = ellipsoid_mesh(V(0, 0.848, mz - 0.0015), V(0.017, 0.0045, 0.004), rot_facing(V(0, 0, 1)))
    parts.append((v, nn, f, "mouth"))
    return parts

# ── Animation ──────────────────────────────────────────────────────────────
def q_axis(axis, deg):
    a = math.radians(deg) * 0.5
    axis = np.asarray(axis, float)
    return np.array([*(axis * math.sin(a)), math.cos(a)])  # x, y, z, w

def q_mul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return np.array([aw * bx + ax * bw + ay * bz - az * by,
                     aw * by - ax * bz + ay * bw + az * bx,
                     aw * bz + ax * by - ay * bx + az * bw,
                     aw * bw - ax * bx - ay * by - az * bz])

def euler(x=0.0, y=0.0, z=0.0):
    return q_mul(q_axis((0, 1, 0), y), q_mul(q_axis((1, 0, 0), x), q_axis((0, 0, 1), z)))

ARM_IN = 21.0  # rest pose is an A-pose; animations bring the arms down to the sides

def pose_idle(t):
    s = math.sin(2 * math.pi * t)
    c2 = math.sin(4 * math.pi * t)
    P = {
        "spine": euler(x=1.0 + 0.8 * s), "chest": euler(x=0.8 * c2),
        "head": euler(x=-1.0, y=5.0 * math.sin(2 * math.pi * t + 0.8)),
        "upperarmR": euler(x=2 * s, z=-ARM_IN + 1.2 * c2), "upperarmL": euler(x=-2 * s, z=ARM_IN - 1.2 * c2),
        "lowerarmR": euler(x=-10), "lowerarmL": euler(x=-10),
        "upperlegR": euler(x=0.5 * s), "upperlegL": euler(x=-0.5 * s),
    }
    return P, V(0, 0.002 * c2, 0)

def pose_gait(t, legs, knee, arms, elbow, lean, bob, twist):
    P = {}
    for s, ph in (("R", 0.0), ("L", 0.5)):
        w = 2 * math.pi * (t + ph)
        up = -legs * math.sin(w)
        kn = knee * 0.15 + knee * max(0.0, math.cos(w)) ** 1.4
        P["upperleg" + s] = euler(x=up)
        P["lowerleg" + s] = euler(x=kn)
        P["foot" + s] = euler(x=-(up + kn) * 0.55)
        side = 1 if s == "R" else -1
        P["upperarm" + s] = euler(x=arms * math.sin(w), z=-ARM_IN * side)
        P["lowerarm" + s] = euler(x=-elbow - elbow * 0.3 * max(0.0, -math.sin(w)))
    w = 2 * math.pi * t
    P["hips"] = euler(y=twist * math.sin(w))
    P["spine"] = euler(x=lean)
    P["chest"] = euler(y=-twist * 1.6 * math.sin(w))
    P["head"] = euler(x=-lean * 0.7, y=twist * 0.5 * math.sin(w))
    return P, V(0, -bob * math.cos(4 * math.pi * t) - bob, 0)

def pose_walk(t):
    return pose_gait(t, legs=26, knee=42, arms=20, elbow=14, lean=3, bob=0.011, twist=6)

def pose_run(t):
    return pose_gait(t, legs=40, knee=80, arms=36, elbow=70, lean=12, bob=0.022, twist=9)

def pose_scared(t):
    # Mama got them: arms up over the face, turning away, knees buckling, trembling.
    tr = math.sin(2 * math.pi * 6 * t)
    P = {
        "spine": euler(x=-10), "chest": euler(x=-6, y=1.5 * tr),
        "head": euler(x=12, y=18 + 2 * tr),
        "upperarmR": euler(x=-72, y=-38, z=-ARM_IN), "upperarmL": euler(x=-72, y=38, z=ARM_IN),
        "lowerarmR": euler(x=-88 + 3 * tr), "lowerarmL": euler(x=-88 - 3 * tr),
    }
    for s in ("R", "L"):
        P["upperleg" + s] = euler(x=-18)
        P["lowerleg" + s] = euler(x=32)
        P["foot" + s] = euler(x=-14)
    return P, V(0, -0.03, 0)

CLIPS = [("idle", 3.0, pose_idle), ("walk", 0.9, pose_walk), ("run", 0.56, pose_run), ("scared", 1.0, pose_scared)]
FRAMES = 30

def bake(fn):
    rots = np.zeros((FRAMES, len(BONES), 4)); rots[..., 3] = 1
    hips = np.zeros((FRAMES, 3))
    for f in range(FRAMES):
        P, off = fn(f / FRAMES)
        for name, q in P.items():
            rots[f, BI[name]] = q / np.linalg.norm(q)
        hips[f] = off
    return rots, hips

# ── Output ─────────────────────────────────────────────────────────────────
def local_pos(name):
    parent = dict(BONES)[name]
    return JOINT[name] - (JOINT[parent] if parent else 0)

def write_bin(path, verts, nrms, bidx, bw, subs, clips):
    out = bytearray(b"KID2")
    w = lambda fmt, *v: out.extend(struct.pack("<" + fmt, *v))
    def wstr(s):
        b = s.encode("utf-8"); w("i", len(b)); out.extend(b)
    w("i", len(BONES))
    for name, parent in BONES:
        wstr(name); w("i", BI[parent] if parent else -1); w("3f", *(local_pos(name) * HEIGHT))
    w("i", len(verts))
    for i in range(len(verts)):
        w("3f", *(verts[i] * HEIGHT)); w("3f", *nrms[i])
        for k in range(4):
            w("if", int(bidx[i, k]), float(bw[i, k]))
    w("i", len(subs))
    for m, tris in subs:
        mname, col, gloss = MATERIALS[m]
        wstr(mname); w("4f", *col, 1.0); w("f", gloss); w("i", len(tris) * 3)
        out.extend(np.asarray(tris, dtype="<i4").tobytes())
    w("i", len(clips))
    for name, period, rots, hips in clips:
        wstr(name); w("i", FRAMES); w("f", period)
        for f in range(FRAMES):
            w("3f", *(hips[f] * HEIGHT))
            for b in range(len(BONES)):
                w("4f", *rots[f, b])
    with open(path, "wb") as fh:
        fh.write(out)

def write_glb(path, verts, nrms, bidx, bw, subs, clips):
    """Same model mirrored into glTF's right-handed space."""
    M = np.array([-1.0, 1.0, 1.0])
    buf = bytearray()
    views, accessors = [], []
    def add(arr, ctype, typ, target=None, minmax=False):
        arr = np.ascontiguousarray(arr)
        while len(buf) % 4: buf.append(0)
        views.append({"buffer": 0, "byteOffset": len(buf), "byteLength": arr.nbytes, **({"target": target} if target else {})})
        buf.extend(arr.tobytes())
        acc = {"bufferView": len(views) - 1, "componentType": ctype, "count": int(arr.shape[0]), "type": typ}
        if minmax:
            acc["min"] = arr.min(axis=0).tolist(); acc["max"] = arr.max(axis=0).tolist()
        accessors.append(acc)
        return len(accessors) - 1
    pos = add((verts * HEIGHT * M).astype("<f4"), 5126, "VEC3", 34962, True)
    nor = add((nrms * M).astype("<f4"), 5126, "VEC3", 34962)
    jnt = add(bidx.astype("<u2"), 5123, "VEC4", 34962)
    wgt = add(bw.astype("<f4"), 5126, "VEC4", 34962)
    prims, mats = [], []
    for m, tris in subs:
        t = np.asarray(tris)[:, [0, 2, 1]].astype("<u4").reshape(-1)  # mirroring flips winding
        ind = add(t, 5125, "SCALAR", 34963)
        _, col, gloss = MATERIALS[m]
        mats.append({"name": MATERIALS[m][0], "pbrMetallicRoughness": {"baseColorFactor": [*col, 1.0], "metallicFactor": 0.0, "roughnessFactor": 1.0 - gloss * 0.8}})
        prims.append({"attributes": {"POSITION": pos, "NORMAL": nor, "JOINTS_0": jnt, "WEIGHTS_0": wgt}, "indices": ind, "material": len(mats) - 1})
    nodes = []
    for name, parent in BONES:
        nodes.append({"name": name, "translation": (local_pos(name) * HEIGHT * M).tolist()})
    for i, (name, parent) in enumerate(BONES):
        kids = [j for j, (_, p) in enumerate(BONES) if p == name]
        if kids: nodes[i]["children"] = kids
    ibm = np.zeros((len(BONES), 16), dtype="<f4")
    for i, (name, _) in enumerate(BONES):
        mtx = np.eye(4); mtx[:3, 3] = -JOINT[name] * HEIGHT * M
        ibm[i] = mtx.T.reshape(-1)  # column-major
    ibm_acc = add(ibm, 5126, "MAT4")
    mesh_node = len(nodes)
    nodes.append({"name": "KidMesh", "mesh": 0, "skin": 0})
    nodes.append({"name": "Kid", "children": [BI["root"], mesh_node]})
    anims = []
    for name, period, rots, hips in clips:
        times = add((np.arange(FRAMES + 1) * period / FRAMES).astype("<f4"), 5126, "SCALAR")
        accessors[times]["min"] = [0.0]; accessors[times]["max"] = [float(period)]
        samplers, channels = [], []
        for b in range(len(BONES)):
            r = np.concatenate([rots[:, b], rots[:1, b]])  # loop back to frame 0
            r = r * np.array([1, -1, -1, 1])
            samplers.append({"input": times, "output": add(r.astype("<f4"), 5126, "VEC4"), "interpolation": "LINEAR"})
            channels.append({"sampler": len(samplers) - 1, "target": {"node": b, "path": "rotation"}})
        hp = np.concatenate([hips, hips[:1]]) * HEIGHT * M + local_pos("hips") * HEIGHT * M
        samplers.append({"input": times, "output": add(hp.astype("<f4"), 5126, "VEC3"), "interpolation": "LINEAR"})
        channels.append({"sampler": len(samplers) - 1, "target": {"node": BI["hips"], "path": "translation"}})
        anims.append({"name": name, "samplers": samplers, "channels": channels})
    gltf = {
        "asset": {"version": "2.0", "generator": "KaleidoscopeEndless kid_model.py"},
        "scene": 0, "scenes": [{"nodes": [len(nodes) - 1]}], "nodes": nodes,
        "meshes": [{"primitives": prims}], "materials": mats,
        "skins": [{"joints": list(range(len(BONES))), "inverseBindMatrices": ibm_acc, "skeleton": BI["root"]}],
        "animations": anims, "accessors": accessors, "bufferViews": views,
        "buffers": [{"byteLength": len(buf)}],
    }
    js = json.dumps(gltf, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    while len(buf) % 4: buf.append(0)
    with open(path, "wb") as fh:
        fh.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(buf)))
        fh.write(struct.pack("<II", len(js), 0x4E4F534A)); fh.write(js)
        fh.write(struct.pack("<II", len(buf), 0x004E4942)); fh.write(buf)

def main():
    here = os.path.dirname(os.path.abspath(__file__))
    preview_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "assets")
    kinds = sys.argv[2:] or ["boy", "girl", "dez", "kaz", "rat"]
    for kind in kinds:
        build(kind, here, preview_dir)

def build(kind, here, preview_dir):
    global HEIGHT
    HEIGHT = HEIGHTS[kind]
    make_skeleton(adult=kind in ("kaz", "plague", "fish"), rat=kind == "rat")
    define_shapes(kind)
    MATERIALS[MAT["hair"]] = ("hair", HAIR.get(kind, HAIR["boy"]), 0.30)
    rat = kind == "rat"
    MATERIALS[MAT["skin"]] = ("skin", (0.87, 0.69, 0.23) if rat else (0.93, 0.96, 1.0) if kind == "dez" else (0.88, 0.7, 0.52) if kind == "kaz" else (0.96, 0.76, 0.62), 0.15)
    red = (1.0, 0.84, 0.12) if rat else (0.6, 0.05, 0.18) if kind == "dez" else (0.95, 0.42, 0.1) if kind == "kaz" else (0.74, 0.13, 0.14)  # preview's default shirt (the rat: his crown)
    MATERIALS[MAT["shirtA"]] = ("shirtA", red, 0.6 if rat else 0.10)
    MATERIALS[MAT["shoe"]] = ("shoe", (0.14, 0.14, 0.16) if kind == "kaz" else (0.92, 0.92, 0.90), 0.25)
    MATERIALS[MAT["pants"]] = ("pants", (0.33, 0.33, 0.36) if rat else (0.17, 0.2, 0.3) if kind == "kaz" else (0.22, 0.31, 0.52), 0.10)  # the rat: his controller
    MATERIALS[MAT["earInner"]] = ("earInner", (0.94, 0.64, 0.62) if rat else (0.05, 0.47, 0.78), 0.20)
    MATERIALS[MAT["tongue"]] = ("tongue", (0.4, 0.86, 0.96) if rat else (0.08, 0.45, 0.72), 0.8 if rat else 0.40)  # the rat: drool
    MATERIALS[MAT["nose"]] = ("nose", (0.04, 0.04, 0.05) if rat else (0.72, 0.86, 0.96), 0.8 if rat else 0.20)
    MATERIALS[MAT["sleeve"]] = ("sleeve", red, 0.10)
    if kind == "plague":   # the mask (the skin slot), a black coat, dark leather
        MATERIALS[MAT["skin"]] = ("skin", (0.86, 0.83, 0.74), 0.25)
        MATERIALS[MAT["shirtA"]] = ("shirtA", (0.11, 0.11, 0.13), 0.12)
        MATERIALS[MAT["sleeve"]] = ("sleeve", (0.11, 0.11, 0.13), 0.12)
        MATERIALS[MAT["shirtB"]] = ("shirtB", (0.17, 0.12, 0.09), 0.25)
        MATERIALS[MAT["pants"]] = ("pants", (0.09, 0.09, 0.1), 0.10)
        MATERIALS[MAT["shoe"]] = ("shoe", (0.12, 0.09, 0.07), 0.30)
    if kind == "fish":     # tanned, a salmon tank top, navy shorts, white trainers
        MATERIALS[MAT["skin"]] = ("skin", (0.86, 0.63, 0.46), 0.15)
        MATERIALS[MAT["shirtA"]] = ("shirtA", (0.98, 0.5, 0.45), 0.35)     # the belt
        MATERIALS[MAT["sleeve"]] = ("sleeve", (0.95, 0.78, 0.25), 0.6)       # the buckle, and the wristbands
        MATERIALS[MAT["pants"]] = ("pants", (0.12, 0.2, 0.4), 0.10)
        MATERIALS[MAT["shoe"]] = ("shoe", (0.92, 0.92, 0.9), 0.25)
    verts, nrms, faces = sculpt()
    mats, regions = label(verts)
    bidx, bw = skin_weights(verts, regions)

    # Face details join the same mesh, fully on the head bone.
    extra_subs = []
    for part in face_details(kind == "girl"):
        v, n, f, m = part[:4]
        bone = part[4] if len(part) > 4 else "head"
        base = len(verts)
        verts = np.vstack([verts, v]); nrms = np.vstack([nrms, n])
        bidx = np.vstack([bidx, np.tile([BI[bone], 0, 0, 0], (len(v), 1))])
        bw = np.vstack([bw, np.tile([1.0, 0, 0, 0], (len(v), 1))])
        extra_subs.append((MAT[m], f + base))

    # A triangle takes the material most of its corners have.
    fm = mats[faces]
    tri_mat = np.where(fm[:, 1] == fm[:, 2], fm[:, 1], fm[:, 0])
    subs = []
    for m in range(len(MATERIALS)):
        tris = [faces[tri_mat == m]] + [f for mm, f in extra_subs if mm == m]
        tris = np.vstack(tris) if any(len(t) for t in tris) else np.zeros((0, 3), int)
        if len(tris): subs.append((m, tris))

    clips = [(name, period, *bake(fn)) for name, period, fn in CLIPS]
    assert len(verts) < 65000, len(verts)
    write_bin(os.path.join(here, "..", "assets", "kid_" + kind + ".bin"), verts, nrms, bidx, bw, subs, clips)
    write_glb(os.path.join(preview_dir, "kid_" + kind + ".glb"), verts, nrms, bidx, bw, subs, clips)
    print(kind, "verts", len(verts), "tris", sum(len(t) for _, t in subs), "materials", [MATERIALS[m][0] for m, _ in subs])

if __name__ == "__main__":
    main()
