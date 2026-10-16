
# ==== 04b : corridor W (multi-level complex) + corridor S (combat yard) ====
ZM = "14_MultiLevel"

# -- ziggurat : 3 tiers (2 / 4 / 6 m), one ramp per tier on a different side --
ZX, ZY = -82.0, 0.0
for i, (hx, hy) in enumerate(((18, 22), (13, 16), (8, 10))):
    top = 2.0 * (i + 1)
    box("Zig_Tier%d" % (i + 1), (ZX, ZY, top / 2), (2 * hx, 2 * hy, top), "Complex", ZM)
wedge("Zig_Ramp1", ZX + 18, ZX + 18 + 2 / math.tan(math.radians(15)), -4, 4, 0, 2, '-x', "Complex", ZM)
wedge("Zig_Ramp2", ZX - 4, ZX + 4, ZY + 16, ZY + 22, 2, 4, '-y', "Complex", ZM)
wedge("Zig_Ramp3", ZX - 13, ZX - 8, ZY - 3, ZY + 3, 4, 6, '+x', "Complex", ZM)
floor_text("Zig_Label", "ZIGGURAT 6 m", ZX, ZY - 4, 1.6, ZM, z=6)
target_at("Zig_Target", ZX, ZY + 5, 6, ZM, face_to=(0, 0))

# -- parking structure : 125 x 70 m, ground + 4 levels every 8 m (7.4 m clear), 15 deg ramps, open atrium --
PF = (-235.0, -110.0, -35.0, 35.0)
FH = 8.0
RL = FH / math.tan(math.radians(15))
HA = (-160.0, -160.0 + RL, -35.0, -25.0)     # strip A (south) : ramps rising +x
HB = (-200.0, -200.0 + RL, 25.0, 35.0)       # strip B (north) : ramps rising -x
ATR = (-190.0, -165.0, -10.0, 10.0)          # atrium, open on every level
wedge("Park_Ramp_0to1", *HA, 0.0, 8.0, '+x', "Complex", ZM)
wedge("Park_Ramp_1to2", *HB, 8.0, 16.0, '-x', "Complex", ZM)
wedge("Park_Ramp_2to3", *HA, 16.0, 24.0, '+x', "Complex", ZM)
wedge("Park_Ramp_3to4", *HB, 24.0, 32.0, '-x', "Complex", ZM)
T = 0.15
for k in range(1, 5):
    z = FH * k; hole = HA if k % 2 == 1 else HB
    slab_grid("Park_Slab_L%d" % k, PF, [hole, ATR], z, "Complex", ZM)
    rail_x("Park_L%d_RailS" % k, PF[0], PF[1], PF[2] + T, z, ZM, gaps=[(HA[0], HA[1])] if k <= 3 else [])
    rail_x("Park_L%d_RailN" % k, PF[0], PF[1], PF[3] - T, z, ZM, gaps=[(HB[0], HB[1])])
    rail_y("Park_L%d_RailW" % k, PF[2], PF[3], PF[0] + T, z, ZM, gaps={2: [(-24, -16)], 3: [(16, 24)]}.get(k, []))
    rail_y("Park_L%d_RailE" % k, PF[2], PF[3], PF[1] - T, z, ZM)
    if hole is HA:
        rail_x("Park_L%d_HoleSide" % k, HA[0], HA[1], HA[3] + T, z, ZM)
        rail_y("Park_L%d_HoleEnd" % k, HA[2], HA[3], HA[0] - T, z, ZM)
    else:
        rail_x("Park_L%d_HoleSide" % k, HB[0], HB[1], HB[2] - T, z, ZM)
        rail_y("Park_L%d_HoleEnd" % k, HB[2], HB[3], HB[1] + T, z, ZM)
    rail_x("Park_L%d_AtrS" % k, ATR[0] - 2 * T, ATR[1] + 2 * T, ATR[2] - T, z, ZM)
    rail_x("Park_L%d_AtrN" % k, ATR[0] - 2 * T, ATR[1] + 2 * T, ATR[3] + T, z, ZM)
    rail_y("Park_L%d_AtrW" % k, ATR[2], ATR[3], ATR[0] - T, z, ZM)
    rail_y("Park_L%d_AtrE" % k, ATR[2], ATR[3], ATR[1] + T, z, ZM)

def _in(px, py, r, m): return r[0] - m < px < r[1] + m and r[2] - m < py < r[3] + m
def _ov(r, q): return r[0] < q[1] and q[0] < r[1] and r[2] < q[3] and q[2] < r[3]
COLS_ = []
for x in (-234.5, -215, -195, -175, -155, -135, -110.5):
    for y in (-34.5, -12.5, 12.5, 34.5):
        if any(_in(x, y, r, 0.8) for r in (HA, HB, ATR)): continue
        box("Park_Col_%d_%d" % (int(-x), int(y)), (x, y, (4 * FH - 0.6) / 2), (1.0, 1.0, 4 * FH - 0.6), "Concrete", ZM)
        COLS_.append((x - 1.5, x + 1.5, y - 1.5, y + 1.5))

