
# ==== BlockForge — Map_TestGround — 01 : setup, textures, helpers ====
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Vector, Matrix, noise

REPO = r"D:\unity projects\Project-Blockforge"
SRC_DIR = os.path.join(REPO, "Tools", "Maps", "TestGround")
TEX_DIR = os.path.join(REPO, "Assets", "_Project", "Art", "Textures", "Map", "TestGround")
FBX_PATH = os.path.join(REPO, "Assets", "_Project", "Art", "Models", "Environment", "Map_TestGround.fbx")
BLEND_PATH = os.path.join(SRC_DIR, "Map_TestGround.blend")
os.makedirs(SRC_DIR, exist_ok=True); os.makedirs(TEX_DIR, exist_ok=True)
random.seed(42)

# --- reset scene ---
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.collections): bpy.data.collections.remove(c)
for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.curves, bpy.data.lights, bpy.data.cameras):
    for d in list(blk): blk.remove(d)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1.0

ROOT = bpy.data.collections.new("MAP_TestGround"); scene.collection.children.link(ROOT)
COLS = {}
def col(name):
    if name not in COLS:
        c = bpy.data.collections.new(name); ROOT.children.link(c); COLS[name] = c
    return COLS[name]

# --- grid textures : 1 tile = 10 m, minor line every 1 m, major line on the tile border ---
TEX = 1024
def grid_png(name, bg):
    bg = np.array(bg, dtype=np.float32) / 255.0
    img = np.ones((TEX, TEX, 4), dtype=np.float32); img[..., :3] = bg
    minor = bg * 0.80; major = bg * 0.55
    for i in range(1, 10):
        p = int(round(i * TEX / 10)); img[p-1:p+2, :, :3] = minor; img[:, p-1:p+2, :3] = minor
    img[5*TEX//10-2:5*TEX//10+3, :, :3] = bg * 0.70; img[:, 5*TEX//10-2:5*TEX//10+3, :3] = bg * 0.70  # 5 m line
    for sl in (slice(0, 4), slice(TEX-4, TEX)):
        img[sl, :, :3] = major; img[:, sl, :3] = major
    im = bpy.data.images.new("T_TG_" + name, TEX, TEX, alpha=False)
    im.pixels.foreach_set(img.ravel())
    path = os.path.join(TEX_DIR, "T_TG_%s.png" % name)
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()
    bpy.data.images.remove(im)
    return bpy.data.images.load(path, check_existing=True)

MATS = {}
def material(name, color=None, tex_bg=None, rough=0.85, emit=0.0):
    m = bpy.data.materials.new("M_TG_" + name); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = 0.0
    if tex_bg is not None:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = grid_png(name, tex_bg); t.location = (-400, 300)
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        m.diffuse_color = (*[ (c/255.0)**2.2 for c in tex_bg], 1)
    else:
        lin = [c ** 2.2 for c in color]
        bsdf.inputs["Base Color"].default_value = (*lin, 1); m.diffuse_color = (*lin, 1)
        if emit > 0:
            bsdf.inputs["Emission Color"].default_value = (*lin, 1); bsdf.inputs["Emission Strength"].default_value = emit
    MATS[name] = m
    return m

material("Ground",  tex_bg=(128, 130, 134))
material("Hub",     tex_bg=(188, 190, 194))
material("Rampart", tex_bg=(168, 163, 155))
material("Ramps",   tex_bg=(222, 128, 46))   # zone rampes : orange
material("Range",   tex_bg=(196, 58, 50))    # zone tir : rouge
material("Rough",   tex_bg=(170, 146, 110))  # zone accidentee : sable
material("Air",     tex_bg=(46, 166, 196))   # zone aerienne : cyan
material("Concrete",tex_bg=(150, 150, 150))
material("Marking", color=(0.93, 0.93, 0.90), rough=0.6)
material("Dark",    color=(0.12, 0.12, 0.13), rough=0.7)
material("Bounds",  color=(1.0, 0.0, 1.0), rough=1.0)
material("RedPaint", color=(0.85, 0.16, 0.12), rough=0.6)
material("Complex", tex_bg=(118, 132, 156))  # complexe a etages : ardoise
material("Yard",    tex_bg=(156, 84, 56))    # cour de combat : rouille
material("Steel",   tex_bg=(70, 104, 138))   # conteneurs bleus
material("Wood",    tex_bg=(150, 112, 70))   # caisses
material("TgtWhite",  color=(0.95, 0.95, 0.93), rough=0.6)
material("TgtBlack",  color=(0.10, 0.10, 0.11), rough=0.6)
material("TgtBlue",   color=(0.12, 0.45, 0.85), rough=0.6)
material("TgtRed",    color=(0.88, 0.15, 0.10), rough=0.6)
material("TgtYellow", color=(0.98, 0.80, 0.12), rough=0.6)

# --- helpers ---
def world_uv(ob, scale=10.0):
    me = ob.data; bm = bmesh.new(); bm.from_mesh(me)
    uvl = bm.loops.layers.uv.verify(); mw = ob.matrix_world; rot = mw.to_3x3()
    for f in bm.faces:
        n = rot @ f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = mw @ l.vert.co
            u, v = (p.x, p.y) if ax == 2 else ((p.y, p.z) if ax == 0 else (p.x, p.z))
            l[uvl].uv = (u / scale, v / scale)
    bm.to_mesh(me); bm.free()

def make_obj(name, verts, faces, mats, coll, origin=None, face_mats=None, recalc=False, uv=True):
    if isinstance(mats, str): mats = [mats]
    o = Vector(origin) if origin is not None else Vector((0, 0, 0))
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(Vector(v) - o) for v in verts], [], faces); me.update()
    ob = bpy.data.objects.new(name, me); ob.location = o; col(coll).objects.link(ob)
    for m in mats: me.materials.append(MATS[m])
    if face_mats is not None: me.polygons.foreach_set("material_index", face_mats)
    if recalc:
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    ob.matrix_world = Matrix.Translation(o)
    if uv: world_uv(ob)
    return ob

