# =========================================================
# PIPELINE AILERONS (RUDDERS) : GLB -> Blender -> FBX Unity + icones
# ---------------------------------------------------------
# A executer dans Blender (Scripting > Run Script, ou via le MCP).
# - Importe les 7 GLB du pack BlockForge_Rudders_Pack
# - Une collection par aileron, racine Empty nommee comme le FBX,
#   pieces renommees en snake_case, materiaux fusionnes / renommes
#   (rudder_white, rudder_graphite, rudder_steel, rudder_silver_edge,
#    rudder_cyan, rudder_gold)
# - Exporte chaque aileron en FBX dans Art/Models/Blocks/Movement
# - Rend une icone 512x512 fond transparent par aileron dans
#   Art/Textures/Icons/Icon_<nom>.png (rig camera + lumieres "IconRig")
# - Sauvegarde le .blend source dans Tools/Blender/Rudders.blend
# =========================================================

import bpy, os, re, math, mathutils

PACK = r"C:\Users\Alexis\Downloads\BlockForge_Rudders_Pack"
PROJECT = r"D:\unity projects\Project-Blockforge"
FBX_DIR = os.path.join(PROJECT, r"Assets\_Project\Art\Models\Blocks\Movement")
ICON_DIR = os.path.join(PROJECT, r"Assets\_Project\Art\Textures\Icons")
BLEND_PATH = os.path.join(PROJECT, r"Tools\Blender\Rudders.blend")

# Tiers N1 -> N7 : ordre du README du pack (taille croissante)
RUDDERS = [
    ("Rudder_Hawk.glb",        "Rudder_N1_Hawk"),
    ("Rudder_Falcon.glb",      "Rudder_N2_Falcon"),
    ("Rudder_Kestrel.glb",     "Rudder_N3_Kestrel"),
    ("Rudder_Eagle.glb",       "Rudder_N4_Eagle"),
    ("Vampire_Bat_Rudder.glb", "Rudder_N5_VampireBat"),
    ("Rudder_Albatross.glb",   "Rudder_N6_Albatross"),
    ("Bat_Rudder.glb",         "Rudder_N7_Bat"),
]

# Nom de materiau dans le GLB -> nom partage dans le FBX (remappe cote Unity
# par RudderMaterialSetup.cs vers Art/Materials/Blocks/Rudder_*.mat)
MAT_RENAME = {
    "white":  "rudder_white",
    "dark":   "rudder_graphite",
    "steel":  "rudder_steel",
    "edge":   "rudder_silver_edge",
    "cyan":   "rudder_cyan",
    "yellow": "rudder_gold",
}

ICON_SIZE = 512
ICON_MARGIN = 1.10          # marge autour de l'objet dans le cadre
# Direction centre -> camera (Blender, Z haut). Le plan de l'aileron est XZ
# (epaisseur en Y) : on regarde la face laterale, un peu de dessus/devant.
CAM_DIR = mathutils.Vector((0.62, -1.0, 0.52)).normalized()
CAM_LENS = 50.0

DO_EXPORT = True
DO_RENDER = True
DO_SAVE = True


def sanitize(name):
    s = re.sub(r"\.\d{3}$", "", name)
    s = s.lower().replace("-1", "l").replace(" 1", " r")
    s = re.sub(r"[^a-z0-9]+", "_", s).strip("_")
    return s or "piece"


def unique_name(base, used):
    n, i = base, 2
    while n in used:
        n = f"{base}_{i}"
        i += 1
    used.add(n)
    return n


def link_only(obj, coll):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)


def get_shared_material(src):
    shared = set(MAT_RENAME.values())
    if src.name in shared:          # deja le materiau partage
        return src
    base = src.name.split(".")[0]
    if base in shared:              # doublon (.001) d'un materiau partage
        return bpy.data.materials[base]
    target = MAT_RENAME.get(base, "rudder_" + base)
    mat = bpy.data.materials.get(target)
    if mat is None:
        src.name = target
        return src
    return mat


