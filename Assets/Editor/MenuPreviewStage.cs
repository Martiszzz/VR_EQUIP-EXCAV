using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Preview geometry lives in a separate, unsaved stage, never in the user's level or Menu scene.
public class MenuPreviewStage : PreviewSceneStage
{
    public ExcavatorMenuPresentation Source {get;set;}
    public ExcavatorMenuPresentation Preview {get;private set;}
    GameObject content,canvasObject;
    protected override GUIContent CreateHeaderContent()=>new GUIContent("3D Menu Preview",EditorGUIUtility.IconContent("SceneAsset Icon").image,"Temporary preview. Return with the stage back arrow.");
    protected override bool OnOpenStage()
    {
        if(!base.OnOpenStage())return false;
        try
        {
            content=new GameObject("3D Menu Preview (not saved)");SceneManager.MoveGameObjectToScene(content,scene);
            canvasObject=new GameObject("Preview world canvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(canvasObject,scene);canvasObject.transform.SetParent(content.transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            Button left=Button(canvas.transform,"Excavation preview"),right=Button(canvas.transform,"Grapple preview");
            var controller=new GameObject("Preview presentation");controller.transform.SetParent(content.transform,false);
            Preview=controller.AddComponent<ExcavatorMenuPresentation>();Preview.editorPreview=true;Preview.immersiveYard=true;Preview.alignWithHead=false;Preview.menuSounds=false;
            Preview.canvas=canvas;Preview.excavation=left;Preview.precision=right;
            Preview.sourceFont=Source && Source.sourceFont?Source.sourceFont:AssetDatabase.LoadAssetAtPath<Font>("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf");
            Preview.bucketShowcase=Source && Source.bucketShowcase?Source.bucketShowcase:AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Menu/BucketShowcase.prefab");
            Preview.grappleShowcase=Source && Source.grappleShowcase?Source.grappleShowcase:AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Menu/GrappleShowcase.prefab");
            var sun=new GameObject("Preview sunlight");sun.transform.SetParent(content.transform,false);var light=sun.AddComponent<Light>();light.type=LightType.Directional;Preview.environmentLight=light;
            Preview.Build();if(!Preview.IsBuilt || !Preview.Yard || !Preview.Yard.IsBuilt)throw new InvalidOperationException("The 3D menu preview requires its font and both showcase prefabs.");
            Preview.Yard.Reveal();Canvas.ForceUpdateCanvases();
            return true;
        }
        catch(Exception error){Debug.LogException(error);return false;}
    }
    public void FramePlayerView(SceneView view)
    {
        if(!view || !Preview)return;
        view.sceneLighting=true;view.orthographic=false;
        view.LookAt(new Vector3(0,1.1f,4.5f),Quaternion.Euler(7,0,0),2.65f,false,true);
        Preview.canvas.worldCamera=view.camera;view.Repaint();
    }
    protected override void OnCloseStage()
    {
        if(content)DestroyImmediate(content);Preview=null;content=null;canvasObject=null;
        base.OnCloseStage();
    }
    static Button Button(Transform parent,string name)
    {var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);return go.GetComponent<Button>();}
}

[InitializeOnLoad]
public static class MenuPreviewEditor
{
    static MenuPreviewEditor()
    {
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Close();};
        AssemblyReloadEvents.beforeAssemblyReload+=Close;
    }
    [MenuItem("Tools/Excavator/Preview 3D Menu")]
    public static void Open()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var source=UnityEngine.Object.FindObjectsByType<ExcavatorMenuPresentation>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .FirstOrDefault(view=>!EditorSceneManager.IsPreviewScene(view.gameObject.scene));
        Open(source);
    }
    public static void Open(ExcavatorMenuPresentation source)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Close();var stage=ScriptableObject.CreateInstance<MenuPreviewStage>();stage.Source=source && !source.editorPreview?source:null;
        StageUtility.GoToStage(stage,true);
        var view=SceneView.lastActiveSceneView;if(!view)view=EditorWindow.GetWindow<SceneView>();
        stage.FramePlayerView(view);view.Focus();
    }
    [MenuItem("Tools/Excavator/Back to Scene")]
    public static void Close(){if(StageUtility.GetCurrentStage() is MenuPreviewStage)StageUtility.GoToMainStage();}
    [MenuItem("Tools/Excavator/Preview Controls Guide")]
    static void Help()
    {
        if(!(StageUtility.GetCurrentStage() is MenuPreviewStage))Open();
        if(StageUtility.GetCurrentStage() is MenuPreviewStage stage && stage.Preview)stage.Preview.ShowHelp(!stage.Preview.HelpVisible);
        SceneView.RepaintAll();
    }
}

[CustomEditor(typeof(ExcavatorMenuPresentation))]
public class ExcavatorMenuPresentationInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();EditorGUILayout.Space();
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("Preview 3D Menu",GUILayout.Height(30)))MenuPreviewEditor.Open((ExcavatorMenuPresentation)target);
        }
        EditorGUILayout.HelpBox("Preview the worksite in Scene view without entering Play Mode. The preview is temporary and does not change or save your scene.",MessageType.Info);
    }
}
