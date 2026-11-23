using UnityEngine;
using UnityEngine.UI;
using static NeonArena.ArenaTheme;

namespace NeonArena
{
    [DefaultExecutionOrder(600)]
    public sealed class FrontEnd : MonoBehaviour
    {
        public static Vector3 MenuEye=new Vector3(55,40,100),MenuLook=new Vector3(0,0,160);
        public static bool SettingsOpen { get; private set; }
        public static float Volume => PlayerPrefs.GetFloat("MasterVolume",.75f);
        public static float Sensitivity => PlayerPrefs.GetFloat("OrbitSensitivity",3);
        GameObject menu,settings;Button start;readonly Text[] values=new Text[6];float entered;MenuPresentation022 menuGrade;MenuShowcase023 showcase;
        readonly Slider[] sliders=new Slider[6];
        public MenuShowcase023 Showcase => showcase;
        public Slider VolumeSlider => sliders[0];
        public Slider SensitivitySlider => sliders[1];
        public Slider FlashSlider => sliders[5];
        void Start()
        {
            if(ArenaGame.Instance.terrain.satelliteCity){MenuEye=new Vector3(45,32,-48);MenuLook=new Vector3(-8,4,8);}
            entered=Time.unscaledTime;SettingsOpen=false;
            menuGrade=ArenaGame.Instance.player.viewCamera.gameObject.AddComponent<MenuPresentation022>();
            menu=Rect("Front End",transform,Vector2.zero,new Vector2(1600,900)).gameObject;menu.AddComponent<CanvasGroup>();
            var atmosphere=Rect("Perimeter atmosphere",menu.transform,Vector2.zero,new Vector2(1600,900)).gameObject.AddComponent<FieldAtmosphere>();atmosphere.raycastTarget=false;
            // The district, isolated armor portrait and foreground glass form three distinct depths.
            showcase=MenuShowcase023.Create(menu.transform);
            Text("Ranger identifier",menu.transform,new Vector2(150,390),new Vector2(290,42),"R A N G E R   /   0 1",20,FieldUI.Muted,TextAnchor.MiddleLeft);
            Text("Ranger status",menu.transform,new Vector2(175,-431),new Vector2(290,34),"●  机体在线  /  等待部署",17,FieldUI.Cyan,TextAnchor.MiddleCenter);
            var console=FieldUI.Panel("Deployment console",menu.transform,new Vector2(-522,0),new Vector2(464,790));
            console.gameObject.AddComponent<MenuSheetMotion023>();
            Text("Protocol",console.transform,new Vector2(0,334),new Vector2(392,32),"NEON ARENA   /   边境协议",20,FieldUI.Cyan,TextAnchor.MiddleLeft);
            Text("Title",console.transform,new Vector2(0,218),new Vector2(392,182),"白区\n卫星试炼",64,FieldUI.Paper,TextAnchor.MiddleLeft);
            Text("Subtitle",console.transform,new Vector2(0,83),new Vector2(392,55),"最后的信号，来自封锁区。",22,FieldUI.Muted,TextAnchor.MiddleLeft);
            Box("Mission separator",console.transform,new Vector2(0,31),new Vector2(392,1),new Color(.35f,.62f,.71f,.24f));
            Text("Objective",console.transform,new Vector2(0,-4),new Vector2(392,40),"深入白区 · 夺回控制室",23,FieldUI.Paper,TextAnchor.MiddleLeft);
            start=FieldUI.Action(console.transform,"Deploy",new Vector2(0,-84),new Vector2(392,76),"部署试炼    →",()=>ArenaGame.Instance.StartSession(),true);
            FieldUI.Action(console.transform,"Settings",new Vector2(-102,-174),new Vector2(188,62),"系统设置",()=>ShowSettings(true));
            FieldUI.Action(console.transform,"Quit",new Vector2(102,-174),new Vector2(188,62),"退出游戏",()=>Application.Quit());
            Text("Controls",console.transform,new Vector2(0,-264),new Vector2(392,76),"Enter 进入试炼   ·   F1 系统设置\n拖动机体检视  /  滚轮缩放",20,FieldUI.Muted,TextAnchor.MiddleLeft);
            Text("Version",console.transform,new Vector2(0,-346),new Vector2(392,30),"本地作战模拟   /   "+Application.version,20,FieldUI.Muted,TextAnchor.MiddleLeft);
            var warning=FieldUI.Panel("District warning",menu.transform,new Vector2(524,338),new Vector2(430,64),FieldUI.Amber);
            warning.gameObject.AddComponent<MenuSheetMotion023>();
            Text("Warning",warning.transform,Vector2.zero,new Vector2(406,48),"!   熔岩封锁  /  白区外围",23,FieldUI.Amber,TextAnchor.MiddleLeft);
            var route=FieldUI.Panel("District briefing",menu.transform,new Vector2(470,-210),new Vector2(530,160));
            route.gameObject.AddComponent<MenuSheetMotion023>();
            Text("District caption",route.transform,new Vector2(76,29),new Vector2(328,44),"白区  /  卫星城",27,FieldUI.Paper,TextAnchor.MiddleLeft);
            Text("Route",route.transform,new Vector2(76,-26),new Vector2(328,56),"广场接敌 → 办公楼\n高架管线 → 控制室",20,FieldUI.Muted,TextAnchor.MiddleLeft);
            route.color=new Color(.03f,.055f,.075f,.30f);
            route.rectTransform.localEulerAngles=new Vector3(0,-7,-1.2f);
            route.gameObject.AddComponent<MenuGlassPose024>().Bind(showcase);
            Text("Inspection hint",menu.transform,new Vector2(478,-321),new Vector2(410,29),"拖动检视  ·  滚轮缩放  ·  双击复位",18,FieldUI.Paper);
            string[] inspectLabels={"左转","右转","−","+","复位"};
            System.Action[] inspectActions={()=>showcase.RotateBy(-24),()=>showcase.RotateBy(24),()=>showcase.ZoomBy(-.12f),()=>showcase.ZoomBy(.12f),()=>showcase.ResetView()};
            for(int i=0;i<5;i++){int k=i;var b=FieldUI.Action(menu.transform,"Inspect "+i,new Vector2(326+i*76,-364),new Vector2(i==4?80:63,39),inspectLabels[i],()=>inspectActions[k]());b.transform.Find("Label").GetComponent<Text>().fontSize=18;}

            settings=Rect("System Settings",transform,Vector2.zero,new Vector2(1600,900)).gameObject;
            var veil=Box("Modal veil",settings.transform,Vector2.zero,new Vector2(3200,1800),new Color(.003f,.009f,.017f,.84f));veil.raycastTarget=true;
            var sheet=FieldUI.Panel("Settings console",settings.transform,Vector2.zero,new Vector2(1000,754));sheet.raycastTarget=true;
            sheet.gameObject.AddComponent<MenuSheetMotion023>();
            Text("Settings title",sheet.transform,new Vector2(-185,307),new Vector2(480,55),"系统设置",34,FieldUI.Paper,TextAnchor.MiddleLeft);
            Text("Settings hint",sheet.transform,new Vector2(-100,255),new Vector2(650,32),"拖动滑块调整数值 · 更改即时保存",20,FieldUI.Muted,TextAnchor.MiddleLeft);
            FieldUI.Action(sheet.transform,"Close",new Vector2(403,307),new Vector2(60,48),"×",()=>ShowSettings(false));
            string[] names={"主音量","镜头灵敏度","画质等级","显示模式","镜头震动","闪光强度"};
            UnityEngine.Events.UnityAction[] actions={
                ()=>SetVolume(Volume>=.99f?0:Volume+.25f),
                ()=>SetSensitivity(Sensitivity>=6?1:Sensitivity+1),
                ()=>SetQuality((PlayerPrefs.GetInt("DistrictQuality",2)+1)%3),
                ()=>{bool full=!Screen.fullScreen;Screen.fullScreenMode=full?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;PlayerPrefs.SetInt("Fullscreen",full?1:0);PlayerPrefs.Save();},
                ()=>{var c=ArenaGame.Instance.player.viewCamera.GetComponent<ArenaCamera>();c.shakeEnabled=!c.shakeEnabled;PlayerPrefs.SetInt("CameraShake",c.shakeEnabled?1:0);PlayerPrefs.Save();},
                ()=>{float v=PlayerPrefs.GetFloat("ArenaFlashGain",1);v=v>.5f?.25f:v>0?0:1;PlayerPrefs.SetFloat("ArenaFlashGain",v);CombatPresentation.SetFlash(v);PlayerPrefs.Save();}
            };
            for(int i=0;i<6;i++)
            {
                float y=183-i*73;
                var row=FieldUI.Action(sheet.transform,"Setting "+i,new Vector2(0,y),new Vector2(854,62),names[i],actions[i]);
                var label=row.transform.Find("Label").GetComponent<Text>();label.alignment=TextAnchor.MiddleLeft;label.rectTransform.anchoredPosition=new Vector2(-205,0);label.rectTransform.sizeDelta=new Vector2(380,54);
                values[i]=Text("Value "+i,row.transform,new Vector2(242,0),new Vector2(220,52),"",24,FieldUI.Cyan,TextAnchor.MiddleRight);
                Text("Cycle",row.transform,new Vector2(389,0),new Vector2(32,44),"›",26,FieldUI.Muted);
                if(i==0||i==1||i==5)
                {
                    label.rectTransform.anchoredPosition=new Vector2(-262,0);label.rectTransform.sizeDelta=new Vector2(265,54);
                    values[i].rectTransform.anchoredPosition=new Vector2(354,0);values[i].rectTransform.sizeDelta=new Vector2(116,52);values[i].fontSize=20;
                    row.transform.Find("Cycle").gameObject.SetActive(false);
                    int index=i;
                    var track=Rect("Continuous slider",row.transform,new Vector2(73,0),new Vector2(328,50));
                    var hit=track.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
                    var slider=track.gameObject.AddComponent<MenuSlider024>();sliders[i]=slider;slider.minValue=i==1?1:0;slider.maxValue=i==1?6:1;
                    var rail=Box("Rail",track,Vector2.zero,new Vector2(308,4),new Color(.09f,.18f,.23f));rail.raycastTarget=true;
                    var area=Rect("Fill area",track,Vector2.zero,new Vector2(308,4));
                    var fill=Box("Fill",area,Vector2.zero,Vector2.zero,FieldUI.Cyan);fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
                    var handleArea=Rect("Handle area",track,Vector2.zero,new Vector2(308,30));
                    var handle=Box("Handle",handleArea,Vector2.zero,new Vector2(14,24),FieldUI.Paper);handle.raycastTarget=true;
                    slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.direction=Slider.Direction.LeftToRight;
                    slider.SetValueWithoutNotify(i==0?Volume:i==1?Sensitivity:PlayerPrefs.GetFloat("ArenaFlashGain",1));
                    slider.onValueChanged.AddListener(v=>{if(index==0)SetVolume(v);else if(index==1)SetSensitivity(v);else SetFlash(v);});
                }
            }
            Text("Saved hint",sheet.transform,new Vector2(-206,-318),new Vector2(430,42),"Esc 返回    /    设置自动保留",18,FieldUI.Muted,TextAnchor.MiddleLeft);
            var motion=FieldUI.Action(sheet.transform,"Reduce motion",new Vector2(-206,-272),new Vector2(430,40),"界面动态：标准",()=>{PlayerPrefs.SetInt("MenuReducedMotion",MenuShowcase023.ReducedMotion?0:1);PlayerPrefs.Save();});
            motion.transform.Find("Label").GetComponent<Text>().fontSize=18;
            FieldUI.Action(sheet.transform,"Return",new Vector2(286,-302),new Vector2(282,64),"返回",()=>ShowSettings(false),true);
            settings.SetActive(false);ApplySaved();
        }
        public static void SetVolume(float value){PlayerPrefs.SetFloat("MasterVolume",Mathf.Clamp01(value));AudioListener.volume=Mathf.Clamp01(value);PlayerPrefs.Save();}
        public static void SetFlash(float value){value=Mathf.Clamp01(value);PlayerPrefs.SetFloat("ArenaFlashGain",value);CombatPresentation.SetFlash(value);PlayerPrefs.Save();}
        public static void SetSensitivity(float value){value=Mathf.Clamp(value,1,6);PlayerPrefs.SetFloat("OrbitSensitivity",value);if(ArenaGame.Instance)ArenaGame.Instance.player.viewCamera.GetComponent<ArenaCamera>().mouseSensitivity=value;PlayerPrefs.Save();}
        public static void SetQuality(int value)
        {
            value=Mathf.Clamp(value,0,2);PlayerPrefs.SetInt("DistrictQuality",value);QualitySettings.antiAliasing=value==0?0:value==1?2:4;
            QualitySettings.shadowDistance=value==0?22:value==1?38:65;QualitySettings.shadows=value==0?ShadowQuality.HardOnly:ShadowQuality.All;PlayerPrefs.Save();
        }
        public static void ApplySaved()
        {
            SetVolume(Volume);SetSensitivity(Sensitivity);SetQuality(PlayerPrefs.GetInt("DistrictQuality",2));
            if(ArenaGame.Instance)ArenaGame.Instance.player.viewCamera.GetComponent<ArenaCamera>().shakeEnabled=PlayerPrefs.GetInt("CameraShake",ArenaGame.Instance.Frontier?0:1)==1;
            Screen.fullScreenMode=PlayerPrefs.GetInt("Fullscreen",0)==1?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
        }
        public void ShowSettings(bool show){SettingsOpen=show;settings.SetActive(show);if(show)settings.transform.SetAsLastSibling();PlayerPrefs.Save();}
        void LateUpdate()
        {
            var game=ArenaGame.Instance;if(!game||!menu)return;
            bool ready=game.State==ArenaState.Ready;menu.SetActive(ready);menu.GetComponent<CanvasGroup>().blocksRaycasts=!SettingsOpen;menu.GetComponent<CanvasGroup>().alpha=SettingsOpen?.22f:1;
            foreach(Transform child in transform)
            {
                if(child.gameObject==menu||child.gameObject==settings)continue;
                var group=child.GetComponent<CanvasGroup>();if(!group)group=child.gameObject.AddComponent<CanvasGroup>();
                group.alpha=ready?0:1;group.blocksRaycasts=!ready&&!SettingsOpen;
            }
            if(ready)
            {
                var cam=game.player.viewCamera;cam.orthographic=false;cam.fieldOfView=47;
                float t=Time.unscaledTime-entered;
                cam.transform.position=MenuEye+Vector3.right*(MenuShowcase023.ReducedMotion?0:Mathf.Sin(t*.025f)*3);
                cam.transform.LookAt(MenuLook);
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            }
            if(Input.GetKeyDown(KeyCode.F1)&&(ready||game.State==ArenaState.Paused))ShowSettings(!SettingsOpen);
            if(SettingsOpen)
            {
                if(Input.GetKeyDown(KeyCode.Escape))ShowSettings(false);
                string[] labels={Mathf.RoundToInt(Volume*100)+"%",Sensitivity.ToString("0.0")+" / 6",new[]{"性能","均衡","精细"}[PlayerPrefs.GetInt("DistrictQuality",2)],Screen.fullScreen?"无边框全屏":"窗口",game.player.viewCamera.GetComponent<ArenaCamera>().shakeEnabled?"开启":"关闭",Mathf.RoundToInt(PlayerPrefs.GetFloat("ArenaFlashGain",1)*100)+"%"};
                for(int i=0;i<6;i++)values[i].text=labels[i];
                sliders[0].SetValueWithoutNotify(Volume);sliders[1].SetValueWithoutNotify(Sensitivity);sliders[5].SetValueWithoutNotify(PlayerPrefs.GetFloat("ArenaFlashGain",1));
                var motionLabel=settings.transform.Find("Settings console/Reduce motion/Label").GetComponent<Text>();motionLabel.text="界面动态："+(MenuShowcase023.ReducedMotion?"减少":"标准");
            }
            if(menuGrade)menuGrade.enabled=ready;
        }
        void OnDestroy(){SettingsOpen=false;if(menuGrade)Destroy(menuGrade);}
    }
}
