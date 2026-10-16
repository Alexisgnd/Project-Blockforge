
# ==== 02 : ground, hub, corridors, ramparts, bounds ====
def facing_rot(px, py, tx, ty):
    """rot_z for a vertical text/board (rot_x=90) at p so that its front faces point t."""
    return math.atan2(tx - px, -(ty - py))

def outward_rot(dx, dy):
    """rot_z for a floor text whose 'up' points along (dx,dy) (read while moving outward)."""
    return math.atan2(-dx, dy)

def annulus(name, cx, cy, r0, r1, z, mat, coll, seg=48):
    v = []; f = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        v += [(cx + r0*math.cos(a), cy + r0*math.sin(a), z), (cx + r1*math.cos(a), cy + r1*math.sin(a), z)]
    for i in range(seg):
        j = (i + 1) % seg; f.append((2*i, 2*i+1, 2*j+1, 2*j))
    return make_obj(name, v, f, mat, coll, origin=(cx, cy, z), uv=False)

HALF = 300.0; CELL = 10.0; HUB = 40.0
ROUGH = (-280.0, -50.0)   # rough-terrain patch extent on x and y (hole in the ground)

# --- ground 600 x 600, 10 m quads, hub faces lighter, hole for the rough patch ---
n = int(2 * HALF / CELL)
verts = [(-HALF + i*CELL, -HALF + j*CELL, 0.0) for j in range(n+1) for i in range(n+1)]
faces = []; fm = []
for j in range(n):
    for i in range(n):
        cx = -HALF + (i + .5)*CELL; cy = -HALF + (j + .5)*CELL
        if ROUGH[0] < cx < ROUGH[1] and ROUGH[0] < cy < ROUGH[1]: continue
        a = j*(n+1) + i; faces.append((a, a+1, a+n+2, a+n+1))
        fm.append(1 if (abs(cx) < HUB and abs(cy) < HUB) else 0)
g = make_obj("Ground", verts, faces, ["Ground", "Hub"], "00_Ground", face_mats=fm)
bpy.context.view_layer.objects.active = g
bm = bmesh.new(); bm.from_mesh(g.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS'); bm.to_mesh(g.data); bm.free()

# --- hub : spawn point, spawn ring, zone signs ---
sp = bpy.data.objects.new("SpawnPoint", None); sp.empty_display_type = 'ARROWS'; sp.empty_display_size = 3
sp.location = (0, 0, 0.5); col("01_Hub").objects.link(sp)
annulus("Hub_SpawnRing", 0, 0, 5.0, 5.6, 0.012, "Marking", "01_Hub")
floor_text("Hub_SpawnLabel", "SPAWN", 0, -8.5, 2.2, "01_Hub")
# hub border line
for k, (x0, y0, x1, y1) in enumerate([(-HUB, -HUB, HUB, -HUB+0.4), (-HUB, HUB-0.4, HUB, HUB),
                                       (-HUB, -HUB, -HUB+0.4, HUB), (HUB-0.4, -HUB, HUB, HUB)]):
    quad("Hub_Border_%d" % k, x0, y0, x1, y1, 0.012, "Marking", "01_Hub")

SIGNS = [("RAMPS & STEPS", 1, 1, "Ramps"), ("SHOOTING RANGE", -1, 1, "Range"),
         ("ROUGH TERRAIN", -1, -1, "Rough"), ("AIR COURSE", 1, -1, "Air"),
         ("MULTI-LEVEL", -1, 0, "Complex"), ("COMBAT YARD", 0, -1, "Yard")]
for label, sx, sy, mat in SIGNS:
    px, py = sx * 33.0, sy * 33.0
    a = facing_rot(px, py, 0, 0)
    nx, ny = -px / math.hypot(px, py), -py / math.hypot(px, py)
    ux, uy = math.cos(a), math.sin(a)
    tag = mat
    box("Sign_%s_Panel" % tag, (px, py, 6.0), (16, 0.4, 4), mat, "01_Hub", rot_z=a)
    for s in (-1, 1):
        box("Sign_%s_Post%d" % (tag, (s+1)//2), (px + s*6*ux, py + s*6*uy, 2.0), (0.5, 0.5, 4.0), "Dark", "01_Hub", rot_z=a)
    text("Sign_%s_Text" % tag, label, (px + nx*0.27, py + ny*0.27, 6.0), 1.5, "Marking", "01_Hub",
         rot=(math.pi/2, 0, a), extrude=0.05)

# --- corridors : distance markers every 50 m along the 4 axes ---
for dname, dx, dy in (("N", 0, 1), ("E", 1, 0)):   # W and S corridors are now zones
    for D in range(50, 300, 50):
        if dx == 0:
            quad("Corr_%s_%03d_Line" % (dname, D), -HUB, dy*D - 0.25, HUB, dy*D + 0.25, 0.012, "Marking", "02_Corridors")
        else:
            quad("Corr_%s_%03d_Line" % (dname, D), dx*D - 0.25, -HUB, dx*D + 0.25, HUB, 0.012, "Marking", "02_Corridors")
        floor_text("Corr_%s_%03d_Label" % (dname, D), "%d m" % D, dx*(D - 5), dy*(D - 5), 3.5, "02_Corridors",
                   rot_z=outward_rot(dx, dy))

# --- ramparts : rounded-square cliff ring around the playable area ---
def rampart_h(x, y):
    R = 60.0
    qx = abs(x) - (HALF - R); qy = abs(y) - (HALF - R)
    d = math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - R
    d += noise.noise(Vector((x*0.011, y*0.011, 3.1))) * 9.0
    if d <= 0: return -0.3
    h = 4.0 * smooth(d / 12.0)                       # talus
    h += 44.0 * smooth((d - 10.0) / 22.0)            # cliff
    h += max(d - 40.0, 0) * 0.35                     # plateau keeps rising
    h += noise.fractal(Vector((x*0.018, y*0.018, 7.0)), 1.0, 2.0, 4) * 6.0 * smooth(d / 25.0)
    return h - 0.3

EXT = 420.0; RS = 6.0; m = int(2*EXT/RS)
hv = [[rampart_h(-EXT + i*RS, -EXT + j*RS) for i in range(m+1)] for j in range(m+1)]
verts = [(-EXT + i*RS, -EXT + j*RS, hv[j][i]) for j in range(m+1) for i in range(m+1)]
faces = []
for j in range(m):
    for i in range(m):
        if max(hv[j][i], hv[j][i+1], hv[j+1][i], hv[j+1][i+1]) <= -0.29: continue
        a = j*(m+1) + i; faces.append((a, a+1, a+m+2, a+m+1))
r = make_obj("Ramparts", verts, faces, "Rampart", "03_Ramparts")
bm = bmesh.new(); bm.from_mesh(r.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY'); bm.to_mesh(r.data); bm.free()
world_uv(r)
for p in r.data.polygons: p.use_smooth = True

# --- invisible bounds (disable the renderer in Unity, keep the collider) ---
for nm, c, s in (("N", (0, 372, 100), (760, 2, 200)), ("S", (0, -372, 100), (760, 2, 200)),
                 ("E", (372, 0, 100), (2, 760, 200)), ("W", (-372, 0, 100), (2, 760, 200))):
    b = box("Bounds_%s" % nm, c, s, "Bounds", "09_Bounds_Invisible")
    b.display_type = 'WIRE'; b.hide_render = True
