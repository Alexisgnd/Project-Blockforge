
# ==== 04 : zone SW (rough terrain) + zone SE (air course) ====
ZG = "12_Zone_Rough"
CRATERS = ((-195.0, -105.0, 16.0, 3.5), (-115.0, -225.0, 13.0, 3.0), (-215.0, -225.0, 10.0, 2.5))
TRENCH_Y = -165.0

MESA = (-160.0, -262.0, 31.5, 10.0)   # centre x, y, half-size x (with ramps), half-size y

def rough_h(x, y):
    e = min(x - ROUGH[0], ROUGH[1] - x, y - ROUGH[0], ROUGH[1] - y)
    w = smooth(e / 14.0)
    r = min(max((-(x + 50) - (y + 50)) / 400.0, 0.0), 1.0)          # 0 near the hub -> 1 far corner
    h = noise.fractal(Vector((x * 0.035, y * 0.035, 1.7)), 1.0, 2.0, 3) * (0.4 + 3.6 * r)
    if -272 <= x <= -238:                                             # moguls strip (8 m wave)
        t = smooth(min(x + 272, -238 - x) / 4.0)
        h = h * (1 - t) + t * 0.45 * math.sin(2 * math.pi * y / 8.0)
    dt = abs(y - TRENCH_Y)                                            # trench 12 m wide, 1.8 m deep
    if dt < 6 and x > -232:
        h -= 1.8 * (0.5 + 0.5 * math.cos(math.pi * dt / 6.0)) * smooth((x + 232) / 6.0)
    for cx, cy, R, D in CRATERS:                                      # bowl + rim
        d = math.hypot(x - cx, y - cy) / R
        if d < 1.6:
            k = smooth((1.6 - d) / 0.6)
            h = h * (1 - 0.8 * k) + (-D + (D + 1.0) * d * d if d < 1 else 1.0 * k)
    dm = max(abs(x - MESA[0]) - MESA[2], abs(y - MESA[1]) - MESA[3])   # flatten around the mesa
    return h * w * smooth(dm / 8.0)

RS2 = 2.0; nr = int((ROUGH[1] - ROUGH[0]) / RS2)
verts = [(ROUGH[0] + i*RS2, ROUGH[0] + j*RS2, rough_h(ROUGH[0] + i*RS2, ROUGH[0] + j*RS2)) for j in range(nr+1) for i in range(nr+1)]
faces = [(j*(nr+1)+i, j*(nr+1)+i+1, (j+1)*(nr+1)+i+1, (j+1)*(nr+1)+i) for j in range(nr) for i in range(nr)]
rt = make_obj("RoughTerrain", verts, faces, "Rough", ZG, origin=((ROUGH[0]+ROUGH[1])/2, (ROUGH[0]+ROUGH[1])/2, 0))
for p in rt.data.polygons: p.use_smooth = True

placed = 0; tries = 0
while placed < 45 and tries < 2000:
    tries += 1
    x = random.uniform(ROUGH[0] + 10, ROUGH[1] - 10); y = random.uniform(ROUGH[0] + 10, ROUGH[1] - 10)
    if -276 <= x <= -234 or abs(y - TRENCH_Y) < 10: continue
    if any(math.hypot(x - cx, y - cy) < R * 1.3 for cx, cy, R, D in CRATERS): continue
    if -(x + 50) - (y + 50) < 40: continue
    if abs(x - MESA[0]) < MESA[2] + 10 and abs(y - MESA[1]) < MESA[3] + 10: continue
    s = random.uniform(0.7, 3.2)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    sx, sy, sz = random.uniform(0.8, 1.3), random.uniform(0.8, 1.3), random.uniform(0.5, 0.9)
    for v in bm.verts:
        nz = 1.0 + 0.28 * noise.noise(v.co * 1.7 + Vector((placed * 3.1, 0, 0)))
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz)) * s * nz
    rz = random.uniform(0, 2 * math.pi)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(rz, 3, 'Z'))
    z = rough_h(x, y) - 0.3 * s
    vv = [tuple(Vector((x, y, z)) + v.co) for v in bm.verts]; ff = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    make_obj("Rock_%02d" % placed, vv, ff, "Rampart", ZG, origin=(x, y, z), recalc=True)
    placed += 1

