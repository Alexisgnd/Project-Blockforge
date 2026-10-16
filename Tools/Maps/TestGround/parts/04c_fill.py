
# ==== 04c : fill the map — structured lane dressing + props placed on a free-space grid ====
ZF = "16_Props"
random.seed(7)
JERSEY = [(-0.35, 0), (0.35, 0), (0.15, 0.35), (0.12, 1.0), (-0.12, 1.0), (-0.15, 0.35)]
SANDBAG = [(-0.55, 0), (0.55, 0), (0.25, 0.9), (-0.25, 0.9)]

def light_pole(nm, x, y, a):
    box(nm + "_Post", (x, y, 5.0), (0.3, 0.3, 10.0), "Dark", ZF)
    box(nm + "_Arm", (x + 1.1 * math.cos(a), y + 1.1 * math.sin(a), 9.9), (2.5, 0.2, 0.2), "Dark", ZF, rot_z=a)
    box(nm + "_Lamp", (x + 2.1 * math.cos(a), y + 2.1 * math.sin(a), 9.7), (0.8, 0.5, 0.25), "Marking", ZF, rot_z=a)

# -- speed lanes N and E : jersey barriers every 12 m + light poles every 40 m --
for s_ in (-1, 1):
    side = "L" if s_ < 0 else "R"
    for k, t in enumerate(range(50, 290, 12)):
        extrude_profile("LaneN_Barrier_%s%02d" % (side, k), JERSEY, 3.0, (s_ * 13.5, t, 0), math.pi / 2, "Concrete", ZF)
        extrude_profile("LaneE_Barrier_%s%02d" % (side, k), JERSEY, 3.0, (t, s_ * 13.5, 0), 0.0, "Concrete", ZF)
    for k, t in enumerate(range(64, 290, 40)):
        light_pole("LaneN_Pole_%s%d" % (side, k), s_ * 17.0, t, math.pi if s_ > 0 else 0.0)
        light_pole("LaneE_Pole_%s%d" % (side, k), t, s_ * 17.0, -math.pi / 2 if s_ > 0 else math.pi / 2)

# -- occupancy grid (2 m) : existing geometry + reserved areas + ramparts --
GS = 2.0; NG = int(600 / GS)
occ = np.zeros((NG, NG), dtype=bool)
def _c(v): return int((v + 300.0) // GS)
def mark(x0, x1, y0, y1):
    i0, i1 = max(_c(x0), 0), min(_c(x1), NG - 1); j0, j1 = max(_c(y0), 0), min(_c(y1), NG - 1)
    if i0 <= i1 and j0 <= j1: occ[j0:j1 + 1, i0:i1 + 1] = True
def free(x0, x1, y0, y1):
    if x0 < -298 or x1 > 298 or y0 < -298 or y1 > 298: return False
    return not occ[_c(y0):_c(y1) + 1, _c(x0):_c(x1) + 1].any()

bpy.context.view_layer.update()
for ob in ROOT.all_objects:
    if ob.type != 'MESH' or ob.name in ("Ground", "Ramparts", "RoughTerrain") or ob.name.startswith("Bounds_"): continue
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    xs = [p.x for p in bb]; ys = [p.y for p in bb]; zs = [p.z for p in bb]
    if max(zs) - min(zs) < 0.05 and "Label" not in ob.name: continue   # flat paint lines
    if min(zs) > 4.0: continue                                         # floating (rings, drones, bridges)
    mark(min(xs) - 2, max(xs) + 2, min(ys) - 2, max(ys) + 2)
for r in ((-44, 44, -44, 44), (-15, 15, 40, 300), (40, 300, -15, 15), (-286, -46, 38, 292),
          (86, 284, -90, -60), (-284, -46, -284, -46)):
    mark(*r)
ram = np.zeros_like(occ)
for j in range(NG):
    for i in range(NG):
        if rampart_h(-300 + (i + .5) * GS, -300 + (j + .5) * GS) > -0.29: ram[j, i] = True
for _ in range(4):
    d = ram.copy(); d[1:, :] |= ram[:-1, :]; d[:-1, :] |= ram[1:, :]; d[:, 1:] |= ram[:, :-1]; d[:, :-1] |= ram[:, 1:]; ram = d
occ |= ram

def rot_off(x, y, a, dx, dy): return x + dx * math.cos(a) - dy * math.sin(a), y + dx * math.sin(a) + dy * math.cos(a)

# -- prop builders : (radius used for spacing, builder) --
def p_crates(nm, x, y, a):
    for k, (dx, dy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))[:random.randint(2, 4)]):
        px, py = rot_off(x, y, a, dx, dy); box("%s_%d" % (nm, k), (px, py, 0.9), (1.8, 1.8, 1.8), "Wood", ZF, rot_z=a)
    if random.random() < 0.6:
        px, py = rot_off(x, y, a, -1, -1); box(nm + "_Top", (px, py, 2.7), (1.8, 1.8, 1.8), "Wood", ZF, rot_z=a + 0.3)
