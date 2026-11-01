using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ExcavatorMenuPresentation : MonoBehaviour
{
    [System.Serializable]
    class SavedLayout
    {
        public RectTransform root,help,loading,progress;
        public CanvasGroup group;
        public Button helpButton,closeHelp,quit;
        public TMP_Text status,progressText;
        public MenuYard yard;
    }
    [SerializeField] SavedLayout savedLayout;
    public bool HasSavedLayout=>savedLayout!=null && savedLayout.root;
    public Canvas canvas;
    public Button excavation,precision;
    public Font sourceFont;
    public bool immersiveYard;
    [System.NonSerialized] public bool editorPreview;
    public GameObject bucketShowcase,grappleShowcase;
    public AudioClip yardAmbience;
    public MenuYard Yard {get;private set;}
    public Renderer environmentFloor;
    public Light environmentLight;
    Material floorMaterial;
    bool ownsFont;
    public bool menuSounds=true,alignWithHead=true;
    public float viewingDistance=2.3f;
    public bool IsBuilt {get;private set;}
    public bool HelpVisible=>helpRoot && helpRoot.gameObject.activeSelf;
    public RectTransform Root {get;private set;}
    public TMP_Text Status=>status;
    public Button HelpButton {get;private set;}
    public Button CloseHelpButton {get;private set;}
    public Button QuitButton {get;private set;}
    public CanvasGroup Group {get;private set;}
    RectTransform helpRoot,loadingRoot,progressFill;
    TMP_Text status,progressText;
    TMP_FontAsset font;
    AudioSource sound;
    AudioClip hoverClip,clickClip;
    LevelMenu controller;
    float age,lastSound=-1;
    bool loading,anchorReady;
    static readonly Color Ink=new Color(.055f,.085f,.11f),Panel=new Color(.08f,.13f,.17f),Amber=new Color(.94f,.69f,.26f),White=new Color(.92f,.95f,.94f),Muted=new Color(.55f,.65f,.68f);
    void Awake(){Build();}
    IEnumerator Start()
    {
        if(!alignWithHead)
        {
            anchorReady=true;if(Yard)Yard.Reveal();
            var initialCamera=Camera.main;
            if(initialCamera && canvas){canvas.worldCamera=initialCamera;if(Yard){initialCamera.clearFlags=CameraClearFlags.SolidColor;initialCamera.backgroundColor=new Color(.25f,.35f,.4f);}}
            yield break;
        }
        yield return new WaitForSecondsRealtime(.35f);
        var camera=Camera.main;
        if(camera && canvas)
        {
            canvas.worldCamera=camera;
            if(Yard){Yard.AlignToViewer(camera);anchorReady=true;yield break;}
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.065f,.085f);
            var forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f)forward=Vector3.forward;
            canvas.transform.SetPositionAndRotation(camera.transform.position+forward*viewingDistance-Vector3.up*.08f,Quaternion.LookRotation(forward,Vector3.up));
        }
        if(Yard)Yard.Reveal();
        anchorReady=true;
    }
    public void Build()
    {
        if(IsBuilt || !canvas || !excavation || !precision)return;
        controller=GetComponent<LevelMenu>();
        if(HasSavedLayout){BindSavedLayout();return;}
        if(sourceFont){font=TMP_FontAsset.CreateFontAsset(sourceFont,48,6,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024);ownsFont=font!=null;}
        if(!font)font=excavation.GetComponentInChildren<TMP_Text>(true)?.font;
        if(!font)font=TMP_Settings.defaultFontAsset;
        if(!font){Debug.LogError("Menu needs a text font",this);return;}
        IsBuilt=true;
        if(environmentFloor && environmentFloor.sharedMaterial)
        {
            floorMaterial=new Material(environmentFloor.sharedMaterial);
            if(floorMaterial.HasProperty("_BaseColor"))floorMaterial.SetColor("_BaseColor",new Color(.08f,.13f,.16f));
            if(floorMaterial.HasProperty("_Color"))floorMaterial.SetColor("_Color",new Color(.08f,.13f,.16f));
            if(floorMaterial.HasProperty("_EmissionColor"))floorMaterial.SetColor("_EmissionColor",new Color(.025f,.045f,.055f));
            environmentFloor.sharedMaterial=floorMaterial;
        }
        if(environmentLight){environmentLight.color=new Color(1,.85f,.64f);environmentLight.intensity=.8f;}
        var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(1100,720);
        canvasRect.localScale=Vector3.one*.0016f;canvasRect.position=new Vector3(0,1.5f,viewingDistance);canvasRect.rotation=Quaternion.identity;
        foreach(Transform child in canvas.transform)if(child!=excavation.transform && child!=precision.transform)child.gameObject.SetActive(false);
        Root=Rect("Excavator menu",canvas.transform,0,0,1100,720);Root.anchorMin=Root.anchorMax=Root.pivot=new Vector2(.5f,.5f);Root.anchoredPosition=Vector2.zero;
        Group=Root.gameObject.AddComponent<CanvasGroup>();
        var shadow=PanelAt("Panel shadow",Root,-8,8,1116,724,new Color(0,0,0,.28f),22);shadow.raycastTarget=false;
        PanelAt("Main panel",Root,0,0,1100,720,Ink,20);
        PanelAt("Brand mark",Root,44,42,7,24,Amber,2).raycastTarget=false;
        Text("Brand",Root,"EQUIP / EXCAVATOR SIMULATOR",64,40,650,30,18,White,true);
        PanelAt("Online indicator",Root,876,48,7,7,new Color(.45f,.83f,.67f),4).raycastTarget=false;
        Text("Ready",Root,"READY TO WORK",894,39,160,28,15,new Color(.57f,.75f,.67f));
        Rule(Root,44,90,1012);
        Text("Title",Root,"Your next shift.",44,111,1012,64,48,White,true);
        Text("Subtitle",Root,"Choose your machine and get to work.",46,180,1008,34,22,Muted);
        Card(excavation,false,44,"01","EXCAVATION YARD","Dig, move soil and shape the terrain.","BUCKET");
        Card(precision,true,560,"02","PRECISION WORK","Rotate, grab and position objects.","GRAPPLE");
        Rule(Root,44,594,1012);
        HelpButton=SmallButton("Controls",Root,"Controls guide",44,617,258,49);HelpButton.onClick.AddListener(()=>ShowHelp(!HelpVisible));
        Text("Pointer hint",Root,"Point your controller and press the trigger",324,623,530,38,17,Muted);
        QuitButton=SmallButton("Quit",Root,"Exit",916,617,140,49);if(controller)QuitButton.onClick.AddListener(controller.Quit);
        status=Text("Footer status",Root,"CHOOSE A LEVEL TO BEGIN",44,683,1012,20,12,new Color(.38f,.49f,.53f));
        BuildHelp();BuildLoading();
        LinkNavigation();
        if(immersiveYard && bucketShowcase && grappleShowcase){Yard=gameObject.AddComponent<MenuYard>();Yard.Build(this,font,bucketShowcase,grappleShowcase);}
        sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.volume=.12f;sound.dopplerLevel=0;
        hoverClip=Tone("Menu focus",520,.045f);clickClip=Tone("Menu select",740,.07f);
        anchorReady=!alignWithHead || !Application.isPlaying;
        Group.alpha=Application.isPlaying?0:1;Group.interactable=!Application.isPlaying;
    }
    // Called by the editor baker once; the hierarchy and resource references are then normal scene data.
    public void SaveLayoutBindings()
    {
        savedLayout=new SavedLayout {root=Root,help=helpRoot,loading=loadingRoot,progress=progressFill,group=Group,helpButton=HelpButton,closeHelp=CloseHelpButton,quit=QuitButton,status=status,progressText=progressText,yard=Yard};
        ownsFont=false;floorMaterial=null;if(Yard)Yard.SaveLayoutBindings();
        if(Group){Group.alpha=1;Group.interactable=true;}
    }
    void BindSavedLayout()
    {
        Root=savedLayout.root;helpRoot=savedLayout.help;loadingRoot=savedLayout.loading;progressFill=savedLayout.progress;Group=savedLayout.group;
        HelpButton=savedLayout.helpButton;CloseHelpButton=savedLayout.closeHelp;QuitButton=savedLayout.quit;status=savedLayout.status;progressText=savedLayout.progressText;Yard=savedLayout.yard;
        IsBuilt=true;loading=false;age=0;ownsFont=false;anchorReady=!alignWithHead || !Application.isPlaying;
        if(Yard)Yard.BindSavedLayout(this);
        helpRoot.gameObject.SetActive(false);loadingRoot.gameObject.SetActive(false);
        excavation.interactable=precision.interactable=HelpButton.interactable=QuitButton.interactable=true;
        foreach(var feedback in Root.GetComponentsInChildren<MenuButtonFeedback>(true)){feedback.presentation=this;feedback.Initialize();}
        HelpButton.onClick.AddListener(()=>ShowHelp(!HelpVisible));CloseHelpButton.onClick.AddListener(()=>ShowHelp(false));if(controller)QuitButton.onClick.AddListener(controller.Quit);
        foreach(var button in new[]{excavation,precision,HelpButton,CloseHelpButton,QuitButton})button.onClick.AddListener(()=>PlayFeedback(true));
        sound=GetComponent<AudioSource>();if(!sound)sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.volume=.12f;
        hoverClip=Tone("Menu focus",520,.045f);clickClip=Tone("Menu select",740,.07f);
        Group.alpha=Application.isPlaying?0:1;Group.interactable=!Application.isPlaying;
    }
    void Card(Button button,bool grapple,float x,string number,string title,string description,string badge)
    {
        button.transform.SetParent(Root,false);var rect=(RectTransform)button.transform;Position(rect,x,240,496,326);
        foreach(Transform child in button.transform)child.gameObject.SetActive(false);
        var old=button.GetComponent<Image>();if(old){old.raycastTarget=false;old.enabled=false;}
        var panel=PanelAt("Card surface",button.transform,0,0,496,326,Panel,13);panel.raycastTarget=true;
        button.targetGraphic=panel;button.transition=Selectable.Transition.None;
        var art=Rect("Machine illustration",button.transform,14,11,468,148).gameObject.AddComponent<MenuArtwork>();art.grapple=grapple;art.raycastTarget=false;art.SetAllDirty();
        Text("Attachment",button.transform,badge,22,20,180,24,12,Muted,true);
        Rule(button.transform,22,169,452);
        Text("Number",button.transform,number,22,185,38,32,17,Amber,true);
        Text("Level title",button.transform,title,64,181,410,40,24,White,true);
        Text("Description",button.transform,description,22,226,452,46,18,Muted);
        Text("Call to action",button.transform,"Start work",22,285,380,30,18,Amber,true);
        Text("Arrow",button.transform,"→",431,279,44,36,29,Amber);
        var feedback=button.gameObject.AddComponent<MenuButtonFeedback>();feedback.presentation=this;feedback.panel=panel;feedback.Initialize();
        button.onClick.AddListener(()=>PlayFeedback(true));
    }
    void BuildHelp()
    {
        helpRoot=Rect("Controls overlay",Root,0,0,1100,720);PanelAt("Controls backdrop",helpRoot,0,0,1100,720,Ink,20);
        Text("Help eyebrow",helpRoot,"BEFORE YOU START",44,42,1012,30,17,Amber,true);
        Text("Help heading",helpRoot,"Get seated. Start up. Take control.",44,100,1012,65,40,White,true);
        Text("Help intro",helpRoot,"Get comfortable in your seat before operating the machine.",44,179,1012,42,21,Muted);
        HelpRow(255,"01","GRIP A LEVER","Grip a lever with your VR controller. Move it gently; a larger tilt increases movement speed.");
        HelpRow(353,"02","START THE ENGINE","Turn the ignition key. Use the throttle to adjust engine speed and hydraulic power.");
        HelpRow(451,"03","CONTROL THE ATTACHMENT","The thumbstick works while you hold the main joystick. Each level uses a different attachment.");
        CloseHelpButton=SmallButton("Close controls",helpRoot,"Back to level selection",44,617,300,49);CloseHelpButton.onClick.AddListener(()=>ShowHelp(false));helpRoot.gameObject.SetActive(false);
    }
    void HelpRow(float y,string number,string heading,string body)
    {Text("Step "+number,helpRoot,number,44,y,65,44,27,Amber,true);Text("Heading "+number,helpRoot,heading,121,y,900,32,20,White,true);Text("Body "+number,helpRoot,body,121,y+36,900,56,20,Muted);}
    void BuildLoading()
    {
        loadingRoot=Rect("Loading overlay",Root,0,0,1100,720);PanelAt("Loading backdrop",loadingRoot,0,0,1100,720,Ink,20);
        Text("Loading eyebrow",loadingRoot,"PREPARING YOUR WORKSITE",70,245,960,40,18,Amber,true);
        Text("Loading title",loadingRoot,"Almost ready.",70,299,960,66,46,White,true);
        PanelAt("Progress track",loadingRoot,70,415,960,5,new Color(.18f,.26f,.3f),2);
        progressFill=Rect("Progress fill",loadingRoot,70,415,1,5);var image=progressFill.gameObject.AddComponent<Image>();image.color=Amber;image.raycastTarget=false;
        progressText=Text("Progress label",loadingRoot,"0 %",70,443,960,36,20,Muted);loadingRoot.gameObject.SetActive(false);
    }
    void LinkNavigation()
    {
        var nav=new Navigation {mode=Navigation.Mode.Explicit,selectOnRight=precision,selectOnDown=HelpButton};excavation.navigation=nav;
        nav.selectOnRight=null;nav.selectOnLeft=excavation;nav.selectOnDown=QuitButton;precision.navigation=nav;
        HelpButton.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=excavation,selectOnRight=QuitButton};
        QuitButton.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=precision,selectOnLeft=HelpButton};
    }
    public void ShowHelp(bool show)
    {
        if(!IsBuilt || loading)return;
        helpRoot.gameObject.SetActive(show);
        excavation.interactable=precision.interactable=HelpButton.interactable=QuitButton.interactable=true;
        if(show)
        {
            // The yard title is created after the overlay. Draw the opaque guide last, and put it
            // in front of the physical console so its Close button is not behind a 3D collider.
            helpRoot.SetAsLastSibling();
            var backdrop=helpRoot.Find("Controls backdrop");
            if(backdrop && backdrop.TryGetComponent<MenuPanelGraphic>(out var panel)){panel.color=Ink;panel.raycastTarget=true;}
            var camera=canvas.worldCamera?canvas.worldCamera:Camera.main;
            if(Yard && camera)
            {
                var forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
                if(forward.sqrMagnitude<.5f)forward=Vector3.forward;
                helpRoot.position=camera.transform.position+forward*1.15f-Vector3.up*.05f;
                helpRoot.rotation=Quaternion.LookRotation(forward,Vector3.up);
            }
        }
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(show?CloseHelpButton.gameObject:HelpButton.gameObject);
    }
    public void ShowLoading(string scene)
    {
        if(!IsBuilt)return;loading=true;Group.interactable=false;helpRoot.gameObject.SetActive(false);loadingRoot.SetAsLastSibling();loadingRoot.gameObject.SetActive(true);
        excavation.interactable=precision.interactable=HelpButton.interactable=QuitButton.interactable=false;
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        SetProgress(0);
    }
    public void SetProgress(float amount)
    {if(!progressFill)return;amount=Mathf.Clamp01(amount);progressFill.sizeDelta=new Vector2(960*amount,5);progressText.text=Mathf.RoundToInt(amount*100)+" %";}
    public void RestoreAfterLoadFailure(string message)
    {
        loading=false;if(loadingRoot)loadingRoot.gameObject.SetActive(false);
        excavation.interactable=precision.interactable=HelpButton.interactable=QuitButton.interactable=true;
        if(Group){Group.alpha=1;Group.interactable=true;}ShowError(message);
    }
    public void ShowError(string message){if(status){status.text=message;status.color=new Color(1,.54f,.39f);}}
    void Update()
    {
        if(!IsBuilt || !anchorReady)return;age+=Time.unscaledDeltaTime;if(!loading){Group.alpha=Mathf.SmoothStep(0,1,age/.4f);Group.interactable=age>=.3f;}
        if(HelpVisible && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)ShowHelp(false);
    }
    public void SetSpatialFocus(Button button,float amount){if(Yard)Yard.Focus(button,amount);}
    public void PlayFeedback(bool click)
    {if(!menuSounds || !sound || !Application.isPlaying || (!click && Time.unscaledTime-lastSound<.08f))return;lastSound=Time.unscaledTime;sound.PlayOneShot(click?clickClip:hoverClip);}
    Button SmallButton(string name,Transform parent,string label,float x,float y,float w,float h)
    {
        var p=PanelAt(name,parent,x,y,w,h,Panel,8);var button=p.gameObject.AddComponent<Button>();button.targetGraphic=p;button.transition=Selectable.Transition.None;
        Text("Label",p.transform,label,17,9,w-34,h-18,18,White);
        var feedback=p.gameObject.AddComponent<MenuButtonFeedback>();feedback.presentation=this;feedback.panel=p;feedback.Initialize();button.onClick.AddListener(()=>PlayFeedback(true));return button;
    }
    MenuPanelGraphic PanelAt(string name,Transform parent,float x,float y,float width,float height,Color color,float radius)
    {var panel=Rect(name,parent,x,y,width,height).gameObject.AddComponent<MenuPanelGraphic>();panel.color=color;panel.radius=radius;return panel;}
    TMP_Text Text(string name,Transform parent,string value,float x,float y,float width,float height,float size,Color color,bool bold=false)
    {
        var text=Rect(name,parent,x,y,width,height).gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;text.alignment=TextAlignmentOptions.TopLeft;return text;
    }
    void Rule(Transform parent,float x,float y,float width)
    {var image=Rect("Divider",parent,x,y,width,1).gameObject.AddComponent<Image>();image.color=new Color(.2f,.28f,.32f,.65f);image.raycastTarget=false;}
    static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
    {var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;Position(r,x,y,width,height);return r;}
    static void Position(RectTransform rect,float x,float y,float width,float height)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;}
    static AudioClip Tone(string name,float frequency,float length)
    {
        const int rate=22050;var samples=new float[Mathf.RoundToInt(length*rate)];
        for(int i=0;i<samples.Length;i++){float t=i/(float)rate;float envelope=Mathf.Sin(Mathf.PI*i/(samples.Length-1));samples[i]=Mathf.Sin(t*frequency*Mathf.PI*2)*envelope*envelope*.22f;}
        var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
    }
    void OnDestroy()
    {Dispose(hoverClip);Dispose(clickClip);Dispose(floorMaterial);if(ownsFont && font){var atlas=font.atlasTextures;if(atlas!=null)foreach(var texture in atlas)Dispose(texture);Dispose(font.material);Dispose(font);}}
    static void Dispose(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
}
