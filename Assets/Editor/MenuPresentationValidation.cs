using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class MenuPresentationValidation
{
    public static void Run(){SessionState.SetBool("RunMenuPresentationValidation",true);Wait();}
    [InitializeOnLoadMethod]static void Wait(){if(SessionState.GetBool("RunMenuPresentationValidation",false))EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        EditorApplication.update-=Ready;SessionState.SetBool("RunMenuPresentationValidation",false);
        try{Check();Debug.Log("MENU PRESENTATION PASS: preserved buttons, XR raycaster, world-space sizing, English labels, readable layout, hover, help/focus, loading and recovery.");EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Check()
    {
        var go=new GameObject("Menu fixture");var canvasObject=new GameObject("World menu",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
        var events=new GameObject("Menu events",typeof(EventSystem));typeof(EventSystem).GetMethod("OnEnable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(events.GetComponent<EventSystem>(),null);EventSystem.current=events.GetComponent<EventSystem>();var cameraObject=new GameObject("Menu preview camera",typeof(Camera));
        try
        {
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var level1=Button(canvas.transform,"Original level 1");var level2=Button(canvas.transform,"Original level 2");
            var originalChild=new GameObject("Old heading",typeof(RectTransform));originalChild.transform.SetParent(canvas.transform,false);
            int calls=0;level1.onClick.AddListener(()=>calls++);
            go.AddComponent<LevelMenu>();var view=go.AddComponent<ExcavatorMenuPresentation>();view.canvas=canvas;view.excavation=level1;view.precision=level2;
            view.sourceFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Inter-Regular.ttf");if(!view.sourceFont)view.sourceFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf");
            view.alignWithHead=false;view.Build();view.Build();
            Require(view.IsBuilt && !originalChild.activeSelf,"Menu not built or old heading remains visible");
            Require(go.GetComponents<AudioSource>().Length==1 && canvas.GetComponentsInChildren<MenuArtwork>().Length==2,"Menu rebuild duplicates presentation");
            level1.onClick.Invoke();Require(calls==1,"Original scene action was replaced or duplicated");
            Require(canvas.renderMode==RenderMode.WorldSpace && Mathf.Abs(canvas.transform.lossyScale.x*1100-1.76f)<0.01f,"Menu world size is uncomfortable");
            var camera=cameraObject.GetComponent<Camera>();camera.transform.position=new Vector3(0,1.5f,-1);camera.transform.rotation=Quaternion.identity;
            camera.orthographic=true;camera.orthographicSize=.665f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.047f,.065f);canvas.worldCamera=camera;
            Layout(view);
            foreach(var art in view.Root.GetComponentsInChildren<MenuArtwork>())
            {var mesh=art.canvasRenderer.GetMesh();Require(mesh && mesh.vertexCount>100 && !art.canvasRenderer.cull,"A level illustration is not visible");}
            Screenshot(camera,"C:/Temp/VR-menu-main.png");
            var feedback=level1.GetComponent<MenuButtonFeedback>();var rect=(RectTransform)level1.transform;var position=rect.anchoredPosition;
            feedback.OnPointerEnter(new PointerEventData(EventSystem.current));feedback.Step(.2f);
            Require(rect.anchoredPosition.y>position.y && rect.anchoredPosition.y<position.y+5,"Menu focus moves excessively or is absent");
            Screenshot(camera,"C:/Temp/VR-menu-focus.png");feedback.OnPointerExit(null);feedback.Step(1);
            view.HelpButton.onClick.Invoke();Require(view.HelpVisible && EventSystem.current.currentSelectedGameObject==view.CloseHelpButton.gameObject,"Help does not open with usable focus");
            Layout(view);Screenshot(camera,"C:/Temp/VR-menu-help.png");
            view.CloseHelpButton.onClick.Invoke();Require(!view.HelpVisible && EventSystem.current.currentSelectedGameObject==view.HelpButton.gameObject,"Closing help loses controller focus");
            view.ShowLoading("Level 1");view.SetProgress(.65f);Require(!level1.interactable && !level2.interactable && !view.HelpButton.interactable,"Loading allows repeated actions");
            Layout(view);Screenshot(camera,"C:/Temp/VR-menu-loading.png");
            view.RestoreAfterLoadFailure("Please try again.");Require(level1.interactable && level2.interactable && view.Status.text.Contains("try again"),"Failed load cannot recover");
            string source=File.ReadAllText("Assets/Scenes/Menu.unity");Require(source.Contains("Assembly-CSharp::ExcavatorMenuPresentation") && source.Contains("TrackedDeviceGraphicRaycaster") && source.Contains("m_StringArgument: Level 1") && source.Contains("m_StringArgument: Level 2"),"Menu scene loses its XR raycaster or level actions");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(canvasObject);UnityEngine.Object.DestroyImmediate(events);UnityEngine.Object.DestroyImmediate(cameraObject);}
    }
    static void Layout(ExcavatorMenuPresentation view)
    {
        Canvas.ForceUpdateCanvases();
        foreach(var text in view.Root.GetComponentsInChildren<TMP_Text>())
        {
            text.ForceMeshUpdate(true,true);
            Require(!System.Text.RegularExpressions.Regex.IsMatch(text.text,"[ąčęėįšųūžĄČĘĖĮŠŲŪŽ]"),"Menu contains untranslated text: "+text.text);
            Require(text.textBounds.size.y<text.rectTransform.rect.height+3,"Text exceeds vertical bounds: "+text.name+" / "+text.text);
            Require(text.textBounds.size.x<text.rectTransform.rect.width+3,"Text exceeds horizontal bounds: "+text.name);
            foreach(char c in text.text)if(!char.IsWhiteSpace(c))Require(text.font.HasCharacter(c,true,true),"Missing menu glyph: "+c);
            Require(!text.raycastTarget,"A decorative label intercepts controller rays");
        }
    }
    static Button Button(Transform parent,string name)
    {var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);return go.GetComponent<Button>();}
    static void Screenshot(Camera camera,string path)
    {
        var old=RenderTexture.active;var render=new RenderTexture(1600,1050,24);var texture=new Texture2D(1600,1050,TextureFormat.RGB24,false);
        try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;texture.ReadPixels(new Rect(0,0,1600,1050),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(texture);}
    }
}