def p_barrels(nm, x, y, a):
    for k in range(random.randint(3, 7)):
        px, py = rot_off(x, y, a, random.uniform(-2, 2), random.uniform(-2, 2))
        cyl("%s_%d" % (nm, k), (px, py, 0.6), 0.45, 1.2, random.choice(("RedPaint", "Steel", "Yard")), ZF, seg=10)
def p_jersey(nm, x, y, a):
    for k in range(random.randint(2, 4)):
        px, py = rot_off(x, y, a, (k - 1.5) * 3.05, 0); extrude_profile("%s_%d" % (nm, k), JERSEY, 3.0, (px, py, 0), a, "Concrete", ZF)
def p_blocks(nm, x, y, a):
    for k in range(random.randint(2, 4)):
        px, py = rot_off(x, y, a, random.uniform(-3, 3), random.uniform(-3, 3)); s = random.choice((1.5, 2.0, 2.5))
        box("%s_%d" % (nm, k), (px, py, s / 2), (s, s, s), "Concrete", ZF, rot_z=random.uniform(0, math.pi))
def p_pipe(nm, x, y, a):
    cyl(nm, (x, y, 1.0), 1.0, 8.0, "Steel", ZF, axis='x', seg=14, rot_z=a)
def p_sandbags(nm, x, y, a):
    for k in range(3):
        b = a + (k - 1) * 0.45; px, py = rot_off(x, y, b, 0, 3.0)
        extrude_profile("%s_%d" % (nm, k), SANDBAG, 3.0, (px, py, 0), b, "Rough", ZF)
def p_shed(nm, x, y, a):
    m = random.choice(("Yard", "Steel", "Concrete"))
    box(nm, (x, y, 1.75), (7, 5, 3.5), m, ZF, rot_z=a); box(nm + "_Roof", (x, y, 3.65), (7.6, 5.6, 0.3), "Dark", ZF, rot_z=a)
def p_container(nm, x, y, a):
    box(nm, (x, y, 1.3), (12, 2.5, 2.6), random.choice(("Yard", "Steel")), ZF, rot_z=a)
    if random.random() < 0.35: box(nm + "_Top", (x, y, 3.9), (12, 2.5, 2.6), random.choice(("Yard", "Steel")), ZF, rot_z=a + random.uniform(-0.1, 0.1))
def p_outpost(nm, x, y, a):
    box(nm + "_Deck", (x, y, 2.8), (6, 6, 0.4), "Yard", ZF, rot_z=a)
    for k, (dx, dy) in enumerate(((-2.7, -2.7), (2.7, -2.7), (-2.7, 2.7), (2.7, 2.7))):
        px, py = rot_off(x, y, a, dx, dy); box("%s_Leg%d" % (nm, k), (px, py, 1.3), (0.4, 0.4, 2.6), "Dark", ZF, rot_z=a)
    ramp_local(nm + "_Ramp", (x, y, 0), a, -3 - 3 / math.tan(math.radians(15)), -3, 2.0, 3.0, "Concrete", ZF)
    target_at(nm + "_Target", x, y, 3.0, ZF, face_to=(0, 0), size=(2, 2, 2))