def build_rudder(glb, name):
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)

    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(PACK, glb))
    new = [o for o in bpy.data.objects if o not in before]

    root = bpy.data.objects.new(name, None)
    root.empty_display_type = 'PLAIN_AXES'
    root.empty_display_size = 0.5
    coll.objects.link(root)

    used = set()
    for o in sorted(new, key=lambda o: o.name):
        link_only(o, coll)
        if o.parent is None:
            o.parent = root
        o.name = unique_name(sanitize(o.name), used)
        if o.type == 'MESH':
            o.data.name = o.name
            for slot in o.material_slots:
                if slot.material:
                    slot.material = get_shared_material(slot.material)
    # supprime les doublons de materiaux devenus orphelins
    for m in list(bpy.data.materials):
        if m.users == 0:
            bpy.data.materials.remove(m)
    return coll, root


def collection_bounds(coll):
    pts = []
    for o in coll.all_objects:
        if o.type == 'MESH':
            pts += [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
    lo = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi, pts


def export_fbx(coll, root):
    os.makedirs(FBX_DIR, exist_ok=True)
    objs = [root] + [o for o in coll.all_objects if o != root]
    for o in bpy.data.objects:
        o.select_set(o in objs)
    bpy.context.view_layer.objects.active = root
    path = os.path.join(FBX_DIR, root.name + ".fbx")
    # temp_override : le contexte MCP / script n'expose pas selected_objects
    win = bpy.context.window_manager.windows[0] if bpy.context.window_manager.windows else None
    override = dict(selected_objects=objs, active_object=root, object=root)
    if win:
        override.update(window=win, screen=win.screen)
    with bpy.context.temp_override(**override):
        _export_fbx_to(path)
    bpy.ops.object.select_all(action='DESELECT')
    return path


def _export_fbx_to(path):
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',   # UnitScaleFactor=100, sommets en metres (comme les FBX existants)
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,   # "Apply Transform" : racines a rotation nulle dans Unity (comme les FBX existants)
        use_mesh_modifiers=True,
        mesh_smooth_type='OFF',
        use_tspace=False,
        use_custom_props=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode='AUTO',
        embed_textures=False,
    )


