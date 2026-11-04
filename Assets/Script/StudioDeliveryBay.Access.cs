using System.Collections.Generic;
using UnityEngine;

public sealed partial class StudioDeliveryBay
{
    private void BuildTruckWalkway(Transform truck,Material deck,Material edge,Material yellow)
    {
        // One continuous collision surface: floor -> pickup landing -> cargo.
        // Positions are truck-local, but the approach run and plate thickness
        // stay in world metres when the vehicle's size changes.
        var profile=new[] {
            new Vector3(0,-.01f/truckScale,-4.4f-FloorRampRun/truckScale),
            new Vector3(0,1f/truckScale,-4.4f),
            new Vector3(0,1f/truckScale,-3.92f),
            new Vector3(0,1.10f+.015f/truckScale,-2.70f),
            new Vector3(0,1.10f+.015f/truckScale,-2.38f)
        };
        float thickness=.08f/truckScale;
        var vertices=new List<Vector3>();var uv=new List<Vector2>();
        var top=new List<int>();var sides=new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,List<int> triangles)
        {
            int first=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
            uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});
            triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
        }
        for(int i=0;i<profile.Length-1;i++)
        {
            Vector3 a=profile[i]-Vector3.right*accessHalfWidth,b=profile[i]+Vector3.right*accessHalfWidth;
            Vector3 c=profile[i+1]-Vector3.right*accessHalfWidth,d=profile[i+1]+Vector3.right*accessHalfWidth;
            var down=Vector3.down*thickness;
            Quad(a,c,d,b,top);
            Quad(a+down,b+down,d+down,c+down,sides);
            Quad(a,a+down,c+down,c,sides);
            Quad(b,d,d+down,b+down,sides);
            if(i==0)Quad(a,b,b+down,a+down,sides);
            if(i==profile.Length-2)Quad(c,c+down,d+down,d,sides);
            Vector3 direction=profile[i+1]-profile[i];
            foreach(int side in new[]{-1,1})
            {
                var stripe=Part(truck,"Ramp safety edge",(profile[i]+profile[i+1])*.5f+
                    Vector3.right*(side*(accessHalfWidth-.03f/truckScale))+Vector3.up*(.02f/truckScale),
                    new Vector3(.045f/truckScale,.02f/truckScale,direction.magnitude),yellow,false);
                stripe.transform.localRotation=Quaternion.LookRotation(direction,Vector3.up);
            }
        }
        var mesh=new Mesh{name="Continuous truck access deck"};
        mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=2;
        mesh.SetTriangles(top,0);mesh.SetTriangles(sides,1);
        mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
        var ramp=new GameObject("Walk into delivery truck",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
        ramp.transform.SetParent(truck,false);ramp.layer=garageFloorLayer;
        ramp.GetComponent<MeshFilter>().sharedMesh=mesh;
        ramp.GetComponent<MeshRenderer>().sharedMaterials=new[]{deck,edge};
        ramp.GetComponent<MeshCollider>().sharedMesh=mesh;
    }
}
