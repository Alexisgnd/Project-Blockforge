
# ==== 05 : finalize (triangulate ngons, checks), preview, save .blend, export FBX ====
issues = []; tris = {}
for c in ROOT.children:
    t = 0
    for ob in c.objects:
        if ob.type != 'MESH': continue
        bm = bmesh.new(); bm.from_mesh(ob.data)
        ng = [f for f in bm.faces if len(f.verts) > 4]
        if ng: bmesh.ops.triangulate(bm, faces=ng, quad_method='BEAUTY', ngon_method='BEAUTY')
        bm.to_mesh(ob.data); bm.free()
        zs = [v.co.z for v in ob.data.vertices]
        if max(zs) - min(zs) < 1e-4:   # flat decal-like meshes must face up
            down = sum(1 for p in ob.data.polygons if (ob.matrix_world.to_3x3() @ p.normal).z < 0)
            if down: issues.append("%s: %d faces vers le bas" % (ob.name, down))
        t += sum(len(p.vertices) - 2 for p in ob.data.polygons)
    tris[c.name] = t
STATS["tris"] = tris; STATS["total_tris"] = sum(tris.values()); STATS["issues"] = issues
STATS["meshes"] = sum(1 for o in ROOT.all_objects if o.type == 'MESH')
STATS["movers"] = len(MOVERS)

# --- export FBX (meshes + SpawnPoint empty only) ---
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=False, object_types={'MESH', 'EMPTY'},
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False,
    path_mode='RELATIVE', embed_textures=False, use_custom_props=True)

# --- preview helpers (not exported) ---
pc = bpy.data.collections.new("99_Preview"); scene.collection.children.link(pc)
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); sun.data.energy = 3.0
sun.rotation_euler = (math.radians(50), 0, math.radians(35)); pc.objects.link(sun)
cam = bpy.data.objects.new("Cam_Overview", bpy.data.cameras.new("Cam_Overview")); pc.objects.link(cam)
cam.location = (430, -520, 420); cam.rotation_euler = (math.radians(55), 0, math.radians(40))
cam.data.lens = 28; cam.data.clip_end = 5000; scene.camera = cam
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'TEXTURE'
scene.display.shading.show_shadows = True; scene.display.shading.show_cavity = True
scene.render.resolution_x = 1920; scene.render.resolution_y = 1080; scene.render.resolution_percentage = 100
scene.render.filepath = os.path.join(SRC_DIR, "Preview_Overview.png"); scene.render.image_settings.file_format = 'PNG'
for b in COLS["09_Bounds_Invisible"].objects: b.hide_render = True
bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, relative_remap=True)