def p_pole(nm, x, y, a): light_pole(nm, x, y, a)
def p_target(nm, x, y, a):
    target_at(nm, x, y, 0, ZF, face_to=(0, 0), size=random.choice(((2, 2, 2), (4, 3, 3), (5, 4, 4))))
def p_car(nm, x, y, a):
    m = random.choice(("Steel", "Yard", "RedPaint"))
    box(nm, (x, y, 0.6), (4.6, 2.0, 1.2), m, ZF, rot_z=a)
    px, py = rot_off(x, y, a, -0.3, 0); box(nm + "_Cabin", (px, py, 1.6), (2.4, 1.8, 0.8), m, ZF, rot_z=a)
def p_kicker(nm, x, y, a):
    ramp_local(nm, (x, y, 0), a, -2.5, 2.5, 2.0, 1.2, "Ramps", ZF)
def p_popup(nm, x, y, a):
    moving_popup("MovePopup_" + nm, x, y, 0, ZF, face_to=(0, 0))

PROPS = {"crates": (3.0, p_crates), "barrels": (3.0, p_barrels), "jersey": (6.5, p_jersey), "blocks": (4.5, p_blocks),
         "pipe": (4.5, p_pipe), "sandbags": (4.5, p_sandbags), "shed": (4.5, p_shed), "container": (6.5, p_container),
         "outpost": (10.0, p_outpost), "pole": (1.5, p_pole), "target": (2.0, p_target), "car": (3.0, p_car),
         "kicker": (3.5, p_kicker), "popup": (2.0, p_popup)}
REGIONS = [
    ("Yard",  -40, 40, -292, -44, 90, {"crates": 3, "barrels": 3, "jersey": 3, "blocks": 2, "sandbags": 4, "shed": 1, "container": 2, "target": 3, "car": 1, "popup": 1}),
    ("Air",    40, 292, -292, -40, 110, {"crates": 2, "barrels": 2, "blocks": 2, "pipe": 1, "shed": 2, "container": 2, "outpost": 2, "pole": 1, "target": 2, "kicker": 1, "car": 1, "popup": 1}),
    ("NE",     40, 292, 40, 292, 70, {"blocks": 3, "pipe": 2, "jersey": 2, "kicker": 2, "crates": 1, "target": 2, "pole": 1, "barrels": 1}),
    ("West", -292, -44, -44, 44, 60, {"crates": 2, "barrels": 2, "jersey": 2, "car": 3, "blocks": 1, "target": 1, "pole": 1, "sandbags": 1}),
    ("CorrN",  -44, 44, 44, 292, 35, {"crates": 2, "barrels": 2, "blocks": 2, "car": 2, "target": 1, "sandbags": 1}),
    ("CorrE",   44, 292, -44, 44, 35, {"crates": 2, "barrels": 2, "blocks": 2, "car": 2, "target": 1, "sandbags": 1}),
]
GAP = 4.0          # minimum free driving space kept between props
COUNT = {}
for reg, x0, x1, y0, y1, n, w in REGIONS:
    kinds = list(w.keys()); weights = [w[k] for k in kinds]; placed = 0; tries = 0
    while placed < n and tries < n * 40:
        tries += 1
        kind = random.choices(kinds, weights)[0]; rad, fn = PROPS[kind]
        x = random.uniform(x0 + rad, x1 - rad); y = random.uniform(y0 + rad, y1 - rad)
        if not free(x - rad - GAP, x + rad + GAP, y - rad - GAP, y + rad + GAP): continue
        a = random.choice((0.0, math.pi / 2, random.uniform(0, math.pi)))
        fn("Prop_%s_%s_%03d" % (reg, kind, placed + 1), x, y, a)
        mark(x - rad, x + rad, y - rad, y + rad); placed += 1
        COUNT[kind] = COUNT.get(kind, 0) + 1
    COUNT[reg] = placed
STATS["props"] = COUNT
