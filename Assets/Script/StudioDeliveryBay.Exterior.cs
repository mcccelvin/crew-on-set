using UnityEngine;

public sealed partial class StudioDeliveryBay
{
    private void BuildExteriorView()
    {
        if(!hasExteriorOpening)return;
        float wall=exteriorOpening.center.x,centre=exteriorOpening.center.z;
        var scenery=new GameObject("Outside studio - scenery only").transform;
        scenery.SetParent(frame,false);
        var lawn=Paint("Outdoor grass",new Color32(106,128,80,255),0);
        var paving=Paint("Loading yard asphalt",new Color32(105,113,116,255),0);
        var curb=Paint("Yard kerbs",new Color32(189,187,171,255),0);
        var building=Paint("Distant workshops",new Color32(173,183,182,255),0);
        var roof=Paint("Workshop roofs",new Color32(75,91,98,255),0);
        var foliage=Paint("Distant trees",new Color32(66,100,69,255),0);
        // Both surfaces stop outside the authored wall. Their tops differ so
        // the paved yard never shares a plane with the surrounding ground.
        Part(scenery,"Outdoor ground",new Vector3(wall-80.15f,-.12f,centre),new Vector3(160,.12f,180),lawn,false);
        Part(scenery,"Truck approach yard",new Vector3(wall-15.15f,-.055f,centre),new Vector3(30,.05f,32),paving,false);
        foreach(int side in new[]{-1,1})
        {
            Part(scenery,"Yard kerb",new Vector3(wall-15.15f,.04f,centre+side*16.1f),new Vector3(30,.16f,.2f),curb,false);
            for(int i=0;i<3;i++)
            {
                float x=wall-35-i*15,z=centre+side*(20+i*6);
                Part(scenery,"Tree trunk",new Vector3(x,1.25f,z),new Vector3(.5f,2.5f,.5f),roof,false);
                Part(scenery,"Tree crown",new Vector3(x,3.9f,z),new Vector3(4.2f,5.2f,4.2f),foliage,false,PrimitiveType.Sphere);
            }
            float depth=side==1?9:12;
            Part(scenery,"Distant workshop",new Vector3(wall-57,depth*.5f,centre+side*38),new Vector3(24,depth,17),building,false);
            Part(scenery,"Workshop roof",new Vector3(wall-57,depth+.2f,centre+side*38),new Vector3(25,.4f,18),roof,false);
        }
        // Keep the exterior visible, but the playable area ends at the garage
        // opening. The truck cargo and its access ramp remain on the inside.
        var boundary=new GameObject("Garage exterior player boundary",typeof(BoxCollider));
        boundary.transform.SetParent(frame,false);boundary.layer=garageFloorLayer;
        boundary.transform.localPosition=exteriorOpening.center+Vector3.left*.08f+Vector3.up*.5f;
        boundary.GetComponent<BoxCollider>().size=new Vector3(.35f,exteriorOpening.size.y+2,exteriorOpening.size.z+.5f);
        foreach(var renderer in scenery.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }
    }
}
