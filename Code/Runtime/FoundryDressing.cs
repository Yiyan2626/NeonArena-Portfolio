using System.Collections.Generic;
using UnityEngine;

namespace NeonArena
{
    // Stage-six authored landmark pass. Facade details share the existing solid walls;
    // the combat routes, doors, roof connectors and escape ramps remain navigable.
    public sealed class FoundryDressing : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        Material dark,metal,warm,cool;
        public void Build()
        {
            dark=Mat(new Color(.055f,.085f,.11f));metal=Mat(new Color(.38f,.43f,.44f));warm=Mat(new Color(.92f,.45f,.08f),.5f);cool=Mat(new Color(.08f,.7f,.8f),.6f);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*7;Material route=side<0?cool:warm;
                for(int edge=-1;edge<=1;edge+=2)
                {
                    Box("Facade Column",new Vector3(x+edge*3.5f,1.5f,11.74f),new Vector3(.28f,3,.23f),metal);
                    Box("Door Frame Glow",new Vector3(x+edge*1.28f,1.3f,11.72f),new Vector3(.06f,2.6f,.045f),route);
                    for(int y=0;y<4;y++)Box("Facade Tile",new Vector3(x+edge*2.3f,.4f+y*.62f,11.75f),new Vector3(1.65f,.54f,.06f),dark);
                    for(int z=0;z<3;z++)
                    {
                        Box("Side Service Vent",new Vector3(x+edge*3.72f,1.65f,13+z*1.7f),new Vector3(.045f,.9f,.92f),dark);
                        for(int k=0;k<4;k++)Box("Vent Slat",new Vector3(x+edge*3.75f,1.3f+k*.2f,13+z*1.7f),new Vector3(.04f,.055f,.75f),metal);
                    }
                }
                Box("Door Sign",new Vector3(x,2.67f,11.68f),new Vector3(2.4f,.34f,.09f),dark);
                Label(side<0?"A / COOLANT":"B / FORGE",new Vector3(x,2.67f,11.59f),new Vector3(0,180,0),.16f,side<0?Color.cyan:new Color(1,.65f,.2f));
                for(int z=-9;z<=5;z+=2)
                {Box("Upper Route Strip",new Vector3(side*14.5f,z<0?(.3f*(z+11)+.06f):3.07f,z),new Vector3(.10f,.02f,.65f),route);}
                for(int z=-16;z<=-10;z+=2)
                    Box("Maze Entrance Beacon",new Vector3(side*24.8f,.07f,z),new Vector3(.3f,.10f,.65f),route);
                Label(side<0?"01 / WEST LOOP":"02 / EAST LOOP",new Vector3(side*18,.035f,-18),new Vector3(90,0,0),.27f,side<0?Color.cyan:new Color(1,.65f,.2f));
                Label("ROOF ACCESS",new Vector3(x,3.28f,12.5f),new Vector3(90,0,0),.20f,new Color(.9f,.8f,.5f));
            }
            // Central solid pillar becomes the forge landmark rather than adding a new blocker.
            Box("Reactor Crown",new Vector3(0,1.71f,2),new Vector3(1.9f,.2f,1.9f),metal);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<5;i++)Box("Reactor Cooling Fin",new Vector3(side*.94f,.35f+i*.24f,2),new Vector3(.09f,.09f,1.8f),dark);
                Box("Reactor Core Strip",new Vector3(0,.95f,2+side*.92f),new Vector3(.27f,1.1f,.035f),warm);
                for(int x=-5;x<=5;x++)Box("Pit Hazard Mark",new Vector3(x,.035f,-13+side*3.1f),new Vector3(.45f,.025f,.18f),warm);
            }
            Label("AEGIS / FOUNDRY 06",new Vector3(0,.04f,-5),new Vector3(90,0,0),.32f,new Color(.9f,.75f,.42f));
            Label("LOWER SERVICE / -02",new Vector3(0,-2.36f,-15.65f),new Vector3(90,0,0),.17f,Color.cyan);
            for(int side=-1;side<=1;side+=2)
                for(int z=-15;z<=-11;z+=2)Box("Pit Exit Lamp",new Vector3(side*5.9f,.06f,z),new Vector3(.13f,.1f,.35f),cool);
        }
        Material Mat(Color c,float emission=0){var m=RuntimeArt.Material(c,emission);materials.Add(m);return m;}
        void Box(string name,Vector3 p,Vector3 size,Material mat){RuntimeArt.Box(transform,name,p,size,mat);}
        void Label(string text,Vector3 p,Vector3 angles,float size,Color color)
        {
            var label=new GameObject(text).AddComponent<TextMesh>();label.transform.SetParent(transform,false);label.transform.localPosition=p;label.transform.localEulerAngles=angles;
            label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=64;label.characterSize=size/8;label.anchor=TextAnchor.MiddleCenter;label.color=color;label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
        }
        void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);}
    }
}
