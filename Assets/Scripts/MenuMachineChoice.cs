using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// The machine itself is a 3D menu choice, with the same guarded action as its physical sign.
public class MenuMachineChoice : MonoBehaviour
{
    public Button button;
    public MenuYard yard;
    public int bay;
    public void Initialize()
    {
        var bounds=new Bounds();bool first=true;
        foreach(var filter in GetComponentsInChildren<MeshFilter>())
        {
            if(!filter.sharedMesh)continue;var b=filter.sharedMesh.bounds;var frame=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {var p=frame.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
        }
        var collider=gameObject.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
        var interactable=gameObject.AddComponent<XRSimpleInteractable>();if(!interactable.colliders.Contains(collider))interactable.colliders.Add(collider);
        interactable.hoverEntered.AddListener(_=>SetHover(true));interactable.hoverExited.AddListener(_=>SetHover(false));interactable.selectEntered.AddListener(_=>Choose());
    }
    public void SetHover(bool hover){if(yard)yard.SetMachineHover(bay,hover && button && button.IsInteractable());}
    public void Choose(){if(button && button.IsInteractable())button.onClick.Invoke();}
    void OnDisable(){if(yard)yard.SetMachineHover(bay,false);}
}
