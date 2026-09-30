# Factory cut-out for the Bari cell, built in the live Blender (helpers prepended).
# Coordinates are written in the Unity cell frame measured from the FLOOR:
#   X = cell right, Y = up (0 = floor), Z = towards the visitor.
# U() converts to Blender, so the FBX lands in Unity exactly in these numbers
# (Blender FBX -> Unity is (x,y,z) -> (-x, z, -y)).
import math, random
random.seed(7)

def U(x, y, z):
    return (-x, -z, y)

for o in [o for o in bpy.data.objects if o.name.startswith(("Fac_",))]:
    bpy.data.objects.remove(o, do_unlink=True)

epoxy = mat("FacEpoxy", (0.10, 0.36, 0.20), rough=0.35)
concrete = mat("FacConcrete", (0.55, 0.55, 0.53), rough=0.9)
yellow = mat("FacYellow", (0.95, 0.72, 0.05), rough=0.5)
hazard = mat("FacHazard", (1, 1, 1), rough=0.5)
steel = mat("FacSteel", (0.35, 0.37, 0.40), metal=0.8, rough=0.4)
rebar = mat("FacRebar", (0.30, 0.18, 0.10), metal=0.6, rough=0.8)
paint = mat("FacWallPaint", (0.16, 0.30, 0.55), rough=0.7)
lampglow = mat("FacLampGlow", (1.0, 0.92, 0.75), rough=0.3, emit=(1.0, 0.92, 0.75))
roller = mat("Roller", (0.12, 0.12, 0.13), metal=0.2, rough=0.5)

def poly_prism(pts_xz, y0, y1, name, top_mat, side_mat):
    """Closed outline in the cell XZ plane, extruded from y0 to y1 (floor-relative)."""
    bm = bmesh.new()
    bot = [bm.verts.new(U(x, y0, z)) for x, z in pts_xz]
    top = [bm.verts.new(U(x, y1, z)) for x, z in pts_xz]
    n = len(pts_xz)
    ft = bm.faces.new(top); fb = bm.faces.new(list(reversed(bot)))
    sides = [bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i])) for i in range(n)]
    # concave, jagged caps: triangulate here (ear clipping) - Unity's ngon import drops them
    bmesh.ops.triangulate(bm, faces=[ft, fb], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = obj_from_bm(bm, name)
    ob.data.materials.append(top_mat); ob.data.materials.append(side_mat)
    for p in ob.data.polygons:
        p.material_index = 0 if p.normal.z > 0.5 else 1
    return ob

def jagged(rect, step=0.11, bite=0.10):
    """Rectangle (x0, z0, x1, z1) with a broken, irregular edge."""
    x0, z0, x1, z1 = rect
    pts = []
    def edge(ax, az, bx, bz, nx, nz):
        L = math.hypot(bx - ax, bz - az); k = max(2, int(L / step))
        for i in range(k):
            t = i / k
            o = random.uniform(-bite, bite * 0.4)
            if random.random() < 0.12: o -= random.uniform(0.08, 0.22)   # a bigger chunk broken off
            pts.append((ax + (bx - ax) * t + nx * o, az + (bz - az) * t + nz * o))
    edge(x0, z0, x1, z0, 0, -1); edge(x1, z0, x1, z1, 1, 0)
    edge(x1, z1, x0, z1, 0, 1); edge(x0, z1, x0, z0, -1, 0)
    return pts

# ---------------------------------------------------------------- floor island
ISLAND = (-1.75, -1.55, 1.75, 3.15)
outline = jagged(ISLAND)
isle = poly_prism(outline, -0.16, 0.012, "Fac_Floor", epoxy, concrete)
# a few rebar stubs sticking out of the broken edge
bars = []
for (x, z) in random.sample(outline, 14):
    L = random.uniform(0.08, 0.2)
    ang = math.atan2(z - (ISLAND[1] + ISLAND[3]) / 2, x - (ISLAND[0] + ISLAND[2]) / 2)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.006, depth=L, vertices=6,
        location=U(x + math.cos(ang) * L / 2, -0.07, z + math.sin(ang) * L / 2),
        rotation=(math.pi / 2, 0, -ang - math.pi / 2))
    b = bpy.context.active_object; b.data.materials.append(rebar); bars.append(b)
rebars = join(bars, "Fac_Rebar")

# markings: yellow walkway lines, hazard tape around the robot table
def strip(x0, z0, x1, z1, w, name, m):
    L = math.hypot(x1 - x0, z1 - z0); ang = math.atan2(z1 - z0, x1 - x0)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    s = box((L, w, 0.003), U(cx, 0.0135, cz), name, m)
    s.rotation_euler = (0, 0, -ang); return s