def box(name, center, size, mat, coll, rot_z=0.0):
    cx, cy, cz = center; a, b, c = [s / 2 for s in size]
    R = Matrix.Rotation(rot_z, 3, 'Z')
    loc = [(-a,-b,-c),(a,-b,-c),(a,b,-c),(-a,b,-c),(-a,-b,c),(a,-b,c),(a,b,c),(-a,b,c)]
    v = [Vector(center) + R @ Vector(p) for p in loc]
    f = [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    return make_obj(name, v, f, mat, coll, origin=center)

def prism_x(name, profile_yz, x0, x1, mat, coll, origin=None):
    """Extrude a convex (y,z) profile between x0 and x1."""
    n = len(profile_yz)
    v = [(x0, y, z) for y, z in profile_yz] + [(x1, y, z) for y, z in profile_yz]
    f = [tuple(range(n)), tuple(range(n, 2*n))]
    for i in range(n):
        j = (i + 1) % n; f.append((i, j, n + j, n + i))
    if origin is None:
        origin = ((x0 + x1) / 2, sum(p[0] for p in profile_yz) / n, 0.0)
    return make_obj(name, v, f, mat, coll, origin=origin, recalc=True)

def prism_y(name, profile_xz, y0, y1, mat, coll, origin=None):
    n = len(profile_xz)
    v = [(x, y0, z) for x, z in profile_xz] + [(x, y1, z) for x, z in profile_xz]
    f = [tuple(range(n)), tuple(range(n, 2*n))]
    for i in range(n):
        j = (i + 1) % n; f.append((i, j, n + j, n + i))
    if origin is None:
        origin = (sum(p[0] for p in profile_xz) / n, (y0 + y1) / 2, 0.0)
    return make_obj(name, v, f, mat, coll, origin=origin, recalc=True)

def quad(name, x0, y0, x1, y1, z, mat, coll):
    v = [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]
    return make_obj(name, v, [(0, 1, 2, 3)], mat, coll, origin=((x0+x1)/2, (y0+y1)/2, z), uv=False)

FONT = None
for fp in (r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\segoeuib.ttf"):
    if os.path.exists(fp):
        FONT = bpy.data.fonts.load(fp, check_existing=True); break

def text(name, body, loc, size, mat, coll, rot=(0, 0, 0), extrude=0.0):
    cu = bpy.data.curves.new(name, 'FONT'); cu.body = body; cu.size = size
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'; cu.extrude = extrude; cu.resolution_u = 3
    if FONT: cu.font = FONT
    tmp = bpy.data.objects.new(name + "_tmp", cu); scene.collection.objects.link(tmp)
    tmp.location = loc; tmp.rotation_euler = rot
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    mw = tmp.matrix_world.copy()
    bpy.data.objects.remove(tmp, do_unlink=True); bpy.data.curves.remove(cu)
    me.materials.clear(); me.materials.append(MATS[mat])
    ob = bpy.data.objects.new(name, me); ob.matrix_world = mw; col(coll).objects.link(ob)
    return ob

def floor_text(name, body, x, y, size, coll, z=0.0, rot_z=0.0, mat="Marking"):
    return text(name, body, (x, y, z + 0.015), size, mat, coll, rot=(0, 0, rot_z))

def smooth(t):
    t = min(max(t, 0.0), 1.0); return t * t * (3 - 2 * t)

TGT_MATS = ["TgtYellow", "TgtRed", "TgtBlue", "TgtBlack", "TgtWhite", "Dark"]

def disk_target(name, cx, cy, cz, r, coll, face_to, thick=0.16, seg=32):
    """Round archery-style target : 5 concentric rings (yellow centre -> white), dark back and rim.
    Origin at the disk centre ; front faces the point face_to."""
    n = Vector((face_to[0] - cx, face_to[1] - cy, 0))
    n = n.normalized() if n.length > 1e-6 else Vector((0, -1, 0))
    R = Matrix.Rotation(math.atan2(n.x, -n.y), 3, 'Z')
    yf, yb = -thick / 2, thick / 2
    loc = [(0, yf, 0)]
    for k in range(5):
        rk = r * (k + 1) / 5
        loc += [(rk * math.cos(2 * math.pi * i / seg), yf, rk * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
    loc += [(r * math.cos(2 * math.pi * i / seg), yb, r * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
    ring = lambda k, i: 1 + k * seg + (i % seg)
    back = lambda i: 1 + 5 * seg + (i % seg)
    f = []; fm = []
    for i in range(seg): f.append((0, ring(0, i), ring(0, i + 1))); fm.append(0)
    for k in range(1, 5):
        for i in range(seg): f.append((ring(k - 1, i), ring(k, i), ring(k, i + 1), ring(k - 1, i + 1))); fm.append(k)
    f.append(tuple(back(i) for i in reversed(range(seg)))); fm.append(5)
    for i in range(seg): f.append((ring(4, i), back(i), back(i + 1), ring(4, i + 1))); fm.append(5)
    c = Vector((cx, cy, cz))
    return make_obj(name, [c + R @ Vector(p) for p in loc], f, TGT_MATS, coll, origin=(cx, cy, cz),
                    face_mats=fm, recalc=True, uv=False)

def target_stand(name, x, y, z0, cz, coll, face_to):
    n = Vector((face_to[0] - x, face_to[1] - y, 0)); n = n.normalized() if n.length > 1e-6 else Vector((0, -1, 0))
    a = math.atan2(n.x, -n.y); px, py = x - n.x * 0.25, y - n.y * 0.25
    return [box(name + "_Post", (px, py, (z0 + cz) / 2), (0.25, 0.25, cz - z0), "Dark", coll, rot_z=a),
            box(name + "_Base", (px, py, z0 + 0.1), (1.4, 1.0, 0.2), "Dark", coll, rot_z=a)]

def target_at(name, x, y, z0, coll, face_to, size=(4.0, 3.0, 3.0), stand=True):
    """Disk target on a post (radius from size) ; stand=False -> floating disk centred at z0 + r."""
    r = 0.42 * max(size[0], size[2])
    cz = z0 + (0.6 + r if stand else r)
    objs = [disk_target(name, x, y, cz, r, coll, face_to)]
    if stand: objs += target_stand(name, x, y, z0, cz, coll, face_to)
    return objs

# ---- moving targets : static root empty (with bf_* custom properties) + moving child empty ----
MOVERS = []
def empty(name, loc, coll, kind='PLAIN_AXES', size=1.5, rot_z=0.0):
    e = bpy.data.objects.new(name, None); e.empty_display_type = kind; e.empty_display_size = size
    e.location = loc; e.rotation_euler = (0, 0, rot_z); col(coll).objects.link(e); return e

def attach(objs, parent):
    bpy.context.view_layer.update()
    for o in objs:
        mw = o.matrix_world.copy(); o.parent = parent; o.matrix_world = mw

def moving_linear(name, p0, p1, coll, face_to, size=(4.0, 3.0, 3.0), speed=4.0):
    """Slider : <name>_Carriage travels from the root position to <name>_End (ping-pong)."""
    root = empty(name, p0, coll, 'CUBE', 0.6)
    root["bf_move"] = "linear"; root["bf_speed"] = float(speed); root["bf_pingpong"] = 1
    car = empty(name + "_Carriage", p0, coll, 'ARROWS', 1.5)
    end = empty(name + "_End", p1, coll, 'SPHERE', 0.8)
    attach(target_at(name + "_Target", p0[0], p0[1], p0[2] + 0.3, coll, face_to, size), car)
    d = Vector(p1) - Vector(p0); mid = (Vector(p0) + Vector(p1)) / 2
    rail = box(name + "_Rail", (mid.x, mid.y, p0[2] + 0.15), (d.length + 2.0, 0.6, 0.3), "Dark", coll, rot_z=math.atan2(d.y, d.x))
    attach([car, end, rail], root); MOVERS.append(name); return root

def moving_rotor(name, center, coll, arm=5.0, n_disks=2, r=1.2, speed=30.0, base_z=0.0, post=True, arms=True):
    """Turntable / orbit : <name>_Rotor spins around the vertical axis (bf_speed in deg/s)."""
    cx, cy, cz = center
    root = empty(name, center, coll, 'CUBE', 0.6); root["bf_move"] = "rotate"; root["bf_speed"] = float(speed)
    rot = empty(name + "_Rotor", center, coll, 'ARROWS', 2.0)
    parts = []
    if post: attach([box(name + "_Post", (cx, cy, (base_z + cz) / 2), (0.5, 0.5, cz - base_z), "Dark", coll)], root)
    for i in range(n_disks):
        t = 2 * math.pi * i / n_disks; px, py = cx + arm * math.cos(t), cy + arm * math.sin(t)
        if arms:
            parts.append(box("%s_Arm%d" % (name, i + 1), (cx + arm / 2 * math.cos(t), cy + arm / 2 * math.sin(t), cz),
                             (arm, 0.25, 0.25), "Dark", coll, rot_z=t))
        parts.append(disk_target("%s_Disk%d" % (name, i + 1), px, py, cz, r, coll,
                                 (px - math.sin(t) * 10, py + math.cos(t) * 10)))
    attach(parts, rot); attach([rot], root); MOVERS.append(name); return root

def moving_popup(name, x, y, z0, coll, face_to, r=1.0, angle=90.0):
    """Pop-up : <name>_Hinge rotates by bf_angle around the axis Hinge -> <name>_HingeAxis (target lies flat when down)."""
    n = Vector((face_to[0] - x, face_to[1] - y, 0)).normalized(); a = math.atan2(n.x, -n.y)
    root = empty(name, (x, y, z0), coll, 'CUBE', 0.5, rot_z=a); root["bf_move"] = "popup"; root["bf_angle"] = float(angle)
    hinge = empty(name + "_Hinge", (x, y, z0 + 0.25), coll, 'ARROWS', 1.0, rot_z=a)
    axis = empty(name + "_HingeAxis", (x + math.cos(a), y + math.sin(a), z0 + 0.25), coll, 'SPHERE', 0.3)
    base = box(name + "_Base", (x, y, z0 + 0.125), (2 * r + 0.6, 0.8, 0.25), "Dark", coll, rot_z=a)
    d = disk_target(name + "_Disk", x, y, z0 + 0.4 + r, r, coll, face_to)
    arm = box(name + "_Arm", (x - n.x * 0.2, y - n.y * 0.2, z0 + 0.25 + (0.15 + r) / 2), (0.2, 0.15, 0.15 + r), "Dark", coll, rot_z=a)
    attach([d, arm], hinge); attach([hinge, axis, base], root); MOVERS.append(name); return root

def cyl(name, center, r, length, mat, coll, axis='z', seg=12, rot_z=0.0):
    R = Matrix.Rotation(rot_z, 3, 'Z')
    A = Matrix.Rotation(math.pi / 2, 3, 'Y') if axis == 'x' else Matrix.Identity(3)
    v = [Vector(center) + R @ (A @ Vector((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), s * length)))
         for s in (-0.5, 0.5) for i in range(seg)]
    f = [tuple(range(seg)), tuple(range(seg, 2 * seg))] + [(i, (i + 1) % seg, seg + (i + 1) % seg, seg + i) for i in range(seg)]
    ob = make_obj(name, v, f, mat, coll, origin=center, recalc=True)
    for p in ob.data.polygons: p.use_smooth = len(p.vertices) == 4
    return ob

def extrude_profile(name, prof, length, center, a, mat, coll):
    """Convex (y,z) profile extruded along the local X axis (length), rotated by a around Z."""
    n = len(prof); R = Matrix.Rotation(a, 3, 'Z'); c = Vector(center)
    v = [c + R @ Vector((sx * length / 2, y, z)) for sx in (-1, 1) for y, z in prof]
    f = [tuple(range(n)), tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    return make_obj(name, v, f, mat, coll, origin=center, recalc=True)

def ramp_local(name, center, a, x0, x1, w, zh, mat, coll):
    """Wedge in a local frame rotated by a : base on the ground, rising from x0 (0 m) to x1 (zh)."""
    R = Matrix.Rotation(a, 3, 'Z'); c = Vector(center)
    loc = [(x0, -w, 0), (x1, -w, 0), (x1, w, 0), (x0, w, 0), (x1, -w, zh), (x1, w, zh)]
    f = [(0, 3, 2, 1), (1, 2, 5, 4), (0, 4, 5, 3), (0, 1, 4), (3, 5, 2)]
    return make_obj(name, [c + R @ Vector(p) for p in loc], f, mat, coll, origin=center, recalc=True)

def slab_grid(name, F, holes, ztop, mat, coll, t=0.6):
    """One solid slab (top at ztop, thickness t) over rect F with rectangular openings (holes)."""
    fx0, fx1, fy0, fy1 = F
    xs = sorted({fx0, fx1} | {v for h in holes for v in (h[0], h[1]) if fx0 <= v <= fx1})
    ys = sorted({fy0, fy1} | {v for h in holes for v in (h[2], h[3]) if fy0 <= v <= fy1})
    nx, ny = len(xs), len(ys); off = nx * ny
    V = [(x, y, ztop) for y in ys for x in xs] + [(x, y, ztop - t) for y in ys for x in xs]
    faces = []; edges = {}
    for j in range(ny - 1):
        for i in range(nx - 1):
            cx_, cy_ = (xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2
            if any(h[0] < cx_ < h[1] and h[2] < cy_ < h[3] for h in holes): continue
            q = (j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i)
            faces.append(q); faces.append(tuple(off + k for k in reversed(q)))
            for e0, e1 in ((q[0], q[1]), (q[1], q[2]), (q[2], q[3]), (q[3], q[0])):
                edges.setdefault((min(e0, e1), max(e0, e1)), []).append((e0, e1))
    for lst in edges.values():
        if len(lst) == 1:
            e0, e1 = lst[0]; faces.append((e1, e0, off + e0, off + e1))
    ob = make_obj(name, V, faces, mat, coll, origin=((fx0 + fx1) / 2, (fy0 + fy1) / 2, ztop))
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS'); bm.to_mesh(ob.data); bm.free()
    return ob

def wedge(name, x0, x1, y0, y1, z_lo, z_hi, rise, mat, coll, zb=None):
    """Ramp on rect [x0,x1]x[y0,y1], top from z_lo to z_hi along rise ('+x','-x','+y','-y'), flat base at zb (default z_lo)."""
    zb = z_lo if zb is None else zb
    if rise in ('+x', '-x'):
        lo, hi = (x0, x1) if rise == '+x' else (x1, x0)
        prof = [(lo, zb), (hi, zb), (hi, z_hi)] + ([(lo, z_lo)] if z_lo - zb > 1e-6 else [])
        return prism_y(name, prof, y0, y1, mat, coll, origin=((x0 + x1) / 2, (y0 + y1) / 2, zb))
    lo, hi = (y0, y1) if rise == '+y' else (y1, y0)
    prof = [(lo, zb), (hi, zb), (hi, z_hi)] + ([(lo, z_lo)] if z_lo - zb > 1e-6 else [])
    return prism_x(name, prof, x0, x1, mat, coll, origin=((x0 + x1) / 2, (y0 + y1) / 2, zb))

def slab_with_hole(name, F, Hh, ztop, mat, coll, t=0.5):
    fx0, fx1, fy0, fy1 = F; hx0, hx1, hy0, hy1 = Hh
    for i, (a, b, c, d) in enumerate(((fx0, fx1, fy0, hy0), (fx0, fx1, hy1, fy1), (fx0, hx0, hy0, hy1), (hx1, fx1, hy0, hy1))):
        if b - a > 0.01 and d - c > 0.01:
            box("%s_%d" % (name, i), ((a + b) / 2, (c + d) / 2, ztop - t / 2), (b - a, d - c, t), mat, coll)

def _segments(a0, a1, gaps):
    segs = []; cur = a0
    for a, b in sorted(gaps):
        if a > cur: segs.append((cur, a))
        cur = max(cur, b)
    if cur < a1: segs.append((cur, a1))
    return [s for s in segs if s[1] - s[0] > 0.05]

def rail_x(name, x0, x1, y, z, coll, gaps=(), h=1.0, t=0.3, mat="Dark"):
    for i, (a, b) in enumerate(_segments(x0, x1, gaps)):
        box("%s_%d" % (name, i), ((a + b) / 2, y, z + h / 2), (b - a, t, h), mat, coll)

def rail_y(name, y0, y1, x, z, coll, gaps=(), h=1.0, t=0.3, mat="Dark"):
    for i, (a, b) in enumerate(_segments(y0, y1, gaps)):
        box("%s_%d" % (name, i), (x, (a + b) / 2, z + h / 2), (t, b - a, h), mat, coll)

STATS = {}
