using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MenuPreviewValidation
{
    public static void Run(){SessionState.SetBool("RunMenuPreviewValidation",true);Wait();}
    [InitializeOnLoadMethod]static void Wait(){if(SessionState.GetBool("RunMenuPreviewValidation",false))EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;EditorApplication.update-=Ready;SessionState.SetBool("RunMenuPreviewValidation",false);
        try{Check();Debug.Log("EDITOR MENU PREVIEW PASS: real preview stage, complete 3D worksite without Play, help, no source-scene or environment changes, repeatable open/close and no leftover objects.");EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Check()
    {
        var sourceScene=SceneManager.GetActiveScene();int roots=sourceScene.GetRootGameObjects().Length;bool dirty=sourceScene.isDirty;
        bool fog=RenderSettings.fog;Color fogColor=RenderSettings.fogColor,ambient=RenderSettings.ambientLight;AmbientMode mode=RenderSettings.ambientMode;
        for(int cycle=0;cycle<2;cycle++)
        {
            MenuPreviewEditor.Open();Require(StageUtility.GetCurrentStage() is MenuPreviewStage,"Scene view never enters the preview stage");
            var stage=(MenuPreviewStage)StageUtility.GetCurrentStage();var previewScene=stage.scene;
            Require(EditorSceneManager.IsPreviewScene(previewScene) && previewScene!=sourceScene,"Menu preview was placed in the user's original scene");
            Require(stage.Preview && stage.Preview.Yard && stage.Preview.Yard.Machines[0] && stage.Preview.Yard.Machines[1],"Editor preview is missing real machines");
            Require(!Application.isPlaying && stage.Preview.Yard.Site.gameObject.activeInHierarchy,"Editor preview requires Play Mode");
            Require(stage.Preview.Yard.Site.GetComponentsInChildren<MenuMachineChoice>().Length==0,"Preview registers live gameplay interactions");
            stage.Preview.ShowHelp(true);Require(stage.Preview.HelpVisible,"Controls guide cannot be previewed");stage.Preview.ShowHelp(false);
            Require(RenderSettings.fog==fog && RenderSettings.fogColor==fogColor && RenderSettings.ambientLight==ambient && RenderSettings.ambientMode==mode,"Editor preview changes the source environment");
            MenuPreviewEditor.Close();Require(!(StageUtility.GetCurrentStage() is MenuPreviewStage),"Cannot return from preview");
            Require(!previewScene.IsValid() || !previewScene.isLoaded,"Preview scene stays loaded after closing");
            Require(sourceScene.GetRootGameObjects().Length==roots && sourceScene.isDirty==dirty,"Preview changes or dirties the user's scene");
        }
    }
}
