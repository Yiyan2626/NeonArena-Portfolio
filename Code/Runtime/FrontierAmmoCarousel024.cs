using UnityEngine;
using UnityEngine.EventSystems;

namespace NeonArena
{
    // Drag handler lives on the card parent: child Buttons still receive normal clicks.
    // Only SelectAmmo is reachable from this gesture. Mobility remains an explicit claim.
    public sealed class FrontierAmmoCarousel024 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        RectTransform[] cards;
        RectTransform area;
        FieldGlassGraphic[] optics;
        FieldGlassGraphic[][] cardCoatings;
        float position,target,originPosition,originX;
        int observed=-2;
        bool dragging;
        public float Position => position;
        public bool IsDragging => dragging;
        public int FocusedIndex => Wrap(Mathf.RoundToInt(position));
        public int GestureCount { get; private set; }
        static int Wrap(int value){return (value%3+3)%3;}
        static bool Reduced => PlayerPrefs.GetInt("MenuReducedMotion",0)==1||CombatPresentation.FlashGain<=0;
        public void Configure(RectTransform[] value){cards=value;optics=new FieldGlassGraphic[cards.Length];cardCoatings=new FieldGlassGraphic[cards.Length][];for(int i=0;i<cards.Length;i++){optics[i]=cards[i].GetComponentInChildren<FieldGlassGraphic>();cardCoatings[i]=cards[i].GetComponentsInChildren<FieldGlassGraphic>();}area=(RectTransform)transform;position=target=0;Apply();}
        void OnEnable(){observed=-2;dragging=false;}
        void OnDisable(){dragging=false;}
        float LocalX(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(area,e.position,e.pressEventCamera,out var p);return p.x;}
        public void OnBeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;
            dragging=true;originPosition=position;originX=LocalX(e);e.eligibleForClick=false;
        }
        public void OnDrag(PointerEventData e)
        {
            if(!dragging)return;
            position=originPosition+(originX-LocalX(e))/360f;Apply();e.eligibleForClick=false;
        }
        public void OnEndDrag(PointerEventData e)
        {
            if(!dragging)return;
            dragging=false;e.eligibleForClick=false;
            float distance=originX-LocalX(e);
            target=Mathf.Round(position);
            // A deliberate short swipe advances one card; an incidental press never equips.
            if(Mathf.Abs(distance)>46&&Mathf.RoundToInt(target)==Mathf.RoundToInt(originPosition))target=Mathf.Round(originPosition)+Mathf.Sign(distance);
            if(Mathf.Abs(distance)>18)Equip(Wrap(Mathf.RoundToInt(target)));
            if(Reduced){position=target;Apply();}
        }
        public void OnScroll(PointerEventData e)
        {
            if(Mathf.Abs(e.scrollDelta.y)<.01f&&Mathf.Abs(e.scrollDelta.x)<.01f)return;
            float direction=Mathf.Abs(e.scrollDelta.y)>.01f?-e.scrollDelta.y:e.scrollDelta.x;
            target=Mathf.Round(target)+Mathf.Sign(direction);Equip(Wrap(Mathf.RoundToInt(target)));
            if(Reduced){position=target;Apply();}
        }
        void Equip(int index)
        {
            var g=ArenaGame.Instance;if(!g||!g.Frontier||!g.Frontier.TerminalOpen)return;
            if(g.Frontier.SelectAmmo((AmmoUpgrade)index)){observed=index;GestureCount++;}
        }
        void LateUpdate()
        {
            if(cards==null)return;
            var g=ArenaGame.Instance;if(!g||!g.player)return;
            int selected=g.player.weapon.Loadout.SelectedAmmo;
            if(selected!=observed&&!dragging)
            {
                bool first=observed==-2;observed=selected;
                if(selected>=0){float delta=Mathf.Repeat(selected-position+1.5f,3)-1.5f;target=position+delta;}
                if(first||Reduced)position=target;
            }
            if(!dragging)position=Reduced?target:Mathf.MoveTowards(position,target,Time.unscaledDeltaTime*4.5f);
            Apply();
        }
        void Apply()
        {
            if(cards==null)return;
            int front=0;float nearest=10;
            for(int i=0;i<cards.Length;i++)
            {
                float offset=Mathf.Repeat(i-position+1.5f,3)-1.5f,depth=Mathf.Clamp01(Mathf.Abs(offset));
                cards[i].anchoredPosition=new Vector2(offset*345,-23*depth);
                cards[i].localScale=Vector3.one*(1.07f-.17f*depth);
                cards[i].localRotation=Quaternion.Euler(0,-offset*18,offset*1.5f);
                if(optics[i])optics[i].SetViewAngle(offset*.6f);
                foreach(var coating in cardCoatings[i])if(coating&&coating.coating>0)coating.SetViewAngle(offset*.6f);
                if(Mathf.Abs(offset)<nearest){nearest=Mathf.Abs(offset);front=i;}
            }
            cards[front].SetAsLastSibling();
        }
    }
}
