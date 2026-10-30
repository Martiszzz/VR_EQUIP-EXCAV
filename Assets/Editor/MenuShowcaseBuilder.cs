using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class MenuShowcaseBuilder
{
    static readonly Dictionary<(Mesh,int),Mesh> slices=new Dictionary<(Mesh,int),Mesh>();
    public static void Run()
    {
        try{Build("Level 1","Bucket");Build("Level 2","Grapple");AssetDatabase.SaveAssets();EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Build(string level,string label)
    {
        Directory.CreateDirectory("Assets/Menu/Models");AssetDatabase.Refresh();
        string source=File.ReadAllText("Assets/Scenes/"+level+".unity");
        var blocks=Regex.Matches(source,@"(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)").Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
        var transforms=blocks.Where(b=>b.Value.StartsWith("--- !u!4 ") && b.Value.Contains("m_Father:")).ToDictionary(b=>Match(b.Value,@"m_GameObject: \{fileID: (\d+)"),b=>b.Key);
        var geometry=new RealBucketWorkValidation.Geometry(source);var root=geometry.Node("1113508163246883981");
        var groups=new Dictionary<Color,List<CombineInstance>>();
        var model=new GameObject(label+" excavator display");
        try
        {
            foreach(var filter in blocks.Values.Where(b=>b.Contains("MeshFilter:")))
            {
                string go=Match(filter,@"m_GameObject: \{fileID: (\d+)");if(!transforms.ContainsKey(go))continue;
                var node=geometry.Node(transforms[go]);if(!node.IsChildOf(root) || !Active(go,blocks,transforms))continue;
                var mesh=Resolve<Mesh>(Match(filter,@"m_Mesh: \{fileID: (-?\d+)"),Match(filter,@"m_Mesh: \{fileID: -?\d+, guid: ([a-f0-9]+)"));
                if(!mesh)continue;
                string renderer=blocks.Values.FirstOrDefault(b=>b.Contains("MeshRenderer:") && b.Contains("m_GameObject: {fileID: "+go+"}"));
                if(renderer==null || renderer.Contains("  m_Enabled: 0"))continue;
                string name=Match(blocks[go],@"m_Name: ([^\r\n]+)");node.name=name;
                var meshFilter=node.gameObject.AddComponent<MeshFilter>();meshFilter.sharedMesh=mesh;
                var materials=Regex.Matches(Regex.Match(renderer,@"(?ms)  m_Materials:.*?(?=  m_StaticBatchInfo:)").Value,@"fileID: (-?\d+), guid: ([a-f0-9]+)").Cast<Match>().Select(m=>Resolve<Material>(m.Groups[1].Value,m.Groups[2].Value)).ToArray();
                bool template=InAncestor(node,geometry.Node("1113508163246883981"),"1810314630");
                if(template){node.gameObject.AddComponent<MeshRenderer>();node.gameObject.AddComponent<MenuModelPart>().tone=new Color(.1f,.13f,.14f);continue;}
                for(int slot=0;slot<mesh.subMeshCount;slot++)Add(groups,Tone(slot<materials.Length?materials[slot]:null,name),mesh,slot,Matrix4x4.TRS(-root.position,Quaternion.identity,Vector3.one)*node.localToWorldMatrix);
            }
            if(level=="Level 1")
            {
                var drive=root.gameObject.AddComponent<TrackDrive>();
                foreach(var b in blocks.Values.Where(b=>b.Contains("Assembly-CSharp::TrackChain")))
                {
                    string go=Match(b,@"m_GameObject: \{fileID: (\d+)");var parent=geometry.Node(transforms[go]);var chain=parent.gameObject.AddComponent<TrackChain>();chain.drive=drive;
                    chain.linkPrefab=geometry.Node(Match(b,@"linkPrefab: \{fileID: (\d+)"));chain.linkCount=60;
                    string path=b.Substring(b.IndexOf("  path:"));path=path.Substring(0,path.IndexOf("  linkPrefab:"));
                    chain.path=Regex.Matches(path,@"fileID: (\d+)").Cast<Match>().Select(m=>geometry.Node(m.Groups[1].Value)).ToArray();chain.Build();
                    foreach(var f in parent.GetComponentsInChildren<MeshFilter>())for(int i=0;i<f.sharedMesh.subMeshCount;i++)Add(groups,new Color(.1f,.13f,.14f),f.sharedMesh,i,Matrix4x4.TRS(-root.position,Quaternion.identity,Vector3.one)*f.transform.localToWorldMatrix);
                }
            }
            int index=0;
            foreach(var pair in groups)
            {
                var mesh=new Mesh {name=label+" display section "+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string asset="Assets/Menu/Models/"+label+"-"+index+".asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(asset))AssetDatabase.DeleteAsset(asset);AssetDatabase.CreateAsset(mesh,asset);
                var go=new GameObject("Display section "+index);go.transform.SetParent(model.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");go.AddComponent<MenuModelPart>().tone=pair.Key;index++;
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(model,"Assets/Menu/"+label+"Showcase.prefab");AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab,out string guid,out long fileId);
            Debug.Log($"MENU MODEL {label}: guid={guid} fileID={fileId} batches={groups.Count}");
            File.WriteAllText("C:/Temp/VR-menu-"+label+"-ref.txt",fileId+" "+guid);
        }
        finally{UnityEngine.Object.DestroyImmediate(model);geometry.Dispose();}
    }
    static bool InAncestor(Transform node,Transform root,string name){for(var t=node;t && t!=root;t=t.parent)if(t.name==name)return true;return false;}
    static void Add(Dictionary<Color,List<CombineInstance>> groups,Color c,Mesh mesh,int submesh,Matrix4x4 matrix)
    {
        if(!groups.TryGetValue(c,out var list))groups[c]=list=new List<CombineInstance>();
        if(!slices.TryGetValue((mesh,submesh),out var slice))
        {
            var source=mesh.vertices;var normals=mesh.normals;var indices=mesh.GetTriangles(submesh);var remap=new Dictionary<int,int>();var vertices=new List<Vector3>();var vertexNormals=new List<Vector3>();
            for(int i=0;i<indices.Length;i++)
            {
                int old=indices[i];if(!remap.TryGetValue(old,out int next)){next=vertices.Count;remap[old]=next;vertices.Add(source[old]);vertexNormals.Add(normals.Length>old?normals[old]:Vector3.up);}indices[i]=next;
            }
            slice=new Mesh {indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};slice.SetVertices(vertices);slice.SetNormals(vertexNormals);slice.SetTriangles(indices,0);slices[(mesh,submesh)]=slice;
        }
        list.Add(new CombineInstance {mesh=slice,subMeshIndex=0,transform=matrix});
    }
    static string Match(string text,string pattern)=>Regex.Match(text,pattern).Groups[1].Value;
    static T Resolve<T>(string file,string guid) where T:UnityEngine.Object
    {
        if(string.IsNullOrEmpty(guid))return null;string path=AssetDatabase.GUIDToAssetPath(guid);if(string.IsNullOrEmpty(path))throw new Exception("Missing model asset "+guid);
        long wanted=long.Parse(file);return AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault(a=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a,out string g,out long id);return id==wanted;});
    }
    static bool Active(string go,Dictionary<string,string> b,Dictionary<string,string> t)
    {
        for(int i=0;i<40 && go!="0";i++)
        {
            if(b[go].Contains("  m_IsActive: 0"))return false;
            if(!t.ContainsKey(go))break;string parent=Match(b[t[go]],@"m_Father: \{fileID: (\d+)");if(parent=="0")break;go=Match(b[parent],@"m_GameObject: \{fileID: (\d+)");
        }
        return true;
    }
    static Color Tone(Material mat,string name)
    {
        string n=(mat?mat.name:"").ToLowerInvariant();string part=name.ToLowerInvariant();
        if(n.Contains("glass") || n.Contains("window"))return new Color(.29f,.49f,.55f,.45f);
        if(part.Contains("track") || n.Contains("rubber") || n.Contains("black"))return new Color(.09f,.12f,.13f);
        if(part.Contains("cylinder") || n.Contains("metal") || n.Contains("chrome"))return new Color(.43f,.49f,.5f);
        Color c=mat && mat.HasProperty("_BaseColor")?mat.GetColor("_BaseColor"):mat && mat.HasProperty("_Color")?mat.color:Color.gray;
        if(c.a<.8f)return new Color(.29f,.49f,.55f,.45f);
        if(c.r>c.g*1.35f && c.r>.4f)return new Color(.87f,.43f,.1f);
        if(c.maxColorComponent<.3f)return new Color(.12f,.16f,.18f);
        if(c.maxColorComponent>.8f && Mathf.Abs(c.r-c.b)<.15f)return new Color(.66f,.7f,.69f);
        return new Color(.8f,.48f,.16f);
    }
}
