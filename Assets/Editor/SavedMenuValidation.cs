using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SavedMenuValidation
{
    public static void Run()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Menu/LayoutExport.unity")?"Assets/Menu/LayoutExport.unity":"Assets/Scenes/Menu.unity");
            var view=UnityEngine.Object.FindFirstObjectByType<ExcavatorMenuPresentation>();
            Require(view && view.HasSavedLayout,"Saved presentation bindings missing");
            int count=scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Length);
            Require(count>200,"Worksite is not saved in the scene");
            foreach(var filter in view.GetComponentsInChildren<MeshFilter>(true))Require(AssetDatabase.Contains(filter.sharedMesh),"Transient geometry: "+filter.name);
            foreach(var renderer in view.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)Require(material && AssetDatabase.Contains(material),"Transient material: "+renderer.name);
            foreach(var label in view.GetComponentsInChildren<TMP_Text>(true))Require(label.font && AssetDatabase.Contains(label.font) && label.font.atlasTextures.All(t=>AssetDatabase.Contains(t)),"Transient font: "+label.name);
            view.Build();view.Build();
            Require(count==scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Length),"Runtime binding rebuilt the saved hierarchy");
            Require(view.Yard.Site.gameObject.activeInHierarchy && view.Yard.Machines.All(m=>m.activeInHierarchy),"Worksite hidden outside Play");
            var serialized=new SerializedObject(view.excavation);Require(serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls.Array.data[0].m_Arguments.m_StringArgument").stringValue=="Level 1","Excavation level binding lost");
            serialized=new SerializedObject(view.precision);Require(serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls.Array.data[0].m_Arguments.m_StringArgument").stringValue=="Level 2","Grapple level binding lost");
            foreach(var machine in view.Yard.Machines){machine.GetComponent<MenuMachineChoice>().Initialize();Require(machine.GetComponents<BoxCollider>().Length==1,"Duplicate selectable collider");}
            view.HelpButton.onClick.Invoke();Require(view.HelpVisible && !view.excavation.interactable,"Help binding failed");
            view.CloseHelpButton.onClick.Invoke();Require(!view.HelpVisible && view.excavation.interactable,"Close help binding failed");
            view.ShowLoading("Level 1");view.SetProgress(.5f);Require(!view.excavation.interactable,"Loading does not block level selection");view.RestoreAfterLoadFailure("Test complete");
            var rendererData=ScriptableObject.CreateInstance<UniversalRendererData>();var pipeline=UniversalRenderPipelineAsset.Create(rendererData);
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            var camera=new GameObject("Validation viewer",typeof(Camera)).GetComponent<Camera>();camera.transform.position=new Vector3(0,1.6f,-.45f);camera.fieldOfView=78;camera.nearClipPlane=.05f;camera.farClipPlane=70;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.35f,.4f);view.canvas.worldCamera=camera;
            ShaderUtil.allowAsyncCompilation=false;Canvas.ForceUpdateCanvases();var target=new RenderTexture(1440,900,24);camera.targetTexture=target;camera.Render();camera.Render();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});RenderTexture.active=target;var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();System.IO.File.WriteAllBytes("C:/Temp/VR-saved-menu.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
            Debug.Log("SAVED MENU PASS: visible authored hierarchy, persistent geometry/materials/fonts, no duplicate generation, level events, help/loading, selectable machines and URP render.");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
