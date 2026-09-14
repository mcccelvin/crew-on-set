using UnityEngine;

// Display-referred clipping aid: this is not a calibrated waveform or lux meter.
public sealed class ProductionExposureMonitor : MonoBehaviour
{
    Camera source;
    RenderTexture sample;
    Texture2D pixels;
    float next;
    bool shown;
    float dark,bright;
    readonly int[] histogram=new int[32];
    public static void Toggle(Camera camera)
    {
        if(camera==null||GameSavePrefs.GetInt("OwnedEquipment.EXPOSURE MONITOR",0)==0)return;
        var monitor=camera.GetComponent<ProductionExposureMonitor>()??camera.gameObject.AddComponent<ProductionExposureMonitor>();monitor.source=camera;monitor.shown=!monitor.shown;
    }
    void LateUpdate()
    {
        if(!shown||source==null||!source.isActiveAndEnabled||Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;
        if(sample==null){sample=new RenderTexture(64,36,16);pixels=new Texture2D(64,36,TextureFormat.RGB24,false);}
        var target=source.targetTexture;var active=RenderTexture.active;
        try{
            source.targetTexture=sample;source.Render();RenderTexture.active=sample;pixels.ReadPixels(new Rect(0,0,64,36),0,0);pixels.Apply();
            System.Array.Clear(histogram,0,histogram.Length);dark=bright=0;
            var colors=pixels.GetPixels32();foreach(var c in colors){float y=(.2126f*c.r+.7152f*c.g+.0722f*c.b)/255f;histogram[Mathf.Min(31,(int)(y*32))]++;if(y<.03f)dark++;if(y>.97f)bright++;}dark=dark/colors.Length*100;bright=bright/colors.Length*100;
        }finally{source.targetTexture=target;RenderTexture.active=active;}
    }
    void OnGUI()
    {
        if(!shown||source==null||!source.isActiveAndEnabled)return;
        var rect=new Rect(Screen.width-310,40,295,160);GUI.Box(rect,"EXPOSURE MONITOR [M]");
        int peak=1;foreach(int n in histogram)peak=Mathf.Max(peak,n);
        for(int i=0;i<32;i++){float h=55f*histogram[i]/peak;GUI.DrawTexture(new Rect(rect.x+15+i*8,rect.y+85-h,6,h),Texture2D.whiteTexture);}
        GUI.Label(new Rect(rect.x+12,rect.y+90,275,65),"Near black: "+dark.ToString("F0")+"% | Near white: "+bright.ToString("F0")+"%\nCheck product detail, not a target percentage.\nImage estimate; not calibrated exposure.");
    }
    void OnDestroy(){if(sample!=null){sample.Release();Destroy(sample);}if(pixels!=null)Destroy(pixels);}
}
