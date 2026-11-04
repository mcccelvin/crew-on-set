using System.Collections.Generic;
using UnityEngine;

public sealed partial class StudioDeliveryBay
{
    private struct Vertex
    {
        public Vector3 position, normal, local;
        public Vector2 uv, uv2;
        public Color color;
        public static Vertex Between(Vertex a,Vertex b,float t) => new Vertex {
            position=Vector3.LerpUnclamped(a.position,b.position,t),normal=Vector3.LerpUnclamped(a.normal,b.normal,t).normalized,
            local=Vector3.LerpUnclamped(a.local,b.local,t),uv=Vector2.LerpUnclamped(a.uv,b.uv,t),
            uv2=Vector2.LerpUnclamped(a.uv2,b.uv2,t),color=Color.LerpUnclamped(a.color,b.color,t)};
    }

    private void OpenPassage(MeshFilter[] structure,Bounds passage)
    {
        foreach(var filter in structure)
        {
            var renderer=filter.GetComponent<MeshRenderer>();
            var source=filter.sharedMesh;
            if(renderer==null || !renderer.enabled || !LocalBounds(renderer.bounds).Intersects(passage))continue;
            if(!source.isReadable)
            {Debug.LogWarning("Delivery doorway requires Read/Write on studio geometry: "+filter.name,filter);continue;}
            var vertices=source.vertices;var normals=source.normals;var uv=source.uv;var uv2=source.uv2;var colors=source.colors;
            var output=new List<Vertex>();var submeshes=new List<int[]>();bool changed=false;
            Vertex Read(int i)=>new Vertex {position=vertices[i],normal=normals.Length==vertices.Length?normals[i]:Vector3.up,
                uv=uv.Length==vertices.Length?uv[i]:Vector2.zero,uv2=uv2.Length==vertices.Length?uv2[i]:Vector2.zero,
                color=colors.Length==vertices.Length?colors[i]:Color.white,
                local=frame.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]))};
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var indices=new List<int>();var triangles=source.GetTriangles(sub);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=Read(triangles[i]);var b=Read(triangles[i+1]);var c=Read(triangles[i+2]);
                    var normal=Vector3.Cross(b.local-a.local,c.local-a.local).normalized;
                    var bounds=new Bounds(a.local,Vector3.zero);bounds.Encapsulate(b.local);bounds.Encapsulate(c.local);
                    if(Mathf.Abs(normal.y)>.35f || !bounds.Intersects(passage))
                    {Emit(new List<Vertex>{a,b,c},output,indices);continue;}
                    // Subtract the portal box from a triangle. Each emitted
                    // outside polygon retains interpolated texture coordinates;
                    // the final inside polygon is the only discarded geometry.
                    var inside=new List<Vertex>{a,b,c};
                    for(int plane=0;plane<6 && inside.Count>0;plane++)
                    {
                        int axis=plane/2;bool lower=(plane&1)==0;
                        float limit=lower?passage.min[axis]:passage.max[axis];
                        Split(inside,axis,limit,lower,out var outside,out var remainder);
                        Emit(outside,output,indices);inside=remainder;
                    }
                    if(inside.Count>=3)changed=true;
                }
                submeshes.Add(indices.ToArray());
            }
            if(!changed)continue;
            var mesh=new Mesh {name=source.name+" / walk-through delivery passage"};
            if(output.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            var points=new List<Vector3>();var directions=new List<Vector3>();var texcoords=new List<Vector2>();
            var secondary=new List<Vector2>();var tint=new List<Color>();
            foreach(var vertex in output)
            {points.Add(vertex.position);directions.Add(vertex.normal);texcoords.Add(vertex.uv);secondary.Add(vertex.uv2);tint.Add(vertex.color);}
            mesh.SetVertices(points);mesh.SetNormals(directions);mesh.SetUVs(0,texcoords);mesh.SetUVs(1,secondary);mesh.SetColors(tint);
            mesh.subMeshCount=submeshes.Count;
            for(int i=0;i<submeshes.Count;i++)mesh.SetTriangles(submeshes[i],i);
            mesh.RecalculateBounds();mesh.RecalculateTangents();meshes.Add(mesh);filter.sharedMesh=mesh;
            bool colliderFound=false;
            foreach(var collider in filter.GetComponents<Collider>())
            {
                if(collider.isTrigger || !collider.enabled)continue;
                if(collider is MeshCollider target){target.sharedMesh=mesh;colliderFound=true;}
                else collider.enabled=false;
            }
            if(!colliderFound)filter.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
    }

    private static void Split(List<Vertex> polygon,int axis,float limit,bool lower,out List<Vertex> outside,out List<Vertex> inside)
    {
        outside=new List<Vertex>();inside=new List<Vertex>();
        for(int i=0;i<polygon.Count;i++)
        {
            var a=polygon[i];var b=polygon[(i+1)%polygon.Count];
            float da=(a.local[axis]-limit)*(lower?1:-1),db=(b.local[axis]-limit)*(lower?1:-1);
            bool ain=da>=0,bin=db>=0;
            if(ain)inside.Add(a);else outside.Add(a);
            if(ain==bin)continue;
            var edge=Vertex.Between(a,b,da/(da-db));inside.Add(edge);outside.Add(edge);
        }
    }

    private static void Emit(List<Vertex> polygon,List<Vertex> vertices,List<int> indices)
    {
        if(polygon.Count<3)return;
        int offset=vertices.Count;vertices.AddRange(polygon);
        for(int i=1;i<polygon.Count-1;i++){indices.Add(offset);indices.Add(offset+i);indices.Add(offset+i+1);}
    }
}
