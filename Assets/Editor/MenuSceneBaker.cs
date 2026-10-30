using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Author the menu as ordinary scene objects and persistent resources, not a runtime preview.
public static class MenuSceneBaker
{
    const string AssetsFolder="Assets/Menu/SceneAssets";
    public static void Run(){SessionState.SetBool("BakeSavedMenu",true);Wait();}
    [InitializeOnLoadMethod]static void Wait(){if(SessionState.GetBool("BakeSavedMenu",false))EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;EditorApplication.update-=Ready;SessionState.SetBool("BakeSavedMenu",false);
        try{Bake();Debug.Log("SAVED MENU BAKE PASS: complete editable hierarchy and persistent resources exported.");EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Bake()
    {
        Directory.CreateDirectory(AssetsFolder);AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("3D Menu");var controller=root.AddComponent<LevelMenu>();
        var canvasGo=new GameObject("World Space Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(TrackedDeviceGraphicRaycaster));canvasGo.transform.SetParent(root.transform,false);
        var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
        var xr=new SerializedObject(canvasGo.GetComponent<TrackedDeviceGraphicRaycaster>());xr.FindProperty("m_IgnoreReversedGraphics").boolValue=false;xr.FindProperty("m_CheckFor3DOcclusion").boolValue=false;xr.ApplyModifiedPropertiesWithoutUndo();
        Button left=Button(canvas.transform,"Excavation stand"),right=Button(canvas.transform,"Grapple stand");
        UnityEventTools.AddStringPersistentListener(left.onClick,controller.Load,"Level 1");UnityEventTools.AddStringPersistentListener(right.onClick,controller.Load,"Level 2");
        var view=root.AddComponent<ExcavatorMenuPresentation>();view.canvas=canvas;view.excavation=left;view.precision=right;view.immersiveYard=true;view.alignWithHead=false;view.menuSounds=true;
        view.sourceFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Inter-Regular.ttf");if(!view.sourceFont)view.sourceFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf");
        view.bucketShowcase=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Menu/BucketShowcase.prefab");view.grappleShowcase=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Menu/GrappleShowcase.prefab");view.yardAmbience=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Excavator/engine-idle-loop.wav");
        var sun=new GameObject("Worksite sunlight");sun.transform.SetParent(root.transform,false);view.environmentLight=sun.AddComponent<Light>();view.environmentLight.type=LightType.Directional;sun.AddComponent<UniversalAdditionalLightData>();
        view.Build();if(!view.IsBuilt || !view.Yard)throw new Exception("Unable to create saved menu");
        // Remove hidden remnants of the old full-screen layout, keeping the help/loading overlays.
        foreach(Transform child in view.Root.Cast<Transform>().ToArray())if(!child.gameObject.activeSelf && child.name!="Controls overlay" && child.name!="Loading overlay")UnityEngine.Object.DestroyImmediate(child.gameObject);
        foreach(var button in new[]{left,right})foreach(Transform child in button.transform.Cast<Transform>().ToArray())if(!child.gameObject.activeSelf)UnityEngine.Object.DestroyImmediate(child.gameObject);
        Canvas.ForceUpdateCanvases();
        var font=view.Status.font;string chars=new string(Enumerable.Range(32,95).Select(x=>(char)x).ToArray())+"·→";font.TryAddCharacters(chars);
        AssetDatabase.CreateAsset(font,AssetsFolder+"/Menu Font.asset");
        foreach(var atlas in font.atlasTextures)if(atlas && !AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,font);
        if(!AssetDatabase.Contains(font.material))AssetDatabase.AddObjectToAsset(font.material,font);
        font.atlasPopulationMode=AtlasPopulationMode.Static;EditorUtility.SetDirty(font);
        int textureId=0,materialId=0,meshId=0;
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        foreach(var material in renderer.sharedMaterials)
        {
            if(!material || AssetDatabase.Contains(material))continue;
            Color color=material.color;Texture texture=material.mainTexture;Vector2 tiling=material.mainTextureScale;
            material.shader=Shader.Find("Universal Render Pipeline/Lit");material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",color.a<.9f?.65f:.2f);
            if(texture)
            {
                if(!AssetDatabase.Contains(texture)){texture.name="Menu ground grain";AssetDatabase.CreateAsset(texture,AssetsFolder+"/Texture-"+(textureId++)+".asset");}
                material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",tiling);
            }
            if(color.a<.9f){material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;}
            material.name="Worksite material "+materialId;AssetDatabase.CreateAsset(material,AssetsFolder+"/Material-"+(materialId++)+".mat");
        }
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh && !AssetDatabase.Contains(filter.sharedMesh))
        {filter.sharedMesh.name="Worksite detail "+meshId;AssetDatabase.CreateAsset(filter.sharedMesh,AssetsFolder+"/Geometry-"+(meshId++)+".asset");}
        view.SaveLayoutBindings();RenderSettings.skybox=null;RenderSettings.sun=view.environmentLight;
        AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene,"Assets/Menu/LayoutExport.unity"))throw new Exception("Unable to save menu hierarchy");
        Debug.Log($"SAVED MENU RESOURCES: {materialId} materials, {meshId} detail meshes, {textureId} textures; root objects {scene.rootCount}");
    }
    static Button Button(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);return go.GetComponent<Button>();}
}
