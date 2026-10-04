# Cell details, built with the helpers of bari_assets.py prepended:
#   OmniCore C30 controller under the robot table (449 x 189 x 384 mm), the cables that make
#   it a working cell (robot <-> controller, controller <-> industrial PC, mains to a socket
#   on the wall), a fanless box PC under the table next to the controller (no monitor - the
#   HUD is the screen), and the anchor reference plate on the table corner.
# Coordinates in the Unity cell frame measured from the FLOOR (X right, Y up, Z to the visitor).
# Screen and QR faces are quads added in Unity (textures from tools/branding).
import math

def U(x, y, z):
    return (-x, -z, y)

for o in [o for o in bpy.data.objects if o.name.startswith("Det_")]:
    bpy.data.objects.remove(o, do_unlink=True)

body = mat("Controller", (0.80, 0.81, 0.82), metal=0.2, rough=0.5)
dark = mat("ControllerDark", (0.16, 0.17, 0.19), metal=0.3, rough=0.6)
cable = mat("Cable", (0.03, 0.03, 0.035), rough=0.7)
bezel = mat("PanelBezel", (0.09, 0.10, 0.11), metal=0.4, rough=0.5)
alu = mat("Profile", (0.78, 0.8, 0.82), metal=0.9, rough=0.35)

TABLE_TOP = 0.80

# ---------------------------------------------------------------- OmniCore C30
CX, CZ, CW, CH, CD = -0.35, -0.05, 0.449, 0.189, 0.384
cy = 0.02 + CH / 2
parts = [box((CW, CD, CH), U(CX, cy, CZ), "Det_Controller", body)]
parts.append(box((CW - 0.03, 0.006, CH - 0.04), U(CX, cy, CZ + CD / 2 + 0.002), "front", dark))
for sx in (-1, 1):   # handles
    parts.append(box((0.02, 0.03, 0.12), U(CX + sx * (CW / 2 + 0.012), cy, CZ + CD / 2 - 0.04), "h", dark))
    for fz in (-1, 1):   # feet
        parts.append(box((0.04, 0.04, 0.02), U(CX + sx * (CW / 2 - 0.04), 0.01, CZ + fz * (CD / 2 - 0.04)), "ft", dark))
for i, bx in enumerate((-0.12, -0.07)):   # power switch and status lamp
    bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=0.012, vertices=12,
                                        location=U(CX + bx, cy + 0.03, CZ + CD / 2 + 0.008), rotation=(math.pi / 2, 0, 0))
    b = bpy.context.active_object; b.data.materials.append(body if i else dark); parts.append(b)
controller = join(parts, "Det_Controller")

# ---------------------------------------------------------------- cables
def cable_curve(points, radius, name):
    cd = bpy.data.curves.new(name, type="CURVE")
    cd.dimensions = "3D"
    cd.bevel_depth = radius
    cd.bevel_resolution = 1
    cd.resolution_u = 6
    sp = cd.splines.new("BEZIER")
    sp.bezier_points.add(len(points) - 1)
    for bp, p in zip(sp.bezier_points, points):
        bp.co = U(*p)
        bp.handle_left_type = bp.handle_right_type = "AUTO"
    ob = bpy.data.objects.new(name, cd)
    bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.convert(target="MESH")
    ob = bpy.context.active_object
    ob.data.materials.append(cable)
    return ob

back = CZ - CD / 2
WALL = -1.29          # front face of the factory wall
SLAB = 0.004          # top of the factory floor slab
def lie(r): return SLAB + r + 0.004   # cable centre height when it lies on the floor
cables = [
    # robot foot -> over the back edge of the table -> down -> controller back
    cable_curve([(0.02, TABLE_TOP + 0.03, -0.10), (0.02, TABLE_TOP + 0.015, -0.27), (0.0, TABLE_TOP - 0.10, -0.33),
                 (-0.08, 0.35, -0.34), (-0.25, 0.12, -0.30), (CX + 0.05, 0.10, back - 0.01)], 0.013, "c1"),
    # controller -> box PC, a short loop lying on the floor (extra points: no sag below it)
    cable_curve([(CX + CW / 2 + 0.005, 0.08, CZ + 0.10), (CX + CW / 2 + 0.03, lie(0.006) + 0.02, CZ + 0.13),
                 (CX + CW / 2 + 0.06, lie(0.006), CZ + 0.14), (0.0, lie(0.006), CZ + 0.13),
                 (0.025, lie(0.006) + 0.015, CZ + 0.11), (0.03, 0.04, CZ + 0.095)], 0.006, "c2"),
    # mains: controller back -> along the floor -> bend -> up the wall face into the socket
    cable_curve([(CX - 0.10, 0.08, back - 0.01), (CX - 0.13, lie(0.008) + 0.02, back - 0.10),
                 (CX - 0.16, lie(0.008), back - 0.18), (-0.60, lie(0.008), -1.05), (-0.62, lie(0.008), -1.20),
                 (-0.62, lie(0.008) + 0.03, WALL + 0.008 + 0.012), (-0.62, 0.12, WALL + 0.008 + 0.006),
                 (-0.62, 0.36, WALL + 0.008 + 0.006)], 0.008, "c3"),
]
socket = box((0.08, 0.04, 0.10), U(-0.62, 0.40, WALL + 0.02), "sock", dark)
cables_obj = join(cables + [socket], "Det_Cables")

# ---------------------------------------------------------------- fanless box PC under the table
PX, PZ, PW, PH, PD = 0.10, -0.05, 0.26, 0.075, 0.20
py = 0.012 + PH / 2
pc = [box((PW, PD, PH), U(PX, py, PZ), "Det_PC", bezel)]
for i in range(9):   # cooling fins on top
    fx = PX - PW / 2 + 0.02 + i * (PW - 0.04) / 8
    pc.append(box((0.004, PD - 0.02, 0.022), U(fx, py + PH / 2 + 0.011, PZ), "fin", bezel))
pc.append(box((PW - 0.03, 0.004, 0.03), U(PX, py - 0.01, PZ + PD / 2 + 0.002), "ports", dark))
for fz in (-1, 1):
    for fx in (-1, 1):
        pc.append(box((0.03, 0.03, 0.012), U(PX + fx * (PW / 2 - 0.03), 0.006, PZ + fz * (PD / 2 - 0.03)), "ft", dark))
pc_obj = join(pc, "Det_PC")

# ---------------------------------------------------------------- anchor reference plate on the table
QX, QZ = 0.22, 0.20
qr = box((0.16, 0.195, 0.004), U(QX, TABLE_TOP + 0.002, QZ), "Det_QRPlate", alu)

export("bari_cell_details", [controller, cables_obj, pc_obj, qr])
print("details ok")
