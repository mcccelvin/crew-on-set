"""Bevel Unity-local mesh copies; preserve material slots and interpolated UVs."""
import bpy, bmesh, json, math
from pathlib import Path

work = Path(__file__).resolve().parents[1] / 'Logs/StudioStyle'
for name in ['director table', 'editor table.001', 'sound table.002', 'kiosk table.001']:
    data = json.loads((work / (name + '.json')).read_text())
    faces, slots = [], []
    for slot, sub in enumerate(data['submeshes']):
        indices = sub['triangles']
        faces.extend([indices[i:i+3] for i in range(0, len(indices), 3)])
        slots.extend([slot] * (len(indices)//3))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(v['x'],v['y'],v['z']) for v in data['vertices']], [], faces)
    uv = mesh.uv_layers.new()
    for poly, slot in zip(mesh.polygons, slots):
        poly.material_index = slot
        for loop in poly.loop_indices:
            index = mesh.loops[loop].vertex_index
            coord = data['uv'][index] if len(data['uv']) else {'x':0,'y':0}
            uv.data[loop].uv = (coord['x'],coord['y'])
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.0000001)
    bm.normal_update()
    edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle() > math.radians(35)]
    bmesh.ops.bevel(bm, geom=edges, offset=data['width'], segments=3,
                   affect='EDGES', clamp_overlap=True, loop_slide=True)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.normal_update(); bm.to_mesh(mesh); bm.free(); mesh.update()
    result = {'vertices':[], 'normals':[], 'uv':[], 'submeshes':[{'triangles':[]} for _ in data['submeshes']]}
    for poly in mesh.polygons:
        for loop in poly.loop_indices:
            v = mesh.vertices[mesh.loops[loop].vertex_index].co
            n = poly.normal
            uv = mesh.uv_layers.active.data[loop].uv
            result['submeshes'][poly.material_index]['triangles'].append(len(result['vertices']))
            result['vertices'].append(dict(zip(('x','y','z'),v)))
            result['normals'].append(dict(zip(('x','y','z'),n)))
            result['uv'].append(dict(zip(('x','y'),uv)))
    (work / (name + '.beveled.json')).write_text(json.dumps(result))
    print(name, 'beveled edges:',len(edges), 'triangles:',len(mesh.polygons))
