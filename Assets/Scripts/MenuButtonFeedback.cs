using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuButtonFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler,IPointerDownHandler,IPointerUpHandler
{
    public ExcavatorMenuPresentation presentation;
    public MenuPanelGraphic panel;
    public Color normal=new Color(.08f,.13f,.17f),highlight=new Color(.12f,.19f,.23f);
    Button button;RectTransform rect;Vector2 home;bool hovered,selected,pressed;float amount;
    public void Initialize(){button=GetComponent<Button>();rect=(RectTransform)transform;home=rect.anchoredPosition;}
    public void Step(float dt)
    {
        if(!rect)Initialize();
        bool active=button && button.IsInteractable() && (hovered || selected);
        amount=Mathf.MoveTowards(amount,active?1:0,dt*7);
        if(panel){panel.color=Color.Lerp(normal,highlight,amount);panel.SetBorder(Color.Lerp(new Color(.2f,.28f,.32f),new Color(.94f,.69f,.26f),amount));}
        rect.anchoredPosition=home+Vector2.up*(amount*3);
        rect.localScale=Vector3.one*(pressed && active?.992f:1);
    }
    void Update(){Step(Time.unscaledDeltaTime);}
    public void OnPointerEnter(PointerEventData e){hovered=true;if(button && button.IsInteractable() && presentation)presentation.PlayFeedback(false);}
    public void OnPointerExit(PointerEventData e){hovered=false;pressed=false;}
    public void OnSelect(BaseEventData e){selected=true;}
    public void OnDeselect(BaseEventData e){selected=pressed=false;}
    public void OnPointerDown(PointerEventData e){pressed=true;}
    public void OnPointerUp(PointerEventData e){pressed=false;}
    void OnDisable(){hovered=selected=pressed=false;if(rect){rect.anchoredPosition=home;rect.localScale=Vector3.one;}}
}
