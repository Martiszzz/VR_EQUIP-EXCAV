using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// A stationary, real-scale VR worksite. Only the signs use UI; the yard and machines have real depth.
public class MenuYard : MonoBehaviour
{
    public Transform Site {get;private set;}
    public GameObject[] Machines {get;private set;}=new GameObject[2];
    public Transform[] Stands {get;private set;}=new Transform[2];
    public bool IsBuilt {get;private set;}
    readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
    readonly List<Mesh> meshes=new List<Mesh>();
    readonly Renderer[][] focusRails=new Renderer[2][];
    readonly Light[] focusLights=new Light[2];
    MaterialPropertyBlock block;
    readonly bool[] machineHover=new bool[2];
    ExcavatorMenuPresentation view;
    Color amber=new Color(.94f,.59f,.16f),steel=new Color(.15f,.21f,.24f),stone=new Color(.32f,.33f,.29f);
    public void Build(ExcavatorMenuPresentation presentation,TMP_FontAsset font,GameObject bucket,GameObject grapple)
    {
        if(IsBuilt || !bucket || !grapple)return;IsBuilt=true;view=presentation;block=new MaterialPropertyBlock();
        Site=new GameObject("3D menu worksite").transform;Site.SetParent(transform,false);Site.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var canvas=(RectTransform)view.canvas.transform;canvas.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        foreach(Transform child in view.Root)
            if(child!=view.excavation.transform && child!=view.precision.transform && child!=view.HelpButton.transform && child!=view.QuitButton.transform && child.name!="Controls overlay" && child.name!="Loading overlay")child.gameObject.SetActive(false);
        if(view.environmentFloor)view.environmentFloor.enabled=false;
        if(view.environmentLight){view.environmentLight.intensity=1.2f;view.environmentLight.shadows=LightShadows.Soft;view.environmentLight.transform.rotation=Quaternion.Euler(38,-28,0);}
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.56f,.59f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.25f,.35f,.4f);RenderSettings.fogDensity=.014f;
        var ground=Box("Yard ground",new Vector3(0,-.08f,5),new Vector3(25,.16f,28),new Color(.65f,.68f,.65f));
        var groundMaterial=ground.GetComponent<Renderer>().sharedMaterial;var texture=SoilClodVisual.GroundLayer.diffuseTexture;
        groundMaterial.mainTexture=texture;groundMaterial.mainTextureScale=new Vector2(16,16);
        if(groundMaterial.HasProperty("_BaseMap")){groundMaterial.SetTexture("_BaseMap",texture);groundMaterial.SetTextureScale("_BaseMap",new Vector2(16,16));}
        Box("Arrival apron",new Vector3(0,.008f,1.3f),new Vector3(5,.016f,3.8f),new Color(.34f,.37f,.35f));
        for(int i=-6;i<=6;i++)Box("Concrete joint",new Vector3(i*1.6f,.013f,5),new Vector3(.012f,.008f,22),new Color(.2f,.25f,.24f));
        for(int i=-2;i<12;i++)Box("Cross joint",new Vector3(0,.015f,i*1.6f),new Vector3(23,.008f,.012f),new Color(.2f,.25f,.24f));
        for(int i=0;i<7;i++)Box("Arrival centerline",new Vector3(0,.022f,.3f+i*.42f),new Vector3(.045f,.01f,.22f),new Color(.8f,.67f,.33f));
        Bay(0,new Vector3(-1.65f,0,5.1f));Bay(1,new Vector3(1.65f,0,5.1f));
        Machines[0]=Machine(bucket,new Vector3(-1.65f,.12f,5.15f),145);
        Machines[1]=Machine(grapple,new Vector3(1.65f,.12f,5.15f),-145);
        for(int bay=0;bay<2;bay++){var choice=Machines[bay].AddComponent<MenuMachineChoice>();choice.yard=this;choice.bay=bay;choice.button=bay==0?view.excavation:view.precision;choice.Initialize();}
        Fence();BackWall();Console();
        for(int i=0;i<4;i++)Cone(new Vector3(i<2?-3.35f:3.35f,0,i%2==0?3.5f:6.65f));
        Pile(new Vector3(-2.5f,.08f,6.2f),.65f,.32f);Pile(new Vector3(-.7f,.08f,6.7f),.55f,.25f);
        for(int i=0;i<3;i++)Box("Workpiece",new Vector3(1.7f+(i%2)*.32f,.2f+i*.15f,6.1f),new Vector3(.6f,.18f,.35f),new Color(.46f,.34f,.19f));
        if(view.menuSounds && view.yardAmbience)
        {
            var ambience=new GameObject("Distant engine ambience");ambience.transform.SetParent(Site,false);ambience.transform.localPosition=new Vector3(0,.7f,5.5f);
            var source=ambience.AddComponent<AudioSource>();source.clip=view.yardAmbience;source.loop=true;source.playOnAwake=false;source.volume=.035f;source.spatialBlend=1;source.minDistance=1.5f;source.maxDistance=16;source.dopplerLevel=0;if(Application.isPlaying)source.Play();
        }
        Header(font);
        PlaceCard(view.excavation,0,new Vector3(-1.75f,1.02f,3.05f),-20,"01 / KASIMAS","Kaušas · žemės · reljefas");
        PlaceCard(view.precision,1,new Vector3(1.75f,1.02f,3.05f),20,"02 / GRIEBTUVAS","Rotatorius · tikslūs judesiai");
        Utility(view.HelpButton,new Vector3(-.25f,.87f,1.48f),320);
        Utility(view.QuitButton,new Vector3(.3f,.87f,1.48f),200);
        foreach(string name in new[]{"Controls overlay","Loading overlay"})
        {
            var overlay=(RectTransform)view.Root.Find(name);overlay.pivot=new Vector2(.5f,.5f);overlay.position=new Vector3(0,1.35f,1.95f);overlay.localRotation=Quaternion.identity;overlay.localScale=Vector3.one*.82f;
        }
        var status=view.Status;if(status){status.gameObject.SetActive(true);PlaceText(status,new Vector3(0,.96f,1.48f),650,28,1);status.rectTransform.localRotation=Quaternion.Euler(25,0,0);status.fontSize=16;status.color=new Color(.7f,.78f,.78f);status.alignment=TextAlignmentOptions.Center;status.text="RINKIS TECHNIKĄ ARBA STENDĄ";}
        Site.gameObject.SetActive(!Application.isPlaying);
    }
    public void Reveal(){if(Site)Site.gameObject.SetActive(true);}
    public void AlignToViewer(Camera camera)
    {
        if(!Site || !camera){Reveal();return;}
        var forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)forward=Vector3.forward;
        var origin=camera.transform.position;origin.y=camera.transform.root.position.y;
        var rotation=Quaternion.LookRotation(forward,Vector3.up);Site.SetPositionAndRotation(origin,rotation);view.canvas.transform.SetPositionAndRotation(origin,rotation);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.35f,.4f);Reveal();
    }
    public void SetMachineHover(int bay,bool hover){if(bay<0 || bay>1 || !view)return;machineHover[bay]=hover;Focus(bay==0?view.excavation:view.precision,0);}
    public void Focus(Button button,float amount)
    {
        int bay=button==view.excavation?0:button==view.precision?1:-1;if(bay<0)return;
        if((bay==0?view.excavation:view.precision).IsInteractable() && machineHover[bay])amount=1;
        if(focusLights[bay])focusLights[bay].intensity=amount*1.5f;
        block.SetColor("_BaseColor",Color.Lerp(steel,amber,amount));block.SetColor("_Color",Color.Lerp(steel,amber,amount));block.SetColor("_EmissionColor",amber*amount*.45f);
        if(focusRails[bay]!=null)foreach(var rail in focusRails[bay])if(rail)rail.SetPropertyBlock(block);
    }
    void Bay(int index,Vector3 center)
    {
        Box("Work bay",center+Vector3.up*.04f,new Vector3(2.9f,.08f,3.6f),stone);
        var rails=new List<Renderer>();
        foreach(float side in new[]{-1f,1f})
        {
            rails.Add(Box("Bay edge",center+new Vector3(side*1.47f,.09f,0),new Vector3(.06f,.08f,3.66f),steel).GetComponent<Renderer>());
            for(int i=0;i<10;i++)Box("Safety marking",center+new Vector3(side*1.35f,.085f,-1.5f+i*.32f),new Vector3(.16f,.014f,.16f),i%2==0?amber:new Color(.12f,.16f,.17f));
        }
        focusRails[index]=rails.ToArray();
        var go=new GameObject("Bay focus light");go.transform.SetParent(Site,false);go.transform.localPosition=center+new Vector3(0,2.4f,-.3f);var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=4;light.color=new Color(1,.72f,.4f);light.intensity=0;light.shadows=LightShadows.None;focusLights[index]=light;
    }
    GameObject Machine(GameObject prefab,Vector3 position,float yaw)
    {
        var model=Instantiate(prefab,Site);model.name=prefab.name;model.transform.localPosition=position;model.transform.localRotation=Quaternion.Euler(0,yaw,0);
        foreach(var part in model.GetComponentsInChildren<MenuModelPart>())
        {
            var renderer=part.GetComponent<MeshRenderer>();if(!renderer)continue;renderer.sharedMaterial=Material(part.tone);
            if(part.tone.a<.9f)renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        var bounds=BoundsOf(model);model.transform.localScale*=1.7f/Mathf.Max(.1f,bounds.size.y);bounds=BoundsOf(model);
        model.transform.position+=Site.TransformPoint(position)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        return model;
    }
    public static Bounds BoundsOf(GameObject go)
    {var renderers=go.GetComponentsInChildren<MeshRenderer>();var bounds=new Bounds(go.transform.position,Vector3.zero);if(renderers.Length>0){bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);}return bounds;}
    void PlaceCard(Button button,int index,Vector3 point,float yaw,string title,string description)
    {
        var rect=(RectTransform)button.transform;rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(650,240);rect.position=point;rect.localRotation=Quaternion.Euler(0,yaw,0);
        foreach(Transform child in rect)child.gameObject.SetActive(false);
        var panel=(RectTransform)rect.Find("Card surface");panel.gameObject.SetActive(true);panel.sizeDelta=rect.sizeDelta;
        Label(button,"Level title",title,26,23,598,48,34);
        Label(button,"Description",description,26,88,598,42,24);
        Label(button,"Call to action","Pradėti darbą",26,166,530,44,28);
        Label(button,"Arrow","→",565,157,52,50,36);
        button.GetComponent<MenuButtonFeedback>().Initialize();
        var stand=new GameObject(index==0?"Excavation selection stand":"Precision selection stand").transform;stand.SetParent(Site,false);stand.localPosition=point;stand.localRotation=Quaternion.Euler(0,yaw,0);Stands[index]=stand;
        Part(stand,"Solid sign housing",new Vector3(0,0,.055f),new Vector3(1.1f,.43f,.1f),steel);
        for(int i=-1;i<=1;i+=2)Part(stand,"Support",new Vector3(i*.32f,-.55f,.08f),new Vector3(.045f,.82f,.06f),steel);
        Part(stand,"Stand foot",new Vector3(0,-.99f,.08f),new Vector3(1,.035f,.5f),new Color(.13f,.18f,.2f));
        Part(stand,"Amber identification rail",new Vector3(0,.24f,.055f),new Vector3(1.1f,.025f,.1f),amber);
    }
    void Label(Button button,string childName,string value,float x,float y,float w,float h,float size)
    {var label=button.transform.Find(childName).GetComponent<TMP_Text>();label.gameObject.SetActive(true);label.text=value;label.fontSize=size;var rect=label.rectTransform;rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);}
    void Utility(Button button,Vector3 point,float width)
    {
        var r=(RectTransform)button.transform;r.pivot=new Vector2(.5f,.5f);r.position=point;r.localRotation=Quaternion.Euler(25,0,0);r.sizeDelta=new Vector2(width,64);
        var label=button.GetComponentInChildren<TMP_Text>();label.rectTransform.anchoredPosition=new Vector2(17,-16);label.rectTransform.sizeDelta=new Vector2(width-34,36);label.fontSize=19;button.GetComponent<MenuButtonFeedback>().Initialize();
    }
    void Header(TMP_FontAsset font)
    {
        Text3D("Yard brand","EQUIP / EKSKAVATORIAUS SIMULIATORIUS",font,new Vector3(0,3.18f,6.9f),24,new Vector2(1600,90),.0025f,new Color(.85f,.74f,.5f));
        Text3D("Worksite title","DARBO AIKŠTELĖ",font,new Vector3(0,2.86f,6.88f),86,new Vector2(1600,110),.003f,new Color(.91f,.94f,.91f));
        Text3D("Worksite subtitle","Tavo kita pamaina prasideda čia.",font,new Vector3(0,2.57f,6.88f),25,new Vector2(1600,75),.0028f,new Color(.66f,.75f,.74f));
    }
    void Text3D(string name,string value,TMP_FontAsset font,Vector3 point,float size,Vector2 dimensions,float scale,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(view.Root,false);var r=(RectTransform)go.transform;r.sizeDelta=dimensions;r.pivot=new Vector2(.5f,.5f);r.position=point;r.localScale=Vector3.one*(scale/.0016f);r.localRotation=Quaternion.identity;
        var text=go.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
    }
    void PlaceText(TMP_Text text,Vector3 point,float width,float height,float scale)
    {text.rectTransform.pivot=new Vector2(.5f,.5f);text.rectTransform.position=point;text.rectTransform.localRotation=Quaternion.identity;text.rectTransform.sizeDelta=new Vector2(width,height);text.rectTransform.localScale=Vector3.one*scale;}
    void Console()
    {
        Box("Console pedestal",new Vector3(0,.39f,1.62f),new Vector3(.16f,.78f,.22f),steel);
        var face=Box("Control desk",new Vector3(0,.86f,1.57f),new Vector3(1.12f,.24f,.12f),steel);face.transform.localRotation=Quaternion.Euler(25,0,0);
        Box("Console foot",new Vector3(0,.025f,1.65f),new Vector3(.65f,.05f,.5f),steel);
    }
    void BackWall()
    {
        Box("Header backing",new Vector3(0,2.91f,7.1f),new Vector3(7,.95f,.15f),steel);
        for(int i=-1;i<=1;i+=2)Box("Header upright",new Vector3(i*3.55f,1.7f,7.1f),new Vector3(.12f,3.4f,.12f),steel);
        for(int i=-1;i<=1;i+=2)
        {
            var container=Box("Storage container",new Vector3(i*5.5f,1.05f,9),new Vector3(3.3f,2.1f,3.6f),new Color(.25f,.34f,.34f));
            for(int rib=0;rib<16;rib++)Box("Container rib",new Vector3(i*5.5f-1.5f+rib*.2f,1.05f,7.17f),new Vector3(.035f,2,.035f),new Color(.19f,.28f,.29f));
        }
    }
    void Fence()
    {
        for(int side=-1;side<=1;side+=2)for(int z=-2;z<=14;z+=2)
        {
            Box("Fence post",new Vector3(side*8,0.7f,z),new Vector3(.07f,1.4f,.07f),steel);
            for(int rail=0;rail<3;rail++)Box("Fence rail",new Vector3(side*8,.35f+rail*.4f,z+1),new Vector3(.035f,.035f,2),steel);
        }
        for(int i=-8;i<=8;i+=2)
        {Box("Rear fence post",new Vector3(i,.7f,14),new Vector3(.07f,1.4f,.07f),steel);for(int rail=0;rail<3;rail++)Box("Rear rail",new Vector3(i+1,.35f+rail*.4f,14),new Vector3(2,.035f,.035f),steel);}
    }
    void Cone(Vector3 p)
    {
        Box("Cone foot",p+Vector3.up*.025f,new Vector3(.32f,.05f,.32f),new Color(.12f,.15f,.16f));
        var mesh=new Mesh {name="Safety cone"};var v=new List<Vector3>();var orange=new List<int>();var white=new List<int>();
        float[] rings={0,.15f,.2f,.27f,.31f,.42f};
        foreach(float y in rings)for(int i=0;i<12;i++){float angle=i*Mathf.PI*2/12;float r=Mathf.Lerp(.13f,.025f,y/.42f);v.Add(new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r));}
        for(int band=0;band<rings.Length-1;band++)for(int i=0;i<12;i++)
        {int a=band*12+i,b=band*12+(i+1)%12,c=a+12,d=b+12;(band==2?white:orange).AddRange(new[]{a,c,b,b,c,d});}
        mesh.SetVertices(v);mesh.subMeshCount=2;mesh.SetTriangles(orange,0);mesh.SetTriangles(white,1);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
        var go=new GameObject("Safety cone");go.transform.SetParent(Site,false);go.transform.localPosition=p+Vector3.up*.05f;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=new[]{Material(amber),Material(new Color(.82f,.84f,.77f))};
    }
    void Pile(Vector3 p,float radius,float height)
    {
        var mesh=new Mesh {name="Earth mound"};var v=new Vector3[18];v[0]=Vector3.up*height;
        for(int i=0;i<17;i++){float a=i*Mathf.PI*2/16;v[i+1]=new Vector3(Mathf.Cos(a)*radius*(1+Mathf.Sin(i*7)*.08f),0,Mathf.Sin(a)*radius);}
        var tri=new List<int>();for(int i=1;i<17;i++)tri.AddRange(new[]{0,i+1,i});mesh.vertices=v;mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);MeshObject("Earth pile",mesh,p,new Color(.36f,.26f,.15f));
    }
    GameObject Box(string name,Vector3 position,Vector3 size,Color color)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(Site,false);go.transform.localPosition=position;go.transform.localScale=size;var collider=go.GetComponent<Collider>();if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);go.GetComponent<Renderer>().sharedMaterial=Material(color);return go;}
    void Part(Transform parent,string name,Vector3 position,Vector3 size,Color color)
    {var go=Box(name,Vector3.zero,size,color);go.transform.SetParent(parent,false);go.transform.localPosition=position;}
    void MeshObject(string name,Mesh mesh,Vector3 position,Color color)
    {var go=new GameObject(name);go.transform.SetParent(Site,false);go.transform.localPosition=position;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Material(color);}
    Material Material(Color color)
    {
        if(materials.TryGetValue(color,out var material))return material;
        var shader=Shader.Find(GraphicsSettings.currentRenderPipeline?"Universal Render Pipeline/Lit":"Standard");material=new Material(shader){name="Menu yard surface",color=color,enableInstancing=true};
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",color.a<.9f?.65f:.2f);
        if(color.a<.9f)
        {
            if(material.HasProperty("_Surface"))material.SetFloat("_Surface",1);if(material.HasProperty("_Mode"))material.SetFloat("_Mode",3);material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.EnableKeyword("_ALPHABLEND_ON");material.renderQueue=3000;
        }
        materials[color]=material;return material;
    }
    void OnDestroy(){foreach(var m in materials.Values)Dispose(m);foreach(var m in meshes)Dispose(m);if(Site)Dispose(Site.gameObject);}
    static void Dispose(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
}