floor_text("Rough_Moguls_Label", "MOGULS", -255, -45, 3.0, ZG)
floor_text("Rough_Trench_Label", "TRENCH", -45, TRENCH_Y, 3.0, ZG, rot_z=outward_rot(-1, 0))
floor_text("Rough_Gentle_Label", "ROUGH: GENTLE > HARD", -120, -45, 2.5, ZG)

# ==== zone SE : air course ====
ZA = "13_Zone_Air"
# altitude tower : 3 x 3 m, 120 m, alternating 10 m bands, labels facing the hub
TX, TY = 60.0, -60.0
a = facing_rot(TX, TY, 0, 0); nx, ny = -TX / math.hypot(TX, TY), -TY / math.hypot(TX, TY)
for k in range(12):
    box("AltTower_%03dm" % ((k + 1) * 10), (TX, TY, k * 10 + 5), (3, 3, 10), "Air" if k % 2 == 0 else "Marking", ZA, rot_z=a)
    text("AltTower_%03dm_Label" % ((k + 1) * 10), "%d" % ((k + 1) * 10), (TX + nx * 1.6, TY + ny * 1.6, k * 10 + 9.0),
         1.6, "Dark", ZA, rot=(math.pi / 2, 0, a), extrude=0.05)

# landing pads on pillars
for h, px, py in ((5, 95, -130), (10, 140, -185), (20, 195, -130), (30, 240, -185)):
    box("Pad_%02dm" % h, (px, py, h - 0.5), (14, 14, 1.0), "Air", ZA)
    box("Pad_%02dm_Pillar" % h, (px, py, (h - 1) / 2), (4, 4, h - 1), "Concrete", ZA)
    floor_text("Pad_%02dm_Label" % h, "%d m" % h, px, py, 4.0, ZA, z=h)

# runway markings (along +X)
RY = -75.0
quad("Runway_EdgeL", 90, RY - 10.25, 280, RY - 9.75, 0.012, "Marking", ZA)
quad("Runway_EdgeR", 90, RY + 9.75, 280, RY + 10.25, 0.012, "Marking", ZA)
for k in range(15):
    x0 = 105 + k * 12
    quad("Runway_Dash_%02d" % k, x0, RY - 0.3, x0 + 6, RY + 0.3, 0.012, "Marking", ZA)
floor_text("Runway_Label", "RUNWAY", 96, RY, 3.5, ZA, rot_z=outward_rot(1, 0))

# rings (major r 7 m, tube 0.55 m), numbered, oriented along the course
def torus(name, center, tangent, R=7.0, r=0.55, seg=32, sides=8):
    q = Vector((0, 0, 1)).rotation_difference(tangent.normalized())
    v = []; f = []
    for i in range(seg):
        a1 = 2 * math.pi * i / seg
        for j in range(sides):
            b = 2 * math.pi * j / sides
            p = Vector(((R + r * math.cos(b)) * math.cos(a1), (R + r * math.cos(b)) * math.sin(a1), r * math.sin(b)))
            v.append(Vector(center) + q @ p)
    for i in range(seg):
        for j in range(sides):
            i2 = (i + 1) % seg; j2 = (j + 1) % sides
            f.append((i * sides + j, i2 * sides + j, i2 * sides + j2, i * sides + j2))
    ob = make_obj(name, v, f, "Air", ZA, origin=center, recalc=True)
    for p in ob.data.polygons: p.use_smooth = True
    return ob

COURSE = [(70, -215, 3), (90, -250, 10), (140, -268, 16), (195, -262, 22), (245, -240, 28), (270, -190, 34),
          (258, -128, 40), (205, -100, 34), (150, -100, 26), (100, -168, 16)]
for k in range(1, len(COURSE)):
    p = Vector(COURSE[k]); prv = Vector(COURSE[k - 1]); nxt = Vector(COURSE[k + 1]) if k + 1 < len(COURSE) else p + (p - prv)
    t = (nxt - prv); t.z = 0
    torus("Ring_%02d" % k, tuple(p), t)
    ra = facing_rot(p.x, p.y, prv.x, prv.y)
    text("Ring_%02d_Number" % k, "%d" % k, (p.x, p.y, p.z + 9.5), 3.0, "Marking", ZA, rot=(math.pi / 2, 0, ra), extrude=0.1)
