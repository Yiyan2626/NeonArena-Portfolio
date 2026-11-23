using System.Collections.Generic;
using UnityEngine;

namespace NeonArena
{
    // Additive stages: advanced spaces stay available rather than cycling back to an empty map.
    public sealed class StageExpansion : MonoBehaviour
    {
        readonly GameObject[] tiers = new GameObject[9];
        readonly List<BrushPatch> patches = new List<BrushPatch>();
        readonly List<Material> materials = new List<Material>();
        readonly List<CutawayRoof> roofs = new List<CutawayRoof>();
        Transform vertical;
        GameObject flat;
        GameObject flatGrid;
        GameObject bridgeRail;
        Material stone, floor, accent, leaf, dark;
        public IReadOnlyList<BrushPatch> Patches => patches;
        public int Stage { get; private set; }
        public void Initialize()
        {
            vertical = GameObject.Find("Vertical Routes")?.transform;
            flat = GameObject.Find("Playing Surface");
            flatGrid=GameObject.Find("Ground Grid");
            bridgeRail=GameObject.Find("Bridge Far Rail");
            stone=Mat(new Color(.29f,.34f,.39f)); floor=Mat(new Color(.18f,.23f,.28f));
            accent=Mat(new Color(.12f,.65f,.72f),.45f);leaf=Mat(new Color(.06f,.33f,.16f));dark=Mat(new Color(.055f,.075f,.10f));
            for(int i=2;i<=8;i++){tiers[i]=new GameObject("Complexity "+i);tiers[i].transform.SetParent(transform,false);}
            Grass(2,new Vector3(-8,0,6),new Vector2(5,3));Grass(2,new Vector3(8,0,-6),new Vector2(5,3));
            Grass(3,new Vector3(-18,3,9),new Vector2(4,3));Grass(3,new Vector3(18,3,1),new Vector2(4,3));
            // A real depression: the full ground slab is replaced by four surrounding slabs.
            Box(4,"Ground North",new Vector3(0,-.1f,5),new Vector3(52,.2f,30),floor);
            Box(4,"Ground South",new Vector3(0,-.1f,-18),new Vector3(52,.2f,4),floor);
            Box(4,"Ground West",new Vector3(-16,-.1f,-13),new Vector3(20,.2f,6),floor);
            Box(4,"Ground East",new Vector3(16,-.1f,-13),new Vector3(20,.2f,6),floor);
            Box(4,"Pit Bed",new Vector3(0,-2.5f,-13),new Vector3(12,.2f,6),floor);
            Box(4,"Pit North Edge",new Vector3(0,-1.2f,-9.85f),new Vector3(12,2.4f,.3f),stone);
            Box(4,"Pit South Edge",new Vector3(0,-1.2f,-16.15f),new Vector3(12,2.4f,.3f),stone);
            Ramp(4,"Pit Exit East",new Vector3(0,-2.4f,-13),new Vector3(6,0,-13),4);
            Ramp(4,"Pit Exit West",new Vector3(0,-2.4f,-13),new Vector3(-6,0,-13),4);
            var grid=Mat(new Color(.11f,.23f,.27f),.2f);
            for(int x=-24;x<=24;x+=2)
            {
                if(Mathf.Abs(x)<6)
                {
                    Box(4,"North Grid",new Vector3(x,.006f,5),new Vector3(.025f,.012f,30),grid,false);
                    Box(4,"South Grid",new Vector3(x,.006f,-18),new Vector3(.025f,.012f,4),grid,false);
                }
                else Box(4,"Ground Grid",new Vector3(x,.006f,0),new Vector3(.025f,.012f,39.6f),grid,false);
            }
            for(int z=-18;z<=18;z+=2)
            {
                if(z>-16 && z<-10)
                {
                    for(int side=-1;side<=1;side+=2)Box(4,"Side Grid",new Vector3(side*16,.006f,z),new Vector3(20,.012f,.025f),grid,false);
                }
                else Box(4,"Cross Grid",new Vector3(0,.006f,z),new Vector3(51.6f,.012f,.025f),grid,false);
            }
            for(int x=-4;x<=4;x+=2)Box(4,"Pit Grid",new Vector3(x,-2.394f,-13),new Vector3(.025f,.012f,5.6f),grid,false);
            Grass(4,new Vector3(0,-2.4f,-15),new Vector2(4,1.3f));
            Grass(4,new Vector3(9,0,4),new Vector2(3,4));
            Building(-7);Building(7);
            Box(5,"Bridge Rail Center",new Vector3(0,3.45f,10.9f),new Vector3(10.5f,.9f,.18f),stone);
            for(int side=-1;side<=1;side+=2)Box(5,"Bridge Rail End",new Vector3(side*11,3.45f,10.9f),new Vector3(4,.9f,.18f),stone);
            Grass(5,new Vector3(-7,0,15),new Vector2(3,2));Grass(5,new Vector3(7,0,15),new Vector2(3,2));
            // Staggered partitions with alternating 3 m passages, never sealed cells.
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++)
                {
                    float x=side*(17+i*3);
                    Box(6,"Maze Divider",new Vector3(x,1.2f,-13+(i%2==0?1:-1)),new Vector3(.55f,2.4f,7),stone);
                    Box(6,"Maze Cap",new Vector3(x,2.42f,-13+(i%2==0?1:-1)),new Vector3(.58f,.05f,7),accent,false);
                }
                Grass(6,new Vector3(side*21.5f,0,-15),new Vector2(2,3));
                Box(7,"Roof Cover",new Vector3(side*7,3.9f,16),new Vector3(3,1.3f,.6f),stone);
                Grass(7,new Vector3(side*7,3.25f,14),new Vector2(3,2));
                Box(8,"Inner Barricade",new Vector3(side*7,1.15f,-1),new Vector3(.6f,2.3f,5),stone);
                Grass(8,new Vector3(side*3,0,-6),new Vector2(3,2));
            }
            tiers[6].AddComponent<FoundryDressing>().Build();
            var roofDecorations=tiers[7].GetComponentsInChildren<Renderer>();
            foreach(var roof in roofs){roof.RegisterDecorations(roofDecorations);roof.RegisterDecorations(tiers[6].GetComponentsInChildren<Renderer>());}
            foreach(var root in tiers)if(root)root.SetActive(false);
        }
        Material Mat(Color color,float emission=0){var m=RuntimeArt.Material(color,emission);materials.Add(m);return m;}
        Transform Box(int tier,string name,Vector3 p,Vector3 size,Material m,bool solid=true)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(tiers[tier].transform,false);
            o.transform.localPosition=p;o.transform.localScale=size;o.GetComponent<Renderer>().sharedMaterial=m;
            o.layer=9;if(!solid){o.GetComponent<Collider>().enabled=false;Destroy(o.GetComponent<Collider>());}return o.transform;
        }
        void Ramp(int tier,string name,Vector3 a,Vector3 b,float width)
        {
            Vector3 delta=b-a;var r=Box(tier,name,(a+b)*.5f-Vector3.up*.16f,new Vector3(width,.3f,delta.magnitude+.35f),floor);
            r.rotation=Quaternion.LookRotation(delta.normalized,Vector3.up);
        }
        void Building(float x)
        {
            Box(5,"Building Rear Wall",new Vector3(x,1.5f,18),new Vector3(7,3,.4f),stone);
            for(int side=-1;side<=1;side+=2)
            {
                Box(5,"Building Side Wall",new Vector3(x+side*3.5f,1.5f,15),new Vector3(.4f,3,6),stone);
                Box(5,"Door Frame",new Vector3(x+side*2.4f,1.5f,12),new Vector3(2.2f,3,.4f),stone);
                Box(5,"Facade Lamp",new Vector3(x+side*1.25f,1.5f,11.75f),new Vector3(.08f,2,.08f),accent,false);
            }
            var roof=Box(5,"Walkable Building Roof",new Vector3(x,3.1f,15),new Vector3(7.4f,.3f,6.4f),floor);
            roofs.Add(roof.gameObject.AddComponent<CutawayRoof>());
            // Upper bridge and roofs connect at nearly the same elevation.
            Ramp(5,"Roof Access",new Vector3(x,3,10),new Vector3(x,3.25f,12),3);
            Box(5,"Door Lintel",new Vector3(x,2.8f,12),new Vector3(2.6f,.4f,.4f),accent);
        }
        void Grass(int tier,Vector3 p,Vector2 size)
        {
            var o=new GameObject("Dense Visibility Brush");o.transform.SetParent(tiers[tier].transform,false);o.transform.position=p+Vector3.up*.8f;
            var c=o.AddComponent<BoxCollider>();c.isTrigger=true;c.size=new Vector3(size.x,1.6f,size.y);
            patches.Add(o.AddComponent<BrushPatch>());
            for(int x=0;x<Mathf.CeilToInt(size.x*2);x++)for(int z=0;z<Mathf.CeilToInt(size.y*2);z++)
            {
                float h=.9f+.4f*Mathf.Abs(Mathf.Sin(x*17+z*9));
                var blade=RuntimeArt.Box(o.transform,"Reed",new Vector3((x+.5f)*.5f-size.x*.5f,h*.5f-.8f,(z+.5f)*.5f-size.y*.5f),new Vector3(.22f,h,.22f),leaf);
                blade.localRotation=Quaternion.Euler(6*(z%3-1),x*37,8*(x%3-1));
            }
        }
        public void Apply(int wave)
        {
            Stage=Mathf.Clamp(wave,1,8);
            if(vertical)vertical.gameObject.SetActive(Stage>=3);
            if(flat)flat.SetActive(Stage<4);
            if(flatGrid)flatGrid.SetActive(Stage<4);
            if(bridgeRail)bridgeRail.SetActive(Stage<5);
            for(int i=2;i<=8;i++)tiers[i].SetActive(Stage>=i);
        }
        void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);}
    }
}
