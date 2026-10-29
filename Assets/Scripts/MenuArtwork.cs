using UnityEngine;
using UnityEngine.UI;

// Lightweight vector artwork stays sharp in both eyes and needs no large background texture.
[RequireComponent(typeof(CanvasRenderer))]
public class MenuArtwork : MaskableGraphic
{
    public bool grapple;
    VertexHelper vertices;
    static readonly Color Amber=new Color(0.94f,0.69f,0.26f),Steel=new Color(0.24f,0.33f,0.38f),Glass=new Color(0.11f,0.21f,0.27f);
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();vertices=vh;
        var faint=new Color(0.32f,0.46f,0.5f,0.14f);
        for(int row=0;row<5;row++)for(int i=0;i<20;i++)
        {
            float x=i/20f,x2=(i+1)/20f;
            Line(new Vector2(x,0.12f+row*0.16f+Mathf.Sin(x*9+row)*0.045f),new Vector2(x2,0.12f+row*0.16f+Mathf.Sin(x2*9+row)*0.045f),0.002f,faint);
        }
        Poly(Steel,new Vector2(.18f,.14f),new Vector2(.14f,.2f),new Vector2(.18f,.28f),new Vector2(.52f,.28f),new Vector2(.57f,.2f),new Vector2(.53f,.14f));
        for(int i=0;i<12;i++)Line(new Vector2(.19f+i*.028f,.15f),new Vector2(.19f+i*.028f,.24f),.006f,new Color(.43f,.51f,.52f));
        Poly(Amber,new Vector2(.19f,.3f),new Vector2(.19f,.47f),new Vector2(.29f,.51f),new Vector2(.49f,.45f),new Vector2(.51f,.3f));
        Poly(Steel,new Vector2(.29f,.44f),new Vector2(.29f,.8f),new Vector2(.43f,.8f),new Vector2(.49f,.45f));
        Poly(Glass,new Vector2(.31f,.49f),new Vector2(.31f,.76f),new Vector2(.405f,.76f),new Vector2(.455f,.49f));
        Line(new Vector2(.37f,.49f),new Vector2(.37f,.76f),.008f,Steel);
        Line(new Vector2(.47f,.48f),new Vector2(.62f,.89f),.04f,Amber);
        Line(new Vector2(.62f,.89f),new Vector2(.82f,.55f),.032f,Amber);
        Line(new Vector2(.49f,.45f),new Vector2(.6f,.78f),.01f,Steel);
        Line(new Vector2(.64f,.82f),new Vector2(.8f,.53f),.009f,Steel);
        Line(new Vector2(.82f,.55f),new Vector2(.86f,.4f),.021f,Steel);
        if(!grapple)
        {
            Poly(Amber,new Vector2(.82f,.44f),new Vector2(.9f,.4f),new Vector2(.93f,.24f),new Vector2(.78f,.25f));
            for(int i=0;i<4;i++)Line(new Vector2(.8f+i*.035f,.25f),new Vector2(.8f+i*.035f,.21f),.009f,Steel);
            Line(new Vector2(.76f,.13f),new Vector2(.96f,.13f),.005f,Amber);
        }
        else
        {
            Poly(Amber,new Vector2(.83f,.44f),new Vector2(.86f,.41f),new Vector2(.79f,.23f),new Vector2(.77f,.28f));
            Poly(Amber,new Vector2(.86f,.41f),new Vector2(.89f,.44f),new Vector2(.96f,.28f),new Vector2(.94f,.23f));
            Poly(Steel,new Vector2(.83f,.12f),new Vector2(.9f,.12f),new Vector2(.92f,.2f),new Vector2(.87f,.25f),new Vector2(.82f,.21f));
        }
    }
    Vector2 Point(Vector2 p){var r=rectTransform.rect;return new Vector2(r.xMin+p.x*r.width,r.yMin+p.y*r.height);}
    void Poly(Color c,params Vector2[] p)
    {int start=vertices.currentVertCount;foreach(var point in p)vertices.AddVert(Point(point),c,Vector2.zero);for(int i=1;i<p.Length-1;i++)vertices.AddTriangle(start,start+i,start+i+1);}
    void Line(Vector2 a,Vector2 b,float width,Color c)
    {var delta=b-a;var side=new Vector2(-delta.y,delta.x).normalized*width*.5f;Poly(c,a-side,b-side,b+side,a+side);}
}
