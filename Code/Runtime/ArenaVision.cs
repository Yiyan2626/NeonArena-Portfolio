using UnityEngine;
using System.Collections.Generic;

namespace NeonArena
{
    // Lighting, target acquisition and minimap all use player visibility, not camera visibility.
    public sealed class ArenaVision : MonoBehaviour
    {
        public const float SightRadius = 23, BrushRadius = 7;
        const int Width=96, Height=80;
        Texture2D texture;
        readonly Color32[] pixels=new Color32[Width*Height*3];
        readonly List<Bounds> brushBounds=new List<Bounds>(24);
        float nextRefresh;
        public bool PlayerInBrush => ArenaGame.Instance && ArenaGame.Instance.terrain.InBrush(ArenaGame.Instance.player.transform.position);
        public float CurrentRadius => PlayerInBrush ? BrushRadius : ArenaGame.Instance&&ArenaGame.Instance.Frontier?65:SightRadius;
        void Awake()
        {
            texture=new Texture2D(Width,Height*3,TextureFormat.RGBA32,false,true);
            texture.name="Player Visibility / Ground Upper Pit";texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
            Shader.SetGlobalVector("_ArenaWorldRect",new Vector4(ArenaTerrain.WorldHalfWidth,ArenaTerrain.WorldHalfDepth,ArenaTerrain.WorldHalfWidth*2,ArenaTerrain.WorldHalfDepth*2));
            Shader.SetGlobalTexture("_ArenaVision",texture);Shader.SetGlobalFloat("_ArenaVisionEnabled",0);
        }
        public void Invalidate()=>nextRefresh=0;
        public bool CanObserve(Vector3 point)
        {
            var game=ArenaGame.Instance;if(!game||!game.player||!game.terrain)return true;
            Vector3 from=game.player.transform.position,delta=point-from;
            float radius=CurrentRadius;
            if(delta.sqrMagnitude>radius*radius)return false;
            if(!game.terrain.CanSee(from,point))return false;
            return HasGeometrySight(from,point,game.Frontier);
        }
        static bool HasGeometrySight(Vector3 from,Vector3 point,bool upperBodySample)
        {
            if(!Physics.Linecast(from+Vector3.up*.85f,point+Vector3.up*.65f,1<<9,QueryTriggerInteraction.Ignore))return true;
            // A sill can hide the lower body while the torso remains exposed through
            // a firing aperture. Use a second player-origin ray, never camera vision
            // or a reveal override; solid shutters still block both samples.
            return upperBodySample&&!Physics.Linecast(from+Vector3.up*1.25f,point+Vector3.up*1.2f,1<<9,QueryTriggerInteraction.Ignore);
        }
        void LateUpdate()
        {
            var game=ArenaGame.Instance;
            bool active=game && game.State!=ArenaState.Ready;
            Shader.SetGlobalFloat("_ArenaVisionEnabled",active?1:0);
            if(!active || Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.16f;
            Vector3 player=game.player.transform.position;float radius=CurrentRadius;
            // Geometry is stationary during a wave: sample native collider bounds once per atlas refresh.
            game.terrain.CaptureBrushBounds(brushBounds);
            for(int layer=0;layer<3;layer++)for(int z=0;z<Height;z++)for(int x=0;x<Width;x++)
            {
                float y=layer==0?.05f:layer==1?3.3f:-2.35f;
                Vector3 sample=new Vector3(((x+.5f)/Width*2-1)*ArenaTerrain.WorldHalfWidth,y,((z+.5f)/Height*2-1)*ArenaTerrain.WorldHalfDepth);
                if(game.terrain.satelliteCity)
                {
                    float sampleHeight=layer==1?Mathf.Max(8,player.y+2):3.5f;
                    if(Physics.Raycast(new Vector3(sample.x,sampleHeight,sample.z),Vector3.down,out var cityGround,sampleHeight+3,1<<9,QueryTriggerInteraction.Ignore) && cityGround.normal.y>.72f)sample.y=cityGround.point.y+.06f;
                }
                else if(layer==1 && Physics.Raycast(new Vector3(sample.x,20,sample.z),Vector3.down,out var roof,18,1<<9,QueryTriggerInteraction.Ignore) && roof.normal.y>.72f)sample.y=roof.point.y+.06f;
                float distance=Vector3.Distance(player,sample);
                bool seen=distance<radius;
                if(seen)foreach(var bounds in brushBounds)if(BrushPatch.Blocks(bounds,player,sample)){seen=false;break;}
                if(seen)seen=HasGeometrySight(player,sample,game.Frontier);
                float visible=seen?Mathf.Clamp01((radius-distance)/2):0;
                byte b=(byte)(visible*255);
                pixels[(layer*Height+z)*Width+x]=new Color32(b,b,b,255);
            }
            texture.SetPixels32(pixels);texture.Apply(false,false);
        }
        void OnDestroy(){Shader.SetGlobalFloat("_ArenaVisionEnabled",0);if(texture)Destroy(texture);}
    }
}
