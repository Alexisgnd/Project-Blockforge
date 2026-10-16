
# ==== 03 : zone NE (ramps & steps) + zone NW (shooting range) ====
ZR = "10_Zone_Ramps"

# -- ramps : 8 slopes 10..45 deg, 5 m high, 10 m flat top, 12 m wide, same slope both sides --
H = 5.0; TOP = 10.0; W = 12.0; Y0 = 70.0
for i, a in enumerate([10, 15, 20, 25, 30, 35, 40, 45]):
    xc = 70 + i * 22; L = H / math.tan(math.radians(a))
    prof = [(Y0, 0), (Y0 + 2*L + TOP, 0), (Y0 + L + TOP, H), (Y0 + L, H)]
    prism_x("Ramp_%02ddeg" % a, prof, xc - W/2, xc + W/2, "Ramps", ZR, origin=(xc, Y0 + L + TOP/2, 0))
    floor_text("Ramp_%02ddeg_Label" % a, "%d°" % a, xc, Y0 - 5, 4.5, ZR)
    floor_text("Ramp_%02ddeg_TopLabel" % a, "%d°" % a, xc, Y0 + L + TOP/2, 3.0, ZR, z=H)

# -- steps : single blocks of increasing height --
SY0, SY1 = 165.0, 177.0
for i, h in enumerate([0.25, 0.5, 1.0, 1.5, 2.0, 3.0]):
    xc = 70 + i * 22
    box("Step_%03dcm" % int(h * 100), (xc, (SY0 + SY1) / 2, h / 2), (12, SY1 - SY0, h), "Ramps", ZR)
    floor_text("Step_%03dcm_Label" % int(h * 100), ("%g m" % h), xc, SY0 - 5, 3.0, ZR)
# -- stairs : 6 x 0.5 m, 2 m treads, 3 m drop at the back --
xc = 70 + 6 * 22
prof = [(SY0, 0), (SY1, 0), (SY1, 3.0), (175, 3.0), (175, 2.5), (173, 2.5), (173, 2.0), (171, 2.0), (171, 1.5),
        (169, 1.5), (169, 1.0), (167, 1.0), (167, 0.5), (165, 0.5)]
prism_x("Stairs_50cm", prof, xc - 6, xc + 6, "Ramps", ZR, origin=(xc, 171, 0))
floor_text("Stairs_Label", "STAIRS", xc, SY0 - 5, 3.0, ZR)
# -- climbing wall 6 m --
xc = 70 + 7 * 22
box("Wall_6m", (xc, 171, 3.0), (12, 4, 6.0), "Ramps", ZR)
floor_text("Wall_6m_Label", "WALL 6 m", xc, SY0 - 5, 3.0, ZR)

# -- side slopes (devers) : drive along Y across the slope --
for i, a in enumerate([15, 25, 35]):
    xs = 55 + i * 60; wdt = 22.0; hh = wdt * math.tan(math.radians(a))
    prism_y("SideSlope_%02ddeg" % a, [(xs, 0), (xs + wdt, 0), (xs + wdt, hh)], 215, 265, "Ramps", ZR,
            origin=(xs + wdt / 2, 240, 0))
    floor_text("SideSlope_%02ddeg_Label" % a, "SIDE %d°" % a, xs + wdt / 2, 209, 3.0, ZR)

# -- jump lines : kicker 18 deg + gap + landing ramp --
for h, xc in ((1.5, 248.0), (3.0, 272.0)):
    L = h / math.tan(math.radians(18)); gap = 8.0 + 4.0 * h; tag = "%02d" % int(h * 10)
    wedge("Jump_%s_Kicker" % tag, xc - 5, xc + 5, 70, 70 + L, 0, h, '+y', "Ramps", ZR)
    ly0 = 70 + L + gap; lr = 0.7 * h / math.tan(math.radians(12))
    wedge("Jump_%s_Landing" % tag, xc - 5, xc + 5, ly0, ly0 + lr, 0, 0.7 * h, '-y', "Ramps", ZR)
    floor_text("Jump_%s_Label" % tag, "JUMP %g m" % h, xc, 64, 2.2, ZR)

# -- speed bumps (4 x 0.25 m) between ramps and steps --
for k, yb in enumerate((144.0, 146.5, 149.0, 151.5)):
    prism_x("Bump_%d" % (k + 1), [(yb - 0.4, 0), (yb + 0.4, 0), (yb + 0.15, 0.25), (yb - 0.15, 0.25)], 60, 230, "Ramps", ZR, origin=(145, yb, 0))
floor_text("Bump_Label", "BUMPS", 52, 148, 2.5, ZR, rot_z=outward_rot(1, 0))
# -- slalom posts --
for k, x in enumerate(range(62, 232, 12)):
    box("Slalom_%02d" % (k + 1), (x, 186.0 if k % 2 == 0 else 198.0, 0.9), (0.6, 0.6, 1.8), "Ramps", ZR)
