import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[1]
for path in ['Assets/Product/STUDIOP.fbx','Assets/Equipments and Products/STUDIOP.fbx']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/path))
    print('MODEL',path)
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        names=[m.name if m else '' for m in o.data.materials]
        if any('glass' in n.lower() or 'window' in n.lower() for n in names) or o.name in ['Floor.011','Floor.013']:
            print(o.name, 'vertices',len(o.data.vertices), 'materials',names,'dimensions',tuple(round(v,3) for v in o.dimensions))
