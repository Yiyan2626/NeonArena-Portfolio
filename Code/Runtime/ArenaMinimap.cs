using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NeonArena
{
    public sealed class ArenaMinimap : MonoBehaviour
    {
        GameObject panel;
        Text title,legend;
        MapGraphic map;
        int floor;
        float refreshAt;
        static readonly string[] Floors={"全层叠图","地面层","上层 / 屋顶","坑底层"};
        void Awake()
        {
            panel=ArenaTheme.Box("Tactical Minimap",transform,new Vector2(-168,-212),new Vector2(280,266),new Color(.02f,.035f,.055f,.94f),new Vector2(1,1)).gameObject;
            FieldUI.Decorate(panel.GetComponent<Image>(),FieldUI.Cyan);
            title=ArenaTheme.Text("Map Floor",panel.transform,new Vector2(0,109),new Vector2(256,30),"",20,FieldUI.Paper);
            map=ArenaTheme.Rect("Map Geometry",panel.transform,new Vector2(0,4),new Vector2(248,174)).gameObject.AddComponent<MapGraphic>();
            map.raycastTarget=false;
            legend=ArenaTheme.Text("Map Legend",panel.transform,new Vector2(0,-105),new Vector2(254,32),"青■ 目标  /  H 图例",20,FieldUI.Muted);
        }
        void LateUpdate()
        {
            var game=ArenaGame.Instance;if(!game)return;
            bool active=(game.IsPlaying || game.State==ArenaState.Paused) && !game.Cinema.FinaleActive;
            panel.SetActive(active);if(!active)return;
            if(Input.GetKeyDown(KeyCode.M))floor=(floor+1)%4;
            title.text="N ↑  "+Floors[floor]+"  / M";
            if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.08f;map.Refresh(floor);}
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MapGraphic : MaskableGraphic
    {
        readonly List<BoxCollider> geometry=new List<BoxCollider>();
        readonly List<Bounds> cityFootprints=new List<Bounds>();
        int stage=-1,mode;
        public static int Floor(Vector3 p)=>p.y < -1 ? -1 : p.y > 1.9f ? 1 : 0;
        public static bool CanShow(EnemyController e)
        { var game=ArenaGame.Instance;return e && e.IsAlive && e.IsVisibleToPlayer && game && game.Vision.CanObserve(e.VisibilityPoint); }
        public static Color EnemyColor(EnemyController e)=>e.Ranged && e.Ranged.IsRanged?new Color(1,.6f,.12f):e.Elite && e.Elite.IsElite?ArenaTheme.Gold:e.Charge && e.Charge.IsCharger?new Color(.78f,.36f,1):new Color(1,.22f,.26f);
        public void Refresh(int floor)
        {
            mode=floor;var game=ArenaGame.Instance;if(!game)return;
            if(stage!=game.terrain.Complexity)
            {
                stage=game.terrain.Complexity;geometry.Clear();cityFootprints.Clear();
                if(game.terrain.satelliteCity)foreach(var c in FindObjectsOfType<MeshCollider>())
                {
                    var b=c.bounds;
                    if(c.enabled&&c.gameObject.layer==9&&b.size.x>4&&b.size.z>4&&b.size.y>3&&b.size.x<60&&b.size.z<60)cityFootprints.Add(b);
                }
                foreach(var c in FindObjectsOfType<BoxCollider>())if(c.enabled && (c.gameObject.layer==9 || c.GetComponent<BrushPatch>()))geometry.Add(c);
                geometry.Sort((a,b)=>a.bounds.max.y.CompareTo(b.bounds.max.y));
            }
            SetVerticesDirty();
        }
        bool LayerVisible(int layer)=>mode==0 || layer==(mode==1?0:mode==2?1:-1);
        Vector2 Project(Vector3 p)=>new Vector2(p.x/(ArenaTerrain.WorldHalfWidth*2+4)*rectTransform.rect.width,p.z/(ArenaTerrain.WorldHalfDepth*2+4)*rectTransform.rect.height+(mode==0?Mathf.Clamp(p.y,-3,8)*2.2f:0));
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var game=ArenaGame.Instance;if(!game||!game.player)return;
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
            {
                int n=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            void Line(Vector2 a,Vector2 b,Color color,float width=1)
            {Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Quad(a-n,a+n,b+n,b-n,color);}
            void Dot(Vector2 p,Color color,bool square,float size=3)
            {
                if(square){Quad(p+new Vector2(-size,-size),p+new Vector2(-size,size),p+new Vector2(size,size),p+new Vector2(size,-size),color);return;}
                for(int i=0;i<12;i++)
                {float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;int n=vh.currentVertCount;vh.AddVert(p,color,Vector2.zero);vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size,color,Vector2.zero);vh.AddVert(p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*size,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);}
            }
            foreach(var c in geometry)
            {
                if(!c || !c.gameObject.activeInHierarchy)continue;
                var bounds=c.bounds;int layer=Floor(new Vector3(0,bounds.size.y<.7f?bounds.max.y:bounds.min.y,0));
                if(!LayerVisible(layer))continue;
                bool grass=c.GetComponent<BrushPatch>();bool slab=bounds.size.y<.7f;
                Color color=grass?new Color(.12f,.35f,.22f,.85f):slab?(layer==1?new Color(.15f,.39f,.48f,.8f):layer==-1?new Color(.28f,.20f,.15f):new Color(.09f,.13f,.18f)):new Color(.48f,.53f,.57f,.85f);
                Vector3 half=c.size*.5f,center=c.center;
                Vector2 a=Project(c.transform.TransformPoint(center+new Vector3(-half.x,half.y,-half.z)));
                Vector2 b=Project(c.transform.TransformPoint(center+new Vector3(-half.x,half.y,half.z)));
                Vector2 d=Project(c.transform.TransformPoint(center+new Vector3(half.x,half.y,-half.z)));
                Vector2 e=Project(c.transform.TransformPoint(center+new Vector3(half.x,half.y,half.z)));
                Quad(a,b,e,d,color);
                if(slab && layer!=0){Line(a,b,ArenaTheme.Gold,.6f);Line(b,e,ArenaTheme.Gold,.6f);Line(e,d,ArenaTheme.Gold,.6f);Line(d,a,ArenaTheme.Gold,.6f);}
            }
            foreach(var b in cityFootprints)
            {
                Vector2 a=Project(new Vector3(b.min.x,0,b.min.z)),c=Project(new Vector3(b.max.x,0,b.max.z));
                Vector2 top=new Vector2(a.x,c.y),right=new Vector2(c.x,a.y);var col=new Color(.55f,.65f,.7f,.65f);
                Line(a,top,col,.65f);Line(top,c,col,.65f);Line(c,right,col,.65f);Line(right,a,col,.65f);
            }
            int playerFloor=Floor(game.player.transform.position);
            if(game.Frontier)
            {
                Vector2 target=Project(FrontierGuidance.Goal(game));
                Dot(target,Color.black,true,5.5f);Dot(target,new Color(.2f,1,1),true,3.9f);
            }
            foreach(var enemy in game.Enemies)
            {
                if(!CanShow(enemy))continue;int layer=Floor(enemy.transform.position);if(!LayerVisible(layer))continue;
                Vector2 p=Project(enemy.transform.position);Color c=EnemyColor(enemy);
                bool shield=(enemy.Shield && enemy.Shield.IsShieldType)||(enemy.Elite && enemy.Elite.IsElite);
                Dot(p,Color.black,shield,5.2f);Dot(p,c,shield,4);
                if(layer!=playerFloor){float sign=layer>playerFloor?1:-1;Vector2 tip=p+new Vector2(0,sign*8);Line(tip+new Vector2(-3,-sign*3),tip,c);Line(tip,tip+new Vector2(3,-sign*3),c);}
            }
            Vector2 player=Project(game.player.transform.position);Dot(player,Color.white,false,4.6f);Dot(player,new Color(.08f,.5f,1),false,3.4f);
            Vector3 forward=game.player.viewCamera.transform.forward;Vector2 dir=new Vector2(forward.x,forward.z).normalized;
            Line(player+dir*6,player+dir*12,Color.white,1.4f);
        }
    }
}
