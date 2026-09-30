import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'D:\CrewOnSetUnity\crew-on-set\Assets\Equipments and Products\STUDIOP.fbx')
for o in bpy.data.objects:
    if o.type == 'MESH' and (o.name in ['Floor','Floor.005','Floor.026','studio.005','kiosk.001','Plane.002','director table','editor table.001','sound table.002','kiosk table.001']):
        print('MESH',o.name, 'dimensions',tuple(o.dimensions),'vertices',len(o.data.vertices),'materials',[(m.name if m else None) for m in o.data.materials])
