using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hue around the edge, saturation from centre to edge. Value has its own slider.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DirectorColorWheel : MaskableGraphic,IPointerDownHandler,IDragHandler
{
    public bool Interactable=true;
    public System.Action<Color> Changed;
    private float hue,saturation,value=1;
    public Color SelectedColor=>Color.HSVToRGB(hue,saturation,value);
    public float Value=>value;
    private float Radius=>Mathf.Max(1,Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f-6);
    public void SetColorWithoutNotify(Color selected)
    {
        Color.RGBToHSV(selected,out float h,out float s,out float v);
        if(v<=.001f)s=saturation;
        if(s<=.001f || v<=.001f)h=hue;
        if(Mathf.Abs(h-hue)+Mathf.Abs(s-saturation)+Mathf.Abs(v-value)<.0001f)return;
        hue=h;saturation=s;value=v;SetVerticesDirty();
    }
    public void SetValue(float brightness)
    {
        if(!Interactable)return;
        value=Mathf.Clamp01(brightness);SetVerticesDirty();Changed?.Invoke(SelectedColor);
    }
    public void OnPointerDown(PointerEventData e)
    {
        if(e.button!=PointerEventData.InputButton.Left || !ReadPoint(e,out var point) || point.magnitude>Radius)return;
        Pick(point);
    }
    public void OnDrag(PointerEventData e)
    {
        if(e.button==PointerEventData.InputButton.Left && ReadPoint(e,out var point))Pick(point);
    }
    private bool ReadPoint(PointerEventData e,out Vector2 point)
    {
        point=default;
        if(!Interactable || PauseManager.isPaused || !Application.isFocused)return false;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out point))return false;
        point-=rectTransform.rect.center;return true;
    }
    private void Pick(Vector2 point)
    {
        if(point.sqrMagnitude>.001f)hue=Mathf.Repeat(Mathf.Atan2(point.y,point.x)/(Mathf.PI*2),1);
        saturation=Mathf.Clamp01(point.magnitude/Radius);SetVerticesDirty();Changed?.Invoke(SelectedColor);
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();Vector2 centre=rectTransform.rect.center;
        Disc(mesh,centre,Radius+5,new Color32(22,41,65,255));
        Disc(mesh,centre,Radius+2,Color.white);
        int start=mesh.currentVertCount;const int steps=96;
        mesh.AddVert(centre,Color.white,Vector2.zero);
        for(int i=0;i<=steps;i++)
        {
            float h=(float)i/steps,a=h*Mathf.PI*2;
            mesh.AddVert(centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*Radius,Color.HSVToRGB(h,1,1),Vector2.zero);
            if(i>0)mesh.AddTriangle(start,start+i,start+i+1);
        }
        float angle=hue*Mathf.PI*2;
        var marker=centre+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Radius*saturation;
        Disc(mesh,marker,8,new Color32(17,35,56,255));Disc(mesh,marker,6,Color.white);Disc(mesh,marker,3.5f,SelectedColor);
    }
    private static void Disc(VertexHelper mesh,Vector2 centre,float radius,Color tint)
    {
        int start=mesh.currentVertCount;const int steps=48;
        mesh.AddVert(centre,tint,Vector2.zero);
        for(int i=0;i<=steps;i++)
        {
            float a=(float)i/steps*Mathf.PI*2;
            mesh.AddVert(centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero);
            if(i>0)mesh.AddTriangle(start,start+i,start+i+1);
        }
    }
}