floor_text("Slalom_Label", "SLALOM", 52, 192, 2.5, ZR, rot_z=outward_rot(1, 0))
# -- 4-sided pyramid : 4 m platform, ramps at 10 / 20 / 30 / 40 deg --
PX, PY = 260.0, 195.0
box("Pyramid_Top", (PX, PY, 2.0), (12, 12, 4.0), "Ramps", ZR)
for a_, side in ((10, 'N'), (20, 'S'), (30, 'E'), (40, 'W')):
    L = 4.0 / math.tan(math.radians(a_))
    if side == 'N': wedge("Pyramid_Ramp%02d" % a_, PX - 6, PX + 6, PY + 6, PY + 6 + L, 0, 4, '-y', "Ramps", ZR); lx, ly = PX, PY + 6 + L + 3
    if side == 'S': wedge("Pyramid_Ramp%02d" % a_, PX - 6, PX + 6, PY - 6 - L, PY - 6, 0, 4, '+y', "Ramps", ZR); lx, ly = PX, PY - 6 - L - 3
    if side == 'E': wedge("Pyramid_Ramp%02d" % a_, PX + 6, PX + 6 + L, PY - 4, PY + 4, 0, 4, '-x', "Ramps", ZR); lx, ly = PX + 6 + L + 2.5, PY
    if side == 'W': wedge("Pyramid_Ramp%02d" % a_, PX - 6 - L, PX - 6, PY - 4, PY + 4, 0, 4, '+x', "Ramps", ZR); lx, ly = PX - 6 - L - 2.5, PY
    floor_text("Pyramid_Ramp%02d_Label" % a_, "%d°" % a_, lx, ly, 2.0, ZR)
target_at("Pyramid_Target", PX, PY + 2, 4.0, ZR, face_to=(PX, 0))
# -- moving target behind the side slopes + pop-ups at the jump landings --
moving_linear("MoveLinear_NE_Back", (55, 276, 0), (200, 276, 0), ZR, face_to=(127, 0), speed=6)
moving_popup("MovePopup_NE_Jump15", 248, 113, 0, ZR, face_to=(248, 0))
moving_popup("MovePopup_NE_Jump30", 272, 122, 0, ZR, face_to=(272, 0))

# ==== zone NW : shooting range (fire towards +Y) ====
ZT = "11_Zone_Range"
FX0, FX1, FY = -280.0, -50.0, 60.0
FIRE = ((FX0 + FX1) / 2, FY)
quad("Range_FireLine", FX0, FY - 0.6, FX1, FY + 0.6, 0.012, "RedPaint", ZT)
floor_text("Range_FireLine_Label", "FIRE LINE", -240, FY - 4, 2.5, ZT)
lw = (FX1 - FX0) / 6
for k in range(1, 6):
    x = FX1 - k * lw
    quad("Range_LaneLine_%d" % k, x - 0.15, FY, x + 0.15, 266, 0.012, "Marking", ZT)

box("Range_FiringDeck", (-165, 50, 1.5), (70, 12, 3), "Concrete", ZT)
wedge("Range_FiringDeck_Ramp", -130, -130 + 3 / math.tan(math.radians(15)), 44, 56, 0, 3, '-x', "Concrete", ZT)
floor_text("Range_FiringDeck_Label", "DECK 3 m", -165, 50, 3.0, ZT, z=3)

# 6 lanes x 3 disk sizes (S / M robot / L)
SIZES = (("S", (1.5, 1.5, 1.5), -11.0), ("M", (4.0, 3.0, 3.0), 0.0), ("L", (6.0, 4.0, 5.0), 11.0))
for k, D in enumerate([10, 25, 50, 100, 150, 200]):
    xc = FX1 - (k + 0.5) * lw
    for tag, sz, off in SIZES:
        target_at("Target_%03dm_%s" % (D, tag), xc + off, FY + D, 0.0, ZT, face_to=(xc + off, FY - 100), size=sz)
    floor_text("Target_%03dm_Label" % D, "%d m" % D, xc, FY + D - 5, 3.0, ZT)

# elevated targets on pillars
for h, x, y in ((5, -69.0, 140.0), (10, -107.0, 190.0), (15, -146.0, 240.0), (25, -69.0, 240.0)):
    box("Target_High%02d_Pillar" % h, (x, y, h / 2), (2.5, 2.5, h), "Concrete", ZT)
    target_at("Target_High%02d" % h, x, y, h, ZT, face_to=(x, FY - 100))
    floor_text("Target_High%02d_Label" % h, "H %d m" % h, x, y - 6, 2.5, ZT)

# pop-up targets in every lane
for k, D in ((0, 6), (1, 15), (2, 35), (3, 70), (4, 120), (5, 170)):
    xc = FX1 - (k + 0.5) * lw
    moving_popup("MovePopup_Range_%d" % (k + 1), xc - 6, FY + D, 0, ZT, face_to=(xc - 6, FY - 100))
# sliders : far rails + mid rail (behind the short lanes)
moving_linear("MoveLinear_Range_Far1", (-270, 272, 0), (-170, 272, 0), ZT, face_to=(-220, FY - 100), speed=5)
moving_linear("MoveLinear_Range_Far2", (-160, 272, 0), (-60, 272, 0), ZT, face_to=(-110, FY - 100), speed=7)
moving_linear("MoveLinear_Range_Mid", (-160, 150, 0), (-55, 150, 0), ZT, face_to=(-107, FY - 100), size=(3, 2, 3), speed=6)
# turntables
moving_rotor("MoveRotor_Range_1", (-260, 115, 3.0), ZT, arm=4, n_disks=2, r=1.2, speed=40)
moving_rotor("MoveRotor_Range_2", (-184, 130, 3.0), ZT, arm=4, n_disks=2, r=1.2, speed=-30)
# orbiting "drones" (anti-air) : disks on an invisible rotor
moving_rotor("MoveOrbit_Range_1", (-210, 190, 22), ZT, arm=12, n_disks=3, r=1.3, speed=20, post=False, arms=False)
moving_rotor("MoveOrbit_Range_2", (-245, 230, 32), ZT, arm=10, n_disks=2, r=1.3, speed=-25, post=False, arms=False)
moving_rotor("MoveOrbit_Range_3", (-150, 235, 40), ZT, arm=14, n_disks=3, r=1.3, speed=15, post=False, arms=False)

box("Range_Backstop", ((FX0 + FX1) / 2, 283.0, 6.0), (FX1 - FX0 + 6, 2.0, 12.0), "Concrete", ZT)
