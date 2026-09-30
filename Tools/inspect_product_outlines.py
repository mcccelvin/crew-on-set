import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[1]
paths=list((root/'Assets/Product').glob('*.fbx'))+list((root/'Assets/Product/drive-download-20260911T074145Z-1-001').glob('*.fbx'))
paths += list((root/'Assets/Studio/Equipments').glob('Low*.fbx'))+list((root/'Assets/Resources/EquipmentModels').glob('*.fbx'))
for path in paths:
    if path.stem in ['STUDIOP','DefaultCharacGirlRig','coffee_actors_1_rig','coffee_actors_2_rig','coffee_actors_3_rig','CoffeeInterior','GreenScreen','Chair']: continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    print('ASSET',path.relative_to(root))
    for m in bpy.data.materials:
        print('MAT',m.name,tuple(round(c,3) for c in m.diffuse_color))