def ensure_icon_rig():
    scene = bpy.context.scene
    rig = bpy.data.collections.get("IconRig")
    if rig is None:
        rig = bpy.data.collections.new("IconRig")
        scene.collection.children.link(rig)

    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.lens = CAM_LENS
    cam_data.sensor_fit = 'AUTO'
    cam_data.clip_start = 0.01
    cam_data.clip_end = 100
    cam = bpy.data.objects.new("IconCam", cam_data)
    rig.objects.link(cam)
    scene.camera = cam

    def add_light(name, kind, energy, color=(1, 1, 1), size=None):
        ld = bpy.data.lights.new(name, kind)
        ld.energy = energy
        ld.color = color
        if size is not None:
            if kind == 'AREA':
                ld.size = size
            elif kind == 'SUN':
                ld.angle = size
        lo = bpy.data.objects.new(name, ld)
        rig.objects.link(lo)
        return lo

    key = add_light("IconKey", 'SUN', 3.2, (1.0, 0.98, 0.95), math.radians(12))
    fill = add_light("IconFill", 'AREA', 250, (0.85, 0.92, 1.0), 6.0)
    rim = add_light("IconRim", 'AREA', 320, (0.9, 0.97, 1.0), 4.0)

    # Monde : ambiance gris clair (comme les icones existantes)
    world = scene.world or bpy.data.worlds.new("IconWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.82, 0.83, 0.85, 1)
        bg.inputs[1].default_value = 0.5

    # Rendu
    for engine in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE'):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    scene.render.resolution_x = ICON_SIZE
    scene.render.resolution_y = ICON_SIZE
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '8'
    scene.render.image_settings.compression = 50
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = 64
        for attr, val in (("use_shadows", True), ("use_raytracing", False)):
            if hasattr(scene.eevee, attr):
                setattr(scene.eevee, attr, val)
    return rig, cam, key, fill, rim


def look_at_rotation(direction):
    # direction = vecteur camera -> cible
    return direction.to_track_quat('-Z', 'Y').to_euler()


def frame_camera(cam, pts):
    """Place la camera le long de CAM_DIR pour cadrer tous les points."""
    center = sum(pts, mathutils.Vector()) / len(pts)
    rot = look_at_rotation(-CAM_DIR)
    R = rot.to_matrix()
    Rinv = R.transposed()
    half = math.atan((cam.data.sensor_width / 2) / cam.data.lens)  # sensor_fit AUTO, image carree
    t = math.tan(half) / ICON_MARGIN

    shift = mathutils.Vector((0, 0))
    for _ in range(3):
        loc = [Rinv @ (p - center) for p in pts]
        D = max((q.z + max(abs(q.x - shift.x), abs(q.y - shift.y)) / t) for q in loc)
        xs = [(q.x - shift.x) / (D - q.z) for q in loc]
        ys = [(q.y - shift.y) / (D - q.z) for q in loc]
        shift.x += (max(xs) + min(xs)) / 2 * D
        shift.y += (max(ys) + min(ys)) / 2 * D
    cam.location = center + R @ mathutils.Vector((shift.x, shift.y, D))
    cam.rotation_euler = rot
    return center, D


def place_lights(key, fill, rim, center, D):
    # Cle : haut / avant-gauche par rapport a la camera
    R = look_at_rotation(-CAM_DIR).to_matrix()
    key_dir = (R @ mathutils.Vector((-0.6, 0.9, 0.7))).normalized()
    key.location = center + key_dir * D
    key.rotation_euler = look_at_rotation(-key_dir)
    fill_dir = (R @ mathutils.Vector((0.9, 0.2, 0.8))).normalized()
    fill.location = center + fill_dir * D * 0.9
    fill.rotation_euler = look_at_rotation(-fill_dir)
    rim_dir = (R @ mathutils.Vector((0.3, 0.8, -0.9))).normalized()
    rim.location = center + rim_dir * D * 0.8
    rim.rotation_euler = look_at_rotation(-rim_dir)


def render_icon(name, coll, all_colls, cam, key, fill, rim):
    os.makedirs(ICON_DIR, exist_ok=True)
    for c in all_colls:
        c.hide_render = (c != coll)
    _, _, pts = collection_bounds(coll)
    center, D = frame_camera(cam, pts)
    place_lights(key, fill, rim, center, D)
    path = os.path.join(ICON_DIR, f"Icon_{name}.png")
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def run():
    bpy.ops.wm.read_homefile(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0

    built = []
    for glb, name in RUDDERS:
        coll, root = build_rudder(glb, name)
        built.append((name, coll, root))

    rig, cam, key, fill, rim = ensure_icon_rig()
    report = {}
    colls = [c for _, c, _ in built]
    for name, coll, root in built:
        lo, hi, _ = collection_bounds(coll)
        entry = {
            "pieces": len([o for o in coll.all_objects if o.type == 'MESH']),
            "size": [round(hi.x - lo.x, 2), round(hi.y - lo.y, 2), round(hi.z - lo.z, 2)],
            "mats": sorted({s.material.name for o in coll.all_objects if o.type == 'MESH'
                            for s in o.material_slots if s.material}),
        }
        if DO_EXPORT:
            entry["fbx"] = os.path.getsize(export_fbx(coll, root))
        if DO_RENDER:
            entry["icon"] = os.path.getsize(render_icon(name, coll, colls, cam, key, fill, rim))
        report[name] = entry

    for c in colls:
        c.hide_render = False
    if DO_SAVE:
        os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, compress=True)
    report["_blender"] = ".".join(map(str, bpy.app.version))
    report["_engine"] = scene.render.engine
    return report


result = run()
