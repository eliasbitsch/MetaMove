# Headless Blender: industrial assets for the Bari kitting cell.
# blender -b --factory-startup --python bari_assets.py -- <out_dir> <preview_png>
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix

# Runs inside the live Blender GUI (sent over the MCP socket), so nothing here may
# reset the file or render: that would unload the addon or block the socket.
OUT = "C:/git/MetaMove/unity-quest/Assets/MetaMove/Models/Bari"
TEX = "C:/git/MetaMove/unity-quest/Assets/MetaMove/Textures/Bari"
os.makedirs(OUT, exist_ok=True)

def reset():
    pass   # everything stays in the scene; each asset is exported by selection

def clear_all():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)

TEXMAP = {"GearSteel": "Metal051A", "GearAnodized": "Metal051A", "Profile": "Metal009", "KLT": "Plastic015A",
          "TowerBody": "Metal027", "KitShell": "Metal027", "ESDTop": "Rubber004", "Rubber": "Rubber004"}

def mat(name, rgb, metal=0.0, rough=0.5, emit=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes.get("Principled BSDF")
    tid = TEXMAP.get(name)
    if tid and not nt.nodes.get("TexColor"):
        tc = nt.nodes.new("ShaderNodeTexImage"); tc.name = "TexColor"
        tc.image = bpy.data.images.load(TEX + "/" + tid + "/" + tid + "_Color.jpg", check_existing=True)
        mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1.0
        mix.inputs[6].default_value = (*rgb, 1)
        nt.links.new(tc.outputs["Color"], mix.inputs[7]); nt.links.new(mix.outputs[2], b.inputs["Base Color"])
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.name = "TexNormal"
        tn.image = bpy.data.images.load(TEX + "/" + tid + "/" + tid + "_NormalGL.jpg", check_existing=True)
        tn.image.colorspace_settings.name = "Non-Color"
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    if not tid:
        b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = 3.0
    return m

def obj_from_bm(bm, name, material=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if material: ob.data.materials.append(material)
    return ob

def extrude_outline(pts, h, name, material=None, z0=0.0, holes=()):
    """Closed 2D outline (list of (x,y)) extruded along Z from z0 to z0+h."""
    bm = bmesh.new()
    vb = [bm.verts.new((x, y, z0)) for x, y in pts]
    face = bm.faces.new(vb)
    r = bmesh.ops.extrude_face_region(bm, geom=[face])
    top = [e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, verts=top, vec=(0, 0, h))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = obj_from_bm(bm, name, material)
    for (hx, hy, hr) in holes:
        cut(ob, cyl(hr, h + 0.02, (hx, hy, z0 - 0.01), "tmp"))
    return ob

def cyl(r, h, loc, name, material=None, seg=48):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, vertices=seg, location=(loc[0], loc[1], loc[2] + h / 2))
    ob = bpy.context.active_object; ob.name = name
    if material: ob.data.materials.append(material)
    return ob

def box(size, loc, name, material=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    ob = bpy.context.active_object; ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if material: ob.data.materials.append(material)
    return ob

def cut(target, tool):
    m = target.modifiers.new("cut", "BOOLEAN"); m.operation = "DIFFERENCE"; m.object = tool; m.solver = "EXACT"
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(tool, do_unlink=True)

def unite(target, tool):
    m = target.modifiers.new("uni", "BOOLEAN"); m.operation = "UNION"; m.object = tool; m.solver = "EXACT"
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(tool, do_unlink=True)

def join(obs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    ob = bpy.context.active_object; ob.name = name
    return ob

def bevel(ob, w=0.001, seg=2):
    m = ob.modifiers.new("bev", "BEVEL"); m.width = w; m.segments = seg; m.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=m.name)

def smooth(ob, angle=35):
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
    except Exception:
        bpy.ops.object.shade_smooth()

def box_uv(ob, scale=4.0, radial=None):
    """Box-projection UVs in object space. radial=D maps +/-Z faces to [0,1] across a
    diameter D, so a circular-brushed texture sits centred on a gear."""
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            c = l.vert.co
            if ax == 2 and radial: l[uv].uv = (c.x / radial + 0.5, c.y / radial + 0.5)
            elif ax == 0: l[uv].uv = (c.y * scale, c.z * scale)
            elif ax == 1: l[uv].uv = (c.x * scale, c.z * scale)
            else: l[uv].uv = (c.x * scale, c.y * scale)
    bm.to_mesh(me); bm.free()

def export(name, obs):
    for o in obs:
        if o.type == "MESH": box_uv(o, radial=0.07 if name.startswith("bari_gear") else None)
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False)

# ------------------------------------------------------------------ parts

def gear_outline(z, module, tooth_h_frac=1.0):
    """Simplified involute-ish spur gear outline, trapezoid teeth."""
    rp = module * z / 2; ra = rp + module; rf = rp - 1.25 * module
    pts = []
    for i in range(z):
        a0 = 2 * math.pi * i / z; da = 2 * math.pi / z
        for f, r in ((0.00, rf), (0.10, rf), (0.30, ra), (0.50, ra), (0.70, rf), (0.99, rf)):
            a = a0 + f * da
            pts.append((r * math.cos(a), r * math.sin(a)))
    return pts, ra, rf

def make_gear(name, z, module, web_h, hub_r, hub_h, bore_r, helical=0.0, spokes=0, holes=0, material=None):
    pts, ra, rf = gear_outline(z, module)
    rim = extrude_outline(pts, web_h, name + "_rim", material)
    if helical:
        tw = rim.modifiers.new("tw", "SIMPLE_DEFORM"); tw.deform_method = "TWIST"; tw.angle = math.radians(helical); tw.deform_axis = "Z"
        bpy.context.view_layer.objects.active = rim; bpy.ops.object.modifier_apply(modifier=tw.name)
    # recess the web between hub and rim for a machined look
    recess_r_in, recess_r_out = hub_r + 0.003, rf - 0.004
    ring = cyl(recess_r_out, 0.004, (0, 0, web_h - 0.003), "tmp", seg=48)
    cut(ring, cyl(recess_r_in, 0.01, (0, 0, web_h - 0.006), "tmp2", seg=48))
    cut(rim, ring)
    ring2 = cyl(recess_r_out, 0.004, (0, 0, -0.001), "tmp", seg=48)
    cut(ring2, cyl(recess_r_in, 0.01, (0, 0, -0.004), "tmp2", seg=48))
    cut(rim, ring2)
    rm = (hub_r + rf) / 2
    for i in range(holes):
        a = 2 * math.pi * i / holes
        cut(rim, cyl((rf - hub_r) * 0.28, web_h + 0.02, (rm * math.cos(a), rm * math.sin(a), -0.01), "tmp", seg=16))
    for i in range(spokes):  # spoked web: remove sectors between spokes
        a = 2 * math.pi * (i + 0.5) / spokes
        cut(rim, cyl((rf - hub_r) * 0.36, web_h + 0.02, (rm * math.cos(a), rm * math.sin(a), -0.01), "tmp", seg=16))
    hub = cyl(hub_r, hub_h, (0, 0, 0), name + "_hub", material, seg=40)
    unite(rim, hub)          # a real union: joined overlapping volumes break the bore cut
    g = rim; g.name = name
    cut(g, cyl(bore_r, hub_h + 0.02, (0, 0, -0.01), "tmp", seg=24))
    # keyway
    cut(g, box((0.006, bore_r + 0.003, hub_h + 0.02), (0, bore_r * 0.6, hub_h / 2), "tmp"))
    smooth(g)          # no bevel: it tripled the triangle count for a detail nobody sees at 1 m
    return g

clear_all()
steel = mat("GearSteel", (0.62, 0.63, 0.65), metal=1.0, rough=0.28)
ga = make_gear("GearA", z=22, module=0.0025, web_h=0.012, hub_r=0.015, hub_h=0.040, bore_r=0.008, holes=6, material=steel)
export("bari_gear_a", [ga])

reset()
anod = mat("GearAnodized", (0.05, 0.32, 0.55), metal=0.9, rough=0.35)
gb = make_gear("GearB", z=17, module=0.003, web_h=0.014, hub_r=0.015, hub_h=0.040, bore_r=0.008, helical=18, spokes=5, material=anod)
export("bari_gear_b", [gb])

# ------------------------------------------------------------------ KLT box (outer 0.32 x 0.24 x 0.075)

reset()
blue = mat("KLT", (0.05, 0.22, 0.55), rough=0.55)
W, D, H, t = 0.32, 0.24, 0.075, 0.004
outer = box((W, D, H), (0, 0, H / 2), "KLT")
inner = box((W - 2 * t, D - 2 * t, H), (0, 0, H / 2 + 0.004), "tmp")
cut(outer, inner)
# ribs on the outside walls
ribs = [outer]
for i in range(7):
    x = -W / 2 + W * (i + 0.5) / 7
    ribs.append(box((0.004, 0.006, H * 0.9), (x, D / 2 + 0.002, H * 0.47), "r"))
    ribs.append(box((0.004, 0.006, H * 0.9), (x, -D / 2 - 0.002, H * 0.47), "r"))
klt = join(ribs, "KLT")
for s in (1, -1):  # grip slots on the short sides
    cut(klt, box((0.02, 0.10, 0.022), (s * W / 2, 0, H - 0.02), "tmp"))
bevel(klt, 0.0015, 2)
klt.data.materials.clear(); klt.data.materials.append(blue)
export("bari_klt", [klt])

# ------------------------------------------------------------------ kit tray with foam insert, 6 pockets

reset()
dark = mat("KitShell", (0.08, 0.085, 0.09), rough=0.6)
foam = mat("KitFoam", (0.02, 0.02, 0.022), rough=0.95)
shell = box((0.28, 0.20, 0.035), (0, 0, 0.0175), "KitShell", dark)
cut(shell, box((0.268, 0.188, 0.04), (0, 0, 0.022), "tmp"))
ins = box((0.268, 0.188, 0.028), (0, 0, 0.014), "KitFoam", foam)
for ix in range(3):
    for iz in range(2):
        cut(ins, cyl(0.032, 0.03, ((ix - 1) * 0.075, (iz - 0.5) * 0.075, 0.006), "tmp", seg=48))
export("bari_kit", [shell, ins])

# ------------------------------------------------------------------ aluminium profile table

reset()
alu = mat("Profile", (0.78, 0.8, 0.82), metal=0.9, rough=0.35)
esd = mat("ESDTop", (0.16, 0.24, 0.22), rough=0.8)
def profile_section(s=0.04, slot=0.008, depth=0.006):
    h = s / 2; sw = slot / 2
    pts = []
    # square with a slot in the middle of every side, going counter-clockwise
    for (cx, cy, nx, ny) in ((0, -h, 0, 1), (h, 0, -1, 0), (0, h, 0, -1), (-h, 0, 1, 0)):
        tx, ty = -ny, nx  # tangent
        corner = (cx - tx * h, cy - ty * h)
        pts.append(corner)
        pts.append((cx - tx * sw, cy - ty * sw))
        pts.append((cx - tx * sw + nx * depth, cy - ty * sw + ny * depth))
        pts.append((cx + tx * sw + nx * depth, cy + ty * sw + ny * depth))
        pts.append((cx + tx * sw, cy + ty * sw))
    return pts
def profile(length, loc, axis, name):
    ob = extrude_outline(profile_section(), length, name, alu, z0=-length / 2)
    if axis == "x": ob.rotation_euler = (0, math.pi / 2, 0)
    if axis == "y": ob.rotation_euler = (math.pi / 2, 0, 0)
    ob.location = loc
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.transform_apply(rotation=True, location=False)
    return ob
TW, TD, TH = 1.30, 0.95, 0.80   # top size, table height to top surface
parts = []
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = sx * (TW / 2 - 0.05), sy * (TD / 2 - 0.05)
        parts.append(profile(TH - 0.06, (x, y, (TH - 0.06) / 2 + 0.01), "z", "Leg"))
        parts.append(box((0.08, 0.08, 0.008), (x, y, 0.004), "Foot", alu))
for z in (0.12, TH - 0.04):
    for sy in (-1, 1):
        parts.append(profile(TW - 0.14, (0, sy * (TD / 2 - 0.05), z), "x", "Beam"))
    for sx in (-1, 1):
        parts.append(profile(TD - 0.14, (sx * (TW / 2 - 0.05), 0, z), "y", "Beam"))
frame = join(parts, "TableFrame")
for m in list(frame.data.materials): pass
top = box((TW, TD, 0.02), (0, 0, TH - 0.01), "TableTop", esd)
bevel(top, 0.002, 2)
export("bari_table", [frame, top])

# ------------------------------------------------------------------ signal tower (andon)

reset()
black = mat("TowerBody", (0.03, 0.03, 0.035), rough=0.4)
lens_g = mat("LensGreen", (0.1, 0.8, 0.25), rough=0.2)
lens_a = mat("LensAmber", (1.0, 0.55, 0.05), rough=0.2)
lens_r = mat("LensRed", (0.9, 0.08, 0.06), rough=0.2)
base = cyl(0.035, 0.03, (0, 0, 0), "TowerBase", black)
pole = cyl(0.011, 0.30, (0, 0, 0.03), "TowerPole", mat("Chrome", (0.8, 0.8, 0.82), metal=1, rough=0.15))
segs = []
z = 0.33
for n, m in (("LampGreen", lens_g), ("LampAmber", lens_a), ("LampRed", lens_r)):
    s = cyl(0.03, 0.055, (0, 0, z), n, m); segs.append(s)
    ring = cyl(0.031, 0.006, (0, 0, z + 0.055), n + "Ring", black); segs.append(ring)
    z += 0.061
cap = cyl(0.03, 0.02, (0, 0, z), "TowerCap", black)
export("bari_signal_tower", [base, pole, cap] + segs)

# ------------------------------------------------------------------ trolley for tray B

reset()
alu = mat("Profile", (0.78, 0.8, 0.82), metal=0.9, rough=0.35)
esd = mat("ESDTop", (0.16, 0.24, 0.22), rough=0.8)
rubber = mat("Rubber", (0.03, 0.03, 0.03), rough=0.9)
p = []
CW, CD, CH = 0.50, 0.42, 0.80
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = sx * (CW / 2 - 0.03), sy * (CD / 2 - 0.03)
        p.append(profile(CH - 0.12, (x, y, 0.10 + (CH - 0.12) / 2), "z", "Post"))
        w = cyl(0.04, 0.025, (x, y, 0.0), "Wheel", rubber); w.rotation_euler = (math.pi / 2, 0, 0); w.location = (x, y, 0.04)
        bpy.context.view_layer.objects.active = w; w.select_set(True); bpy.ops.object.transform_apply(rotation=True)
        p.append(w)
for z in (0.12, CH - 0.03):
    for sy in (-1, 1): p.append(profile(CW - 0.10, (0, sy * (CD / 2 - 0.03), z), "x", "Rail"))
    for sx in (-1, 1): p.append(profile(CD - 0.10, (sx * (CW / 2 - 0.03), 0, z), "y", "Rail"))
frame_parts = [o for o in p if o.name.startswith(("Post", "Rail"))]
wheel_parts = [o for o in p if o.name.startswith("Wheel")]
fr = join(frame_parts, "TrolleyFrame")
wh = join(wheel_parts, "TrolleyWheels")
shelf = box((CW, CD, 0.015), (0, 0, CH - 0.0075), "TrolleyTop", esd)
shelf2 = box((CW - 0.04, CD - 0.04, 0.012), (0, 0, 0.13), "TrolleyShelf", esd)
export("bari_trolley", [fr, wh, shelf, shelf2])


# ------------------------------------------------------------------ lay out for viewing
lay = {"GearA": (0.22, 0.05, 0.805), "GearB": (0.36, 0.14, 0.805), "KLT": (0.3, 0.1, 0.80),
       "KitShell": (-0.3, 0.1, 0.80), "KitFoam": (-0.3, 0.1, 0.80)}
for o in bpy.data.objects:
    if o.name in lay: o.location = Vector(lay[o.name]) + o.location
for o in bpy.data.objects:
    if o.name.startswith(("Tower", "Lamp")): o.location = Vector((-0.6, 0.42, 0.80)) + o.location
    if o.name.startswith("Trolley"): o.location = Vector((1.1, 0.4, 0)) + o.location
for a in bpy.context.screen.areas:
    if a.type == "VIEW_3D":
        for sp in a.spaces:
            if sp.type == "VIEW_3D": sp.shading.type = "MATERIAL"
        with bpy.context.temp_override(area=a, region=[r for r in a.regions if r.type == "WINDOW"][0]):
            bpy.ops.view3d.view_all()
result = sorted(os.listdir(OUT))
