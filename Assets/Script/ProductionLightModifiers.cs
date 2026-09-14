using UnityEngine;
using System.Collections.Generic;

// Bounded real-time approximation, not a photometric transmission/GI solver.
public sealed class ProductionLightModifiers : MonoBehaviour
{
    struct State {public float intensity;public LightShadows shadows;}
    readonly Dictionary<Light,State> modified=new Dictionary<Light,State>();
    float next;
    void Restore(){foreach(var pair in modified)if(pair.Key!=null){if(Mathf.Approximately(pair.Key.intensity,pair.Value.intensity*.65f))pair.Key.intensity=pair.Value.intensity;if(pair.Key.shadows==LightShadows.Soft)pair.Key.shadows=pair.Value.shadows;}modified.Clear();}
    void LateUpdate()
    {
        if(Time.time<next)return;next=Time.time+.15f;Restore();
        var kits=FindObjectsOfType<ProductionKit>();var lights=FindObjectsOfType<Light>();
        foreach(var kit in kits)
        {
            if(!kit.placed||kit.template)continue;
            Vector3 center=kit.transform.position+Vector3.up*1.4f;
            if(kit.kind==4&&kit.bounce!=null)
            {
                float fill=0;Color tint=Color.black;
                foreach(var source in lights)
                {
                    if(!source.enabled||(source.GetComponentInParent<ProductionKit>()!=null && source.GetComponentInParent<ProductionKit>().kind==4))continue;
                    Vector3 delta=source.transform.position-center;float distance=delta.magnitude;
                    if(distance>6||distance<.1f)continue;
                    float facing=Mathf.Max(0,Vector3.Dot(kit.transform.forward,delta.normalized));
                    if(source.type==LightType.Spot&&Vector3.Angle(source.transform.forward,-delta)>source.spotAngle*.5f)continue;
                    float value=source.intensity*facing/(1+distance*distance)*.6f;fill+=value;tint+=source.color*value;
                }
                kit.bounce.intensity=Mathf.Min(fill,1.2f);if(fill>0)kit.bounce.color=tint/fill;
            }
            if(kit.kind!=3)continue;
            foreach(var source in lights)
            {
                if(!source.enabled||source.type!=LightType.Spot||(source.GetComponentInParent<ProductionKit>()!=null && source.GetComponentInParent<ProductionKit>().kind==4)||modified.ContainsKey(source))continue;
                Vector3 local=kit.transform.InverseTransformPoint(source.transform.position);
                Vector3 dir=kit.transform.InverseTransformDirection(source.transform.forward);
                if(Mathf.Abs(dir.z)<.05f)continue;float t=-local.z/dir.z;
                Vector3 cross=local+dir*t;
                if(t<=0||t>3||Mathf.Abs(cross.x)>.8f||Mathf.Abs(cross.y-1.4f)>.8f)continue;
                modified[source]=new State{intensity=source.intensity,shadows=source.shadows};source.intensity*=.65f;source.shadows=LightShadows.Soft;
            }
        }
    }
    void OnDisable(){Restore();}
}

