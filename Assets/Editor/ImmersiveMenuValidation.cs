using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ImmersiveMenuValidation
{
    public static void Run(){SessionState.SetBool("RunImmersiveMenuValidation",true);Wait();}
    [InitializeOnLoadMethod]static void Wait(){if(SessionState.GetBool("RunImmersiveMenuValidation",false))EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;EditorApplication.update-=Ready;SessionState.SetBool("RunImmersiveMenuValidation",false);
        try{MenuPresentationValidation.Check();Check();Debug.Log("IMMERSIVE MENU PASS: real scene-derived 3D machines, compact meshes, physical stands, perspective/parallax, usable angled sign raycasts, focus lighting, help, loading and scene bindings.");EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Check()
    {
        var owner=new GameObject("Immersive menu fixture");var canvasObject=new GameObject("VR world canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
        var events=new GameObject("XR UI events",typeof(EventSystem));typeof(EventSystem).GetMethod("OnEnable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(events.GetComponent<EventSystem>(),null);EventSystem.current=events.GetComponent<EventSystem>();
        var rig=new GameObject("Viewer rig");var cameraObject=new GameObject("Viewer camera",typeof(Camera));cameraObject.transform.SetParent(rig.transform,false);cameraObject.transform.localPosition=new Vector3(0,1.6f,-.45f);
        var lightObject=new GameObject("Sun",typeof(Light));var sun=lightObject.GetComponent<Light>();sun.type=LightType.Directional;
        try
        {
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;var camera=cameraObject.GetComponent<Camera>();camera.fieldOfView=78;camera.nearClipPlane=.05f;camera.farClipPlane=70;canvas.worldCamera=camera;
            var left=Button(canvas.transform,"Level 1");var right=Button(canvas.transform,"Level 2");int selected=0;left.onClick.AddListener(()=>selected=1);right.onClick.AddListener(()=>selected=2);
            owner.AddComponent<LevelMenu>();var view=owner.AddComponent<ExcavatorMenuPresentation>();view.canvas=canvas;view.excavation=left;view.precision=right;view.environmentLight=sun;
            view.sourceFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Inter-Regular.ttf");if(!view.sourceFont)view.sourceFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf");
            view.bucketShowcase=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Menu/BucketShowcase.prefab");view.grappleShowcase=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Menu/GrappleShowcase.prefab");view.immersiveYard=true;view.alignWithHead=false;view.Build();
            Require(view.Yard && view.Yard.IsBuilt,"3D menu worksite not constructed");view.Yard.AlignToViewer(camera);
            foreach(var machine in view.Yard.Machines)
            {
                Require(machine && machine.GetComponentsInChildren<MeshRenderer>().Length>=5,"Actual excavator geometry missing");
                var bounds=MenuYard.BoundsOf(machine);Require(bounds.size.y>1.6f && bounds.size.y<1.8f,"Display machine is not life-size mini-excavator scale");
                Require(machine.GetComponentsInChildren<Rigidbody>().Length==0 && machine.GetComponentsInChildren<PartDriver>().Length==0 && machine.GetComponentsInChildren<DigTeeth>().Length==0,"Display machine accidentally runs level physics");
                int vertices=machine.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount);Require(vertices<100000,$"Display geometry is unnecessarily heavy: {vertices}");
            }
            Require(Vector3.Distance(view.Yard.Machines[0].transform.position,left.transform.position)>1,"Machine is a flat card illustration instead of a separate 3D object");
            Require(Quaternion.Angle(left.transform.rotation,right.transform.rotation)>30,"Selection signs share a flat screen plane");
            Require(view.Root.Find("Main panel").gameObject.activeSelf==false,"Old flat menu still covers the worksite");
            Canvas.ForceUpdateCanvases();Layout(view);
            camera.transform.LookAt(view.Yard.Site.TransformPoint(new Vector3(0,1,4.7f)));
            Screenshot(camera,"C:/Temp/VR-menu-yard.png");
            Hit(canvas,camera,left);Hit(canvas,camera,right);
            var feedback=left.GetComponent<MenuButtonFeedback>();feedback.OnPointerEnter(new PointerEventData(EventSystem.current));feedback.Step(.3f);
            Require(view.Yard.Site.GetComponentsInChildren<Light>().Any(l=>l.intensity>0),"Pointing at a stand does not highlight its work bay");
            left.onClick.Invoke();Require(selected==1,"Left stand no longer starts its original level action");right.onClick.Invoke();Require(selected==2,"Right stand no longer starts its original level action");
            var choice=view.Yard.Machines[0].GetComponent<MenuMachineChoice>();Require(choice && choice.GetComponent<BoxCollider>(),"The actual 3D machine is not selectable");choice.Choose();Require(selected==1,"Selecting the machine does not start its matching scene action");
            Vector3 site=view.Yard.Site.position;var orientation=view.Yard.Site.rotation;camera.transform.localPosition+=new Vector3(.55f,.05f,.15f);camera.transform.LookAt(view.Yard.Site.TransformPoint(new Vector3(0,1.2f,5)));
            Screenshot(camera,"C:/Temp/VR-menu-yard-side.png");Require(view.Yard.Site.position==site && view.Yard.Site.rotation==orientation,"Worksite follows the head instead of remaining in the world");
            camera.transform.localPosition=new Vector3(0,1.6f,-.45f);camera.transform.localRotation=Quaternion.identity;
            view.HelpButton.onClick.Invoke();selected=0;choice.Choose();Require(selected==0,"3D machine can start a level while help is open");Require(view.HelpVisible && !left.interactable && !right.interactable,"Help allows accidental level selection around its edge");Layout(view);Screenshot(camera,"C:/Temp/VR-menu-yard-help.png");
            view.CloseHelpButton.onClick.Invoke();Require(!view.HelpVisible && left.interactable && right.interactable,"Physical controls desk cannot return to level selection");
            view.ShowLoading("Level 1");view.SetProgress(.72f);Layout(view);Screenshot(camera,"C:/Temp/VR-menu-yard-loading.png");
            Require(!left.interactable && !right.interactable,"Loading does not lock spatial stand actions");
            var source=File.ReadAllText("Assets/Scenes/Menu.unity");Require(source.Contains("immersiveYard: 1") && source.Contains("7f4a7ad3e9c23bb40bd429fc89b44691") && source.Contains("16b12abbc83c90b4a9a05b67895de539"),"Menu scene is not bound to its real 3D showcases");
        }
        finally{UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(canvasObject);UnityEngine.Object.DestroyImmediate(events);UnityEngine.Object.DestroyImmediate(rig);UnityEngine.Object.DestroyImmediate(lightObject);}
    }
    static void Hit(Canvas canvas,Camera camera,Button button)
    {
        var pointer=new PointerEventData(EventSystem.current){position=camera.WorldToScreenPoint(button.transform.position)};var results=new List<RaycastResult>();canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,results);
        if(results.Count==0){var graphic=button.targetGraphic;Debug.Log($"SIGN RAY viewport={camera.WorldToViewportPoint(button.transform.position)}, depth={graphic.depth}, cull={graphic.canvasRenderer.cull}, registered={GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas).Count}, inside={RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform,pointer.position,camera)}");}
        Require(results.Any(r=>r.gameObject.transform.IsChildOf(button.transform)),"Angled stand cannot be selected by a pointer ray: "+button.name);
    }
    static void Layout(ExcavatorMenuPresentation view)
    {
        Canvas.ForceUpdateCanvases();foreach(var t in view.Root.GetComponentsInChildren<TMP_Text>())
        {t.ForceMeshUpdate(true,true);Require(t.textBounds.size.y<=t.rectTransform.rect.height+3,"3D sign text does not fit: "+t.name);Require(t.textBounds.size.x<=t.rectTransform.rect.width+3,"3D sign text is too wide: "+t.name);}
    }
    static Button Button(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);return go.GetComponent<Button>();}
    static void Screenshot(Camera camera,string path)
    {
        var old=RenderTexture.active;var rt=new RenderTexture(1800,1100,24);var image=new Texture2D(1800,1100,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1800,1100),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
}
