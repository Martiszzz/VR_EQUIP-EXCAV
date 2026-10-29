using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class MenuPanelGraphic : MaskableGraphic
{
    public float radius=12,borderWidth=1;
    public Color borderColor=new Color(0.2f,0.27f,0.31f,1);
    public void SetBorder(Color value){borderColor=value;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;const int perCorner=8;const int count=perCorner*4;
        float outer=Mathf.Min(radius,r.width*0.5f,r.height*0.5f),inset=Mathf.Max(0,borderWidth);
        for(int ring=0;ring<2;ring++)
        {
            var rect=new Rect(r.xMin+ring*inset,r.yMin+ring*inset,r.width-ring*inset*2,r.height-ring*inset*2);
            float round=Mathf.Max(0,outer-ring*inset);
            for(int corner=0;corner<4;corner++)for(int i=0;i<perCorner;i++)
            {
                float angle=(corner*90+i*90f/(perCorner-1))*Mathf.Deg2Rad;
                Vector2 center=new Vector2(corner==0 || corner==3?rect.xMax-round:rect.xMin+round,corner<2?rect.yMax-round:rect.yMin+round);
                vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*round,ring==0?borderColor:color,Vector2.zero);
            }
        }
        vh.AddVert(r.center,color,Vector2.zero);
        for(int i=0;i<count;i++){int next=(i+1)%count;vh.AddTriangle(i,next,count+i);vh.AddTriangle(next,count+next,count+i);vh.AddTriangle(count*2,count+i,count+next);}
    }
}