lines = [strip(-1.6, 1.30, 1.6, 1.30, 0.06, "l", yellow), strip(-1.6, 2.05, 1.6, 2.05, 0.06, "l", yellow),
         strip(-1.6, -1.05, 1.6, -1.05, 0.06, "l", yellow)]
tape = [strip(-0.78, -0.42, 0.48, -0.42, 0.075, "t", hazard), strip(0.48, -0.42, 0.48, 0.30, 0.075, "t", hazard),
        strip(-0.78, -0.42, -0.78, 0.30, 0.075, "t", hazard)]
for o in lines + tape:
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
fl_lines = join(lines, "Fac_FloorLines"); fl_tape = join(tape, "Fac_HazardTape")
# tape UVs along its length so the stripes repeat
for f in fl_tape.data.polygons: pass
export("bari_floor_island", [isle, rebars, fl_lines, fl_tape])

# ---------------------------------------------------------------- back wall with a breach
WZ, WT = -1.40, 0.22                   # wall plane z (cell), thickness
wall_pts = []                          # outline in the wall's XY plane (x, y), broken top edge
xs = [-1.7 + i * 0.12 for i in range(int(3.4 / 0.12) + 1)]
wall_pts.append((-1.7, 0.0)); wall_pts.append((1.7, 0.0))
for x in reversed(xs):
    wall_pts.append((x, 2.45 + random.uniform(-0.25, 0.08) + (0.35 if abs(x - 0.9) < 0.5 else 0)))