# roof penthouse (+5 m) with its own ramp
box("Park_Penthouse", (-223, 0, 32 + 2.5), (16, 12, 5), "Complex", ZM)
wedge("Park_Penthouse_Ramp", -215, -215 + 5 / math.tan(math.radians(15)), -4, 4, 32, 37, '-x', "Complex", ZM)
floor_text("Park_Penthouse_Label", "37 m", -223, -3, 2.5, ZM, z=37)
target_at("Park_Penthouse_Target", -227, 3, 37, ZM, face_to=(0, 0))

# per-level content : parked cars, cover walls, disk targets, one slider
random.seed(1234)
CAR_MATS = ("Steel", "Yard", "RedPaint", "Concrete")
for k in range(0, 5):
    z = FH * k
    lx = -222.0 if k < 4 else -140.0
    floor_text("Park_L%d_Label" % k, ("LEVEL %d  %d m" % (k, z)) if k else "PARKING", lx, 0, 2.5, ZM, z=z, rot_z=outward_rot(-1, 0))
    placed = [(lx - 3, lx + 3, -9, 9), (ATR[0] - 2, ATR[1] + 2, ATR[2] - 2, ATR[3] + 2)] + COLS_
    if k == 4: placed += [(-233, -195, -8, 8)]
    def try_place(w, h, tries=80):
        for _ in range(tries):
            x = random.uniform(PF[0] + 3 + w / 2, PF[1] - 3 - w / 2); y = random.uniform(-23 + h / 2, 23 - h / 2)
            r = (x - w / 2 - 1.5, x + w / 2 + 1.5, y - h / 2 - 1.5, y + h / 2 + 1.5)
            if any(_ov(r, q) for q in placed): continue
            placed.append(r); return x, y
        return None
    for i in range(7):
        rot90 = random.random() < 0.5; w, h = (2.0, 4.6) if rot90 else (4.6, 2.0)
        p = try_place(w, h)
        if p:
            m = random.choice(CAR_MATS); a_ = math.pi / 2 if rot90 else 0.0
            box("Park_L%d_Car%d" % (k, i + 1), (p[0], p[1], z + 0.6), (4.6, 2.0, 1.2), m, ZM, rot_z=a_)
            box("Park_L%d_Car%d_Cabin" % (k, i + 1), (p[0], p[1], z + 1.6), (2.4, 1.8, 0.8), m, ZM, rot_z=a_)
    for i in range(5):
        rot90 = random.random() < 0.5; w, h = (0.7, 5.0) if rot90 else (5.0, 0.7)
        p = try_place(w, h)
        if p: box("Park_L%d_Cover%d" % (k, i + 1), (p[0], p[1], z + 0.6), (5.0, 0.7, 1.2), "Concrete", ZM, rot_z=math.pi / 2 if rot90 else 0.0)
    for i in range(4):
        p = try_place(2.5, 2.5)
        if p: target_at("Park_L%d_Target%d" % (k, i + 1), p[0], p[1], z, ZM, face_to=(random.choice((PF[0], PF[1])), p[1] + random.uniform(-20, 20)))
    p = try_place(22, 3)
    if p: moving_linear("MoveLinear_Park_L%d" % k, (p[0] - 10, p[1], z), (p[0] + 10, p[1], z), ZM, face_to=(p[0], p[1] + (30 if p[1] < 0 else -30)), size=(3, 2, 3), speed=5)

# -- towers west of the parking : A (16 m) <- bridge from level 2, B (24 m) <- bridge from level 3, ramp A -> B --
box("TowerA", (-265, -20, 8.0), (20, 20, 16), "Complex", ZM)
box("TowerB", (-265, 20, 12.0), (20, 20, 24), "Complex", ZM)
box("Bridge_L2_TowerA", (-245, -20, 15.7), (20, 8, 0.6), "Complex", ZM)
box("Bridge_L3_TowerB", (-245, 20, 23.7), (20, 8, 0.6), "Complex", ZM)
prism_x("Ramp_TowerA_TowerB", [(-10, 16.0), (10, 24.0), (10, 23.4), (-10, 15.4)], -269, -261, "Complex", ZM, origin=(-265, 0, 20))
floor_text("TowerA_Label", "TOWER A 16 m", -265, -26, 2.0, ZM, z=16)
floor_text("TowerB_Label", "TOWER B 24 m", -265, 26, 2.0, ZM, z=24)
floor_text("Bridges_Label", "NO RAILS", -245, -20, 1.5, ZM, z=16, rot_z=outward_rot(-1, 0))
target_at("TowerA_Target", -272, -24, 16, ZM, face_to=(0, 0))
moving_rotor("MoveRotor_TowerB", (-265, 22, 27.0), ZM, arm=5, n_disks=2, r=1.2, speed=35, base_z=24.0)
floor_text("MultiLevel_Label", "MULTI-LEVEL", -46, 0, 3.0, ZM, rot_z=outward_rot(-1, 0))