floor_text("Air_Start_Label", "RINGS START", 70, -205, 3.0, ZA)

# -- mesa 6 m with two access ramps (rough zone) --
box("Rough_Mesa", (-160, -262, 2.0), (30, 20, 8.0), "Rampart", ZG)
wedge("Rough_Mesa_RampE", -145, -128.5, -268, -256, 0, 6, '-x', "Rough", ZG)
wedge("Rough_Mesa_RampW", -191.5, -175, -268, -256, 0, 6, '+x', "Rough", ZG)
floor_text("Rough_Mesa_Label", "MESA 6 m", -160, -258, 3.0, ZG, z=6)
target_at("Rough_Mesa_Target", -160, -266, 6, ZG, face_to=(0, 0))

# -- sky stairs : 6 platforms every 3 m linked by ramps --
SY = -225.0
wedge("SkyStairs_Ramp0", 93, 105, SY - 4, SY + 4, 0, 3, '+x', "Air", ZA)
for i in range(1, 7):
    xc = 110 + 22 * (i - 1); h = 3.0 * i
    box("SkyStairs_P%d" % i, (xc, SY, h - 0.4), (10, 10, 0.8), "Air", ZA)
    box("SkyStairs_P%d_Pillar" % i, (xc, SY, (h - 0.8) / 2), (2, 2, h - 0.8), "Concrete", ZA)
    floor_text("SkyStairs_P%d_Label" % i, "%d m" % int(h), xc, SY, 2.5, ZA, z=h)
    if i < 6:
        wedge("SkyStairs_Ramp%d" % i, xc + 5, xc + 17, SY - 4, SY + 4, h, h + 3, '+x', "Air", ZA)
for k, (dx, dy) in enumerate(((-3.5, 3.5), (3.5, -3.5))):
    target_at("SkyStairs_Target_%d" % (k + 1), 220 + dx, SY + dy, 18, ZA, face_to=(0, 0), size=(2, 2, 2))

# -- air zone : orbiting drones, runway slider, pop-ups on the landing pads --
moving_rotor("MoveOrbit_Air_1", (150, -150, 25), ZA, arm=20, n_disks=3, r=1.5, speed=15, post=False, arms=False)
moving_rotor("MoveOrbit_Air_2", (225, -235, 45), ZA, arm=15, n_disks=2, r=1.5, speed=-20, post=False, arms=False)
moving_rotor("MoveOrbit_Air_3", (60, -60, 60), ZA, arm=18, n_disks=4, r=1.5, speed=12, post=False, arms=False)
moving_linear("MoveLinear_Air_Runway", (100, -95, 0), (270, -95, 0), ZA, face_to=(185, 0), speed=12)
for h, px, py in ((5, 95, -130), (10, 140, -185), (20, 195, -130), (30, 240, -185)):
    moving_popup("MovePopup_Pad%02d" % h, px + 3.5, py + 3.5, h, ZA, face_to=(0, 0))

# -- rough zone : ruins (broken walls) + pop-up targets --
for g, (rx, ry) in enumerate(((-110, -75), (-200, -140), (-250, -205), (-95, -262), (-70, -190))):
    a0 = random.uniform(0, math.pi)
    for k in range(random.randint(3, 5)):
        L = random.uniform(4, 9); h = random.uniform(1.0, 4.5)
        a = a0 + (math.pi / 2 if k % 2 else 0)
        ox, oy = random.uniform(-7, 7), random.uniform(-7, 7)
        x, y = rx + ox, ry + oy; z0 = rough_h(x, y) - 0.6
        box("Rough_Ruin%d_Wall%d" % (g + 1, k + 1), (x, y, z0 + (h + 0.6) / 2), (L, 0.7, h + 0.6), "Concrete", ZG, rot_z=a)
for k, (x, y) in enumerate(((-90, -140), (-130, -195), (-235, -150), (-230, -262), (-170, -95))):
    moving_popup("MovePopup_Rough_%d" % (k + 1), x, y, rough_h(x, y) - 0.05, ZG, face_to=(0, 0))
