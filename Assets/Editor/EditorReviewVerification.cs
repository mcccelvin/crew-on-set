using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Isolated GPU check. Never opens/replaces a player's scene or starts Play Mode.
public static class EditorReviewVerification
{
    private static string ResultRoot
    {
        get
        {
            var args = Environment.GetCommandLineArgs();
            for (int i=0;i<args.Length-1;i++) if(args[i]=="-reviewOutput") return args[i+1];
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EditorReview");
        }
    }
    [MenuItem("Crew-On-Set/Testing/Verify Embedded Review Graphics")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop Play mode before running the isolated review check."); return; }
        Directory.CreateDirectory(ResultRoot);
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Isolated review graphics check") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var compositor = new VideoFrameCompositor();
        Texture2D footage = null, white = null, readback = null; Sprite sprite = null; Mesh mesh = null;
        Color Sample(int x,int y) => readback.GetPixel(Mathf.FloorToInt((x+.5f)*readback.width/256f),Mathf.FloorToInt((y+.5f)*readback.height/128f));
        try
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) throw new Exception("No graphics device; this GPU check cannot run with -nographics.");
            var monitorGO = new GameObject("Test monitor", typeof(RectTransform), typeof(RawImage)); monitorGO.transform.SetParent(root.transform,false);
            var monitor = monitorGO.GetComponent<RectTransform>(); monitor.sizeDelta = new Vector2(256,128);
            footage = new Texture2D(64,64,TextureFormat.RGBA32,false); var pixels = new Color[64*64];
            for (int i=0;i<pixels.Length;i++) pixels[i] = Color.blue; footage.SetPixels(pixels); footage.Apply();
            white = new Texture2D(2,2); white.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white}); white.Apply();
            sprite = Sprite.Create(white,new Rect(0,0,2,2),new Vector2(.5f,.5f));
            var graphicGO = new GameObject("Placed test graphic",typeof(RectTransform),typeof(Image),typeof(DraggableOverlay)); graphicGO.transform.SetParent(monitor,false);
            var graphicRect = graphicGO.GetComponent<RectTransform>(); graphicRect.sizeDelta = new Vector2(64,32); graphicRect.anchoredPosition = new Vector2(40,-20);
            var graphic = graphicGO.GetComponent<Image>(); graphic.sprite = sprite;
            var overlay = graphicGO.GetComponent<DraggableOverlay>();
            typeof(DraggableOverlay).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(overlay,null);
            overlay.isOnTimeline = true; overlay.startFrame = 24; overlay.endFrame = 48;
            mesh = new Mesh(); mesh.vertices = new[]{new Vector3(-32,-16),new Vector3(-32,16),new Vector3(32,16),new Vector3(32,-16)};
            mesh.uv = new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right}; mesh.colors = new[]{Color.red,Color.red,Color.red,Color.red}; mesh.triangles = new[]{0,1,2,2,3,0};
            graphic.canvasRenderer.SetMesh(mesh);
            var snapshotA=graphic.canvasRenderer.GetMesh(); var snapshotB=graphic.canvasRenderer.GetMesh();
            Debug.Log("Canvas mesh snapshot IDs: "+snapshotA.GetInstanceID()+" / "+snapshotB.GetInstanceID()+"; authored mesh: "+mesh.GetInstanceID());
            var overlays = new[]{overlay}; var group = graphicGO.GetComponent<CanvasGroup>();
            // Evaluate actual timing; Cut avoids a player's optional fade selection affecting fixtures.
            overlay.EvaluateVisibility(23,false);
            readback = Read(compositor.Compose(footage,monitor,overlays));
            Check(readback.width==1920 && readback.height==1080,"Tiny tape reduced the output resolution");
            Check(Sample(168,44).b>.9f,"Graphic appeared before its start frame"); Destroy(readback);
            group.alpha = 1;
            readback = Read(compositor.Compose(footage,monitor,overlays));
            File.WriteAllBytes(Path.Combine(ResultRoot,"editor-review-embedded-gpu.png"),readback.EncodeToPNG());
            Check(Sample(168,44).r>.9f && Sample(168,44).b<.1f,"Overlay not embedded at the expected pixel / wrong Y orientation");
            Check(Sample(10,10).b>.9f,"Graphic covered footage outside its bounds");
            File.WriteAllBytes(Path.Combine(ResultRoot,"editor-review-embedded-gpu.png"),readback.EncodeToPNG()); Destroy(readback);
            group.alpha = .5f;
            readback = Read(compositor.Compose(footage,monitor,overlays)); var blended = Sample(168,44);
            Check(blended.r>.1f && blended.b>.1f,"Graphic transparency lost"); Destroy(readback);
            readback = Read(compositor.Compose(footage,monitor,overlays)); var repeated = Sample(168,44);
            Check(Mathf.Abs(repeated.r-blended.r)<.02f && Mathf.Abs(repeated.b-blended.b)<.02f,"Scrubbing accumulates a logo each frame"); Destroy(readback);
            overlay.EvaluateVisibility(48,false);
            readback = Read(compositor.Compose(footage,monitor,overlays)); Check(Sample(168,44).b>.9f,"End frame is not exclusive");
            Destroy(readback); group.alpha=1;
            // A differently sized monitor and a non-centred pivot must map to identical video pixels.
            monitor.sizeDelta=new Vector2(512,256); monitor.pivot=Vector2.zero;
            graphicRect.localPosition=new Vector3(336,88,0);
            mesh.vertices=new[]{new Vector3(-64,-32),new Vector3(-64,32),new Vector3(64,32),new Vector3(64,-32)};
            graphic.canvasRenderer.SetMesh(mesh);
            readback=Read(compositor.Compose(footage,monitor,overlays));
            Check(Sample(168,44).r>.9f && Sample(10,10).b>.9f,"Monitor resizing/pivot changed video placement");
            Destroy(readback);
            // Pixels outside the source monitor cannot turn into floating screen UI.
            graphicRect.localPosition=new Vector3(500,240,0);
            readback=Read(compositor.Compose(footage,monitor,overlays));
            Check(Sample(255,127).r>.9f && Sample(10,10).b>.9f,"Output bounds/edge clipping failed");
            VerifyArtwork(compositor,footage,monitor,graphic,overlay,mesh);
            File.WriteAllText(Path.Combine(ResultRoot,"editor-review-gpu-result.txt"),"PASS: actual Unity GPU compositor at 1920x1080 over a 64x64 tape; fine graphic detail, original wordmark render, position/Y orientation, monitor resizing/pivots, output clipping, background preservation, transparency, repeat-frame reset and exclusive start/end visibility. Device: "+SystemInfo.graphicsDeviceType);
            Debug.Log("Embedded commercial graphics GPU checks passed.");
        }
        catch(Exception ex)
        { File.WriteAllText(Path.Combine(ResultRoot,"editor-review-gpu-result.txt"),"FAIL: "+ex); Debug.LogException(ex); }
        finally
        {
            compositor.Dispose(); Destroy(readback); Destroy(mesh); Destroy(sprite); Destroy(white); Destroy(footage);
            UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static void VerifyArtwork(VideoFrameCompositor compositor,Texture2D footage,RectTransform monitor,Image graphic,DraggableOverlay overlay,Mesh mesh)
    {
        Texture2D detail=null,render=null;Sprite art=null;
        try
        {
            monitor.sizeDelta=new Vector2(1920,1080);monitor.pivot=Vector2.one*.5f;
            graphic.rectTransform.localPosition=Vector3.zero;
            detail=new Texture2D(1024,64,TextureFormat.RGBA32,false);
            var pixels=new Color[1024*64];
            for(int y=0;y<64;y++)for(int x=0;x<1024;x++)pixels[y*1024+x]=(x/4)%2==0?Color.white:Color.black;
            detail.SetPixels(pixels);detail.Apply();
            art=Sprite.Create(detail,new Rect(0,0,1024,64),Vector2.one*.5f);graphic.sprite=art;
            mesh.vertices=new[]{new Vector3(-512,-32),new Vector3(-512,32),new Vector3(512,32),new Vector3(512,-32)};
            mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};graphic.canvasRenderer.SetMesh(mesh);
            render=Read(compositor.Compose(footage,monitor,new[]{overlay}));
            for(int x=32;x<992;x+=8)
                Check(render.GetPixel(448+x+1,540).r>.9f && render.GetPixel(448+x+5,540).r<.1f,"Fine graphic stripes blurred with low-resolution footage");
            Destroy(render);render=null;Destroy(art);art=null;
            detail.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"UI/UI-EXPORT/PRODUCT/VASE/FLORA & FORM HOME.png")));
            art=Sprite.Create(detail,new Rect(0,0,detail.width,detail.height),Vector2.one*.5f);graphic.sprite=art;
            float halfHeight=500f*detail.height/detail.width;
            mesh.vertices=new[]{new Vector3(-500,-halfHeight),new Vector3(-500,halfHeight),new Vector3(500,halfHeight),new Vector3(500,-halfHeight)};
            graphic.canvasRenderer.SetMesh(mesh);
            var background=new Color[64*64];for(int i=0;i<background.Length;i++)background[i]=new Color(.18f,.75f,.44f);
            footage.SetPixels(background);footage.Apply();
            render=Read(compositor.Compose(footage,monitor,new[]{overlay}));
            File.WriteAllBytes(Path.Combine(ResultRoot,"embedded-branding-fullhd.png"),render.EncodeToPNG());
        }
        finally { Destroy(render);Destroy(art);Destroy(detail); }
    }
    private static Texture2D Read(Texture source)
    {
        var previous = RenderTexture.active; RenderTexture.active = (RenderTexture)source;
        var result = new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
        try { result.ReadPixels(new Rect(0,0,source.width,source.height),0,0); result.Apply(); }
        finally { RenderTexture.active = previous; }
        return result;
    }
    private static void Destroy(UnityEngine.Object value) { if(value!=null) UnityEngine.Object.DestroyImmediate(value); }
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
}