# ==== corridor S : combat yard (containers, cover, watchtowers, bunker, dummies) ====
ZY_ = "15_Combat_Yard"
for nm, x, y, rot, z0, mat in (("C01", -20, -70, 0, 0, "Yard"), ("C02", 15, -85, 90, 0, "Steel"), ("C03", -25, -110, 90, 0, "Yard"),
                               ("C03b", -25, -110, 90, 2.6, "Steel"), ("C04", 20, -125, 0, 0, "Steel"), ("C05", 0, -150, 30, 0, "Yard"),
                               ("C06", -15, -170, 0, 0, "Steel"), ("C06b", -15, -170, 0, 2.6, "Yard"), ("C07", 22, -190, 90, 0, "Yard"),
                               ("C08", -5, -215, 0, 0, "Steel"), ("C09", 25, -240, 90, 0, "Steel"), ("C10", -25, -250, 0, 0, "Yard"),
                               ("C10b", -25, -250, 0, 2.6, "Steel")):
    box("Yard_Container_" + nm, (x, y, z0 + 1.3), (12, 2.5, 2.6), mat, ZY_, rot_z=math.radians(rot))
wedge("Yard_Container_C04_Ramp", 26, 36, -126.25, -123.75, 0, 2.6, '-x', "Concrete", ZY_)

for i, (x, y, rot) in enumerate(((10, -60, 0), (-8, -95, 90), (28, -105, 0), (-30, -140, 0), (8, -165, 90),
                                 (30, -160, 0), (-12, -195, 0), (5, -235, 90), (-30, -225, 90), (15, -265, 0))):
    box("Yard_Cover_%02d" % (i + 1), (x, y, 0.75), (6, 0.8, 1.5), "Concrete", ZY_, rot_z=math.radians(rot))

def watchtower(name, x, y, rise):
    box(name + "_Deck", (x, y, 7.75), (8, 8, 0.5), "Yard", ZY_)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("%s_Leg_%d%d" % (name, (sx + 1) // 2, (sy + 1) // 2), (x + sx * 3.5, y + sy * 3.5, 3.75), (0.6, 0.6, 7.5), "Dark", ZY_)
    L = 8.0 / math.tan(math.radians(20))
    if rise == '+y': wedge(name + "_Ramp", x - 3, x + 3, y - 4 - L, y - 4, 0, 8, '+y', "Concrete", ZY_)
    else:            wedge(name + "_Ramp", x - 3, x + 3, y + 4, y + 4 + L, 0, 8, '-y', "Concrete", ZY_)
    target_at(name + "_Target", x, y, 8, ZY_, face_to=(0, 0), size=(3, 2.5, 2.5))
watchtower("Yard_Tower1", 28, -75, '+y')
watchtower("Yard_Tower2", -28, -200, '-y')

box("Yard_Bunker_Back", (0, -276.5, 2.0), (14, 1, 4), "Concrete", ZY_)
box("Yard_Bunker_WallW", (-6.5, -272, 2.0), (1, 10, 4), "Concrete", ZY_)
box("Yard_Bunker_WallE", (6.5, -272, 2.0), (1, 10, 4), "Concrete", ZY_)
box("Yard_Bunker_Roof", (0, -272, 4.25), (14, 10, 0.5), "Yard", ZY_)
wedge("Yard_Bunker_Ramp", 7, 7 + 4.5 / math.tan(math.radians(15)), -276, -268, 0, 4.5, '-x', "Concrete", ZY_)
target_at("Yard_Bunker_Target", 0, -273.5, 0, ZY_, face_to=(0, 0))
target_at("Yard_Bunker_RoofTarget", 0, -272, 4.5, ZY_, face_to=(0, 0))

for i, (x, y) in enumerate(((-20, -76), (15, -95), (-25, -121), (20, -131), (0, -158), (-15, -176),
                            (22, -201), (-5, -221), (25, -252), (-25, -256))):
    target_at("Yard_Target_%02d" % (i + 1), x, y, 0, ZY_, face_to=(0, 0))
floor_text("Yard_Label", "COMBAT YARD", 0, -46, 3.0, ZY_, rot_z=outward_rot(0, -1))

# -- yard movers : pop-ups behind cover, two turntables --
for k, (x, y) in enumerate(((-32, -60), (32, -112), (-10, -128), (35, -178), (-35, -165), (12, -205), (-18, -238), (35, -270))):
    moving_popup("MovePopup_Yard_%d" % (k + 1), x, y, 0, ZY_, face_to=(0, 0))
moving_rotor("MoveRotor_Yard_1", (-30, -90, 2.5), ZY_, arm=4, n_disks=2, r=1.1, speed=45)
moving_rotor("MoveRotor_Yard_2", (30, -215, 2.5), ZY_, arm=4, n_disks=3, r=1.1, speed=-35)