bm = bmesh.new()
front = [bm.verts.new(U(x, y, WZ + WT / 2)) for x, y in wall_pts]
back = [bm.verts.new(U(x, y, WZ - WT / 2)) for x, y in wall_pts]
n = len(wall_pts)
bm.faces.new(front); bm.faces.new(list(reversed(back)))
for i in range(n): bm.faces.new((front[i], back[i], back[(i + 1) % n], front[(i + 1) % n]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
wall = obj_from_bm(bm, "Fac_Wall"); wall.data.materials.append(concrete)
# breach: jagged hole where the conveyor leaves the cell
hole_pts = []
cx, cy, rw, rh = -0.45, 1.05, 0.62, 0.78
for i in range(28):
    a = 2 * math.pi * i / 28
    r = 1 + random.uniform(-0.18, 0.12)
    hole_pts.append((cx + math.cos(a) * rw * r, max(0.55, cy + math.sin(a) * rh * r)))
hb = bmesh.new()
hf = [hb.verts.new(U(x, y, WZ + WT)) for x, y in hole_pts]
hk = [hb.verts.new(U(x, y, WZ - WT)) for x, y in hole_pts]
hb.faces.new(hf); hb.faces.new(list(reversed(hk)))
for i in range(len(hole_pts)): hb.faces.new((hf[i], hk[i], hk[(i + 1) % len(hole_pts)], hf[(i + 1) % len(hole_pts)]))
bmesh.ops.recalc_face_normals(hb, faces=hb.faces)
cutter = obj_from_bm(hb, "cutter")
cut(wall, cutter)
# triangulate AFTER the boolean: its concave ngon faces are what Unity's import drops
tb = bmesh.new(); tb.from_mesh(wall.data)
bmesh.ops.triangulate(tb, faces=[f for f in tb.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="EAR_CLIP")
tb.to_mesh(wall.data); tb.free()
# painted lower band on the room side (typical hall wall)
band = box((3.4, 0.004, 1.2), U(0, 0.6, WZ + WT / 2 + 0.003), "Fac_WallBand", paint)
cut(band, box((1.5, 0.05, 1.8), U(cx, cy, WZ + WT / 2), "tmp"))   # keep the breach clear of paint
wbars = []
for (x, y) in random.sample(hole_pts, 10):
    L = random.uniform(0.06, 0.16)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.006, depth=L, vertices=6,
        location=U(x + (cx - x) * 0.08, y + (cy - y) * 0.08, WZ + random.uniform(-0.08, 0.08)),
        rotation=(0, math.pi / 2, math.atan2(y - cy, x - cx)))
    b = bpy.context.active_object; b.data.materials.append(rebar); wbars.append(b)
wrebar = join(wbars, "Fac_WallRebar")
export("bari_wall", [wall, band, wrebar])
# portal mask: the breach outline as one face in the wall plane (stencil writer in Unity)
mb = bmesh.new()
mv = [mb.verts.new(U(x, y, WZ)) for x, y in hole_pts]
mb.faces.new(mv)
bmesh.ops.triangulate(mb, faces=mb.faces[:], quad_method="BEAUTY", ngon_method="EAR_CLIP")
bmesh.ops.recalc_face_normals(mb, faces=mb.faces)
mask = obj_from_bm(mb, "Fac_PortalMask"); mask.data.materials.append(mat("FacPortalMask", (0, 0, 0)))
export("bari_portal_mask", [mask])

# ---------------------------------------------------------------- roller conveyor through the breach
CX, CW = -0.45, 0.36
Z0, Z1, TOP = -0.30, -1.52, 0.80        # from the table to beyond the wall, top at table height
parts, rolls = [], []
L = Z0 - Z1
for s in (-1, 1):
    parts.append(box((0.04, 0.035, 0.10), (0, 0, 0), "c", steel))
    parts[-1].location = U(CX + s * (CW / 2 + 0.02), TOP - 0.03, (Z0 + Z1) / 2)
    parts[-1].scale = (1, L / 0.035, 1)
    bpy.ops.object.select_all(action="DESELECT"); parts[-1].select_set(True); bpy.context.view_layer.objects.active = parts[-1]
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for z in (Z0 - 0.1, (Z0 + Z1) / 2, Z1 + 0.1):
        parts.append(box((0.04, 0.04, TOP - 0.08), U(CX + s * (CW / 2 + 0.02), (TOP - 0.08) / 2, z), "leg", steel))
n = int(L / 0.065)
for i in range(n):
    z = Z0 - (i + 0.5) * L / n
    bpy.ops.mesh.primitive_cylinder_add(radius=0.022, depth=CW, vertices=12, location=U(CX, TOP - 0.022, z),
                                        rotation=(0, math.pi / 2, 0))
    r = bpy.context.active_object; r.data.materials.append(roller); rolls.append(r)
conv = join(parts, "Fac_ConveyorFrame"); crol = join(rolls, "Fac_ConveyorRollers")
export("bari_conveyor", [conv, crol])

# ---------------------------------------------------------------- steel beam out of the wall + pendant lamp
# An HEB-style I-beam cantilevers out of the broken wall over the cell and carries the lamp.
BY = 2.30                               # beam centre height above the floor
BZ0, BZ1 = WZ - 0.30, 0.42               # from inside the wall to its free end
def ibeam_z(x, y, z0, z1, name):
    L = z1 - z0; cz = (z0 + z1) / 2
    web = box((0.012, L, 0.20), U(x, y, cz), name, steel)
    fl1 = box((0.20, L, 0.015), U(x, y + 0.093, cz), "f", steel)
    fl2 = box((0.20, L, 0.015), U(x, y - 0.093, cz), "f", steel)
    return join([web, fl1, fl2], name)
beam = ibeam_z(0.0, BY, BZ0, BZ1, "Fac_Beam")
# no boolean on the joined I-profile: overlapping boxes make the exact solver return nothing
# a lump of concrete still clinging where the beam leaves the wall
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.17, location=U(0.02, BY - 0.02, WZ + WT / 2 + 0.04))
lump = bpy.context.active_object; lump.name = "Fac_BeamLump"; lump.scale = (1.3, 0.6, 1.0); lump.data.materials.append(concrete)
bpy.ops.object.transform_apply(scale=True)
# pendant: shackle on the lower flange, cable, shade, glowing disc
LZ = 0.20
shackle = box((0.03, 0.03, 0.04), U(0.0, BY - 0.12, LZ), "Fac_Lamp", steel)
cable = box((0.008, 0.008, 0.26), U(0.0, BY - 0.12 - 0.13, LZ), "c", steel)
bpy.ops.mesh.primitive_cone_add(radius1=0.24, radius2=0.05, depth=0.18, vertices=32, location=U(0.0, BY - 0.47, LZ))
shade = bpy.context.active_object; shade.data.materials.append(mat("FacLampShade", (0.12, 0.13, 0.14), metal=0.5, rough=0.4))
lamp = join([shackle, cable, shade], "Fac_Lamp")
bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=0.01, vertices=32, location=U(0.0, BY - 0.565, LZ))
glow = bpy.context.active_object; glow.name = "Fac_LampGlow"; glow.data.materials.append(lampglow)
export("bari_beam_lamp", [beam, lump, lamp, glow])
print("lamp glow at floor-y", round(BY - 0.565, 3), "z", LZ)

print("factory ok", len(hole_pts))
