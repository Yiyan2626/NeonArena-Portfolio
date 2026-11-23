using System.Collections.Generic;
using UnityEngine;

namespace NeonArena
{
    // Original rigid-weight mechanical skin. Each armor segment is bound to a named joint.
    // The saved prefabs/meshes are also editable independently of gameplay code.
    public sealed class CharacterModel : MonoBehaviour
    {
        public bool imported;
        public Transform[] sourceBones;
        public Transform[] joints;
        public SkinnedMeshRenderer skin;
        public int kind;
        public static readonly string[] JointNames={"Pelvis","Spine","Head","Shoulder L","Elbow L","Hand L","Shoulder R","Elbow R","Hand R","Hip L","Knee L","Foot L","Hip R","Knee R","Foot R"};
        public static CharacterModel Create(int kind)
        {
            var root=new GameObject(new[]{"Ion Ranger","Hunter Automaton","Strider Automaton","Bulwark Automaton","Aegis Warden"}[kind]);
            var model=root.AddComponent<CharacterModel>();model.kind=kind;
            model.joints=new Transform[15];
            Transform Bone(int i,int parent,Vector3 p)
            {
                var t=new GameObject(JointNames[i]).transform;t.SetParent(parent<0?root.transform:model.joints[parent],false);t.localPosition=p;model.joints[i]=t;return t;
            }
            Bone(0,-1,new Vector3(0,.79f,0));Bone(1,0,new Vector3(0,.21f,0));Bone(2,1,new Vector3(0,.40f,0));
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;int a=side==0?3:6,l=side==0?9:12;
                Bone(a,1,new Vector3(sign*.34f,.19f,0));Bone(a+1,a,new Vector3(0,-.29f,0));Bone(a+2,a+1,new Vector3(0,-.26f,0));
                Bone(l,0,new Vector3(sign*.17f,-.01f,0));Bone(l+1,l,new Vector3(0,-.36f,0));Bone(l+2,l+1,new Vector3(0,-.34f,0));
            }
            var vertices=new List<Vector3>();var weights=new List<BoneWeight>();var triangles=new[]{new List<int>(),new List<int>(),new List<int>()};
            void Plate(int bone,Vector3 center,Vector3 size,int material,float taper=1)
            {
                // Octagonal cross-section, inset top/bottom rings: bevels catch light while keeping flat panels.
                Vector2[] rim={new Vector2(-.32f,-.5f),new Vector2(.32f,-.5f),new Vector2(.5f,-.32f),new Vector2(.5f,.32f),new Vector2(.32f,.5f),new Vector2(-.32f,.5f),new Vector2(-.5f,.32f),new Vector2(-.5f,-.32f)};
                Vector3 origin=model.joints[bone].position;
                Vector3 Point(int ring,int i)
                {float y=new[]{-.5f,-.35f,.35f,.5f}[ring];float inset=ring==0||ring==3?.78f:1;float t=Mathf.Lerp(1,taper,y+.5f);var p=rim[i%8];return origin+center+new Vector3(p.x*size.x*inset*t,y*size.y,p.y*size.z*inset);}
                void Tri(Vector3 a,Vector3 b,Vector3 c)
                {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);for(int i=0;i<3;i++){weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});triangles[material].Add(n+i);}}
                for(int r=0;r<3;r++)for(int i=0;i<8;i++)
                {var a=Point(r,i);var b=Point(r,i+1);var c=Point(r+1,i+1);var d=Point(r+1,i);Tri(a,c,b);Tri(a,d,c);}
                for(int i=1;i<7;i++){Tri(Point(0,0),Point(0,i),Point(0,i+1));Tri(Point(3,0),Point(3,i+1),Point(3,i));}
            }
            bool elite=kind==4,heavy=kind==3||elite;float chest=heavy?.77f:.56f;
            Plate(0,Vector3.zero,new Vector3(.47f,.21f,.32f),1);
            Plate(1,new Vector3(0,.10f,0),new Vector3(chest,.40f,.35f),0,.8f);
            Plate(1,new Vector3(0,.10f,.20f),new Vector3(chest*.64f,.23f,.07f),1);
            Plate(1,new Vector3(0,.12f,.25f),new Vector3(.10f,.17f,.05f),2);
            Plate(2,new Vector3(0,.05f,0),new Vector3(.36f,.36f,.37f),1,.8f);
            Plate(2,new Vector3(0,.09f,.19f),new Vector3(.31f,.072f,.045f),2);
            Plate(2,new Vector3(0,-.08f,.17f),new Vector3(.21f,.10f,.10f),0);
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;int a=side==0?3:6,l=side==0?9:12;
                Plate(a,new Vector3(sign*.045f,0,0),new Vector3(heavy?.4f:.26f,.22f,.38f),0);
                Plate(a,new Vector3(0,-.14f,0),new Vector3(.16f,.27f,.18f),1);
                Plate(a+1,new Vector3(0,-.12f,.035f),new Vector3(heavy?.25f:.20f,.29f,.24f),0,.8f);
                Plate(a+1,new Vector3(0,-.12f,.165f),new Vector3(.07f,.16f,.035f),2);
                Plate(a+2,new Vector3(0,-.035f,.025f),new Vector3(.15f,.13f,.18f),1);
                Plate(l,new Vector3(0,-.17f,0),new Vector3(.24f,.33f,.26f),0,.85f);
                Plate(l+1,new Vector3(0,0,.15f),new Vector3(.22f,.15f,.09f),1);
                Plate(l+1,new Vector3(0,-.17f,.015f),new Vector3(.21f,.31f,.25f),0,.8f);
                Plate(l+2,new Vector3(0,0,.07f),new Vector3(.25f,.15f,.39f),1);
                if(kind==2)Plate(a,new Vector3(sign*.07f,.16f,-.14f),new Vector3(.1f,.5f,.30f),2,.3f);
                if(heavy)for(int i=0;i<3;i++)Plate(a,new Vector3(sign*.10f,.04f-i*.06f,.06f),new Vector3(.40f,.09f,.46f),i==0?2:0);
            }
            if(elite)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    Plate(1,new Vector3(side*.32f,.28f,-.30f),new Vector3(.27f,.58f,.30f),1);
                    Plate(1,new Vector3(side*.32f,.38f,-.48f),new Vector3(.13f,.26f,.05f),2);
                    Plate(2,new Vector3(side*.20f,.28f,0),new Vector3(.11f,.38f,.14f),0,.35f);
                }
                Plate(8,new Vector3(0,-.32f,0),new Vector3(.08f,.95f,.08f),1);
                Plate(8,new Vector3(0,-.69f,0),new Vector3(.68f,.31f,.35f),0);
                Plate(8,new Vector3(0,-.69f,.18f),new Vector3(.55f,.11f,.04f),2);
            }
            Color main=kind==0?new Color(.65f,.73f,.76f):kind==2?new Color(.25f,.18f,.36f):elite?new Color(.47f,.30f,.15f):new Color(.28f,.35f,.40f);
            Color light=kind==0?new Color(.04f,.86f,1):kind==2?new Color(.8f,.15f,1):elite?new Color(1,.42f,.05f):new Color(1,.18f,.09f);
            var mats=new[]{RuntimeArt.Material(main),RuntimeArt.Material(new Color(.045f,.07f,.10f)),RuntimeArt.Material(light,1.5f)};
            var mesh=new Mesh{name=root.name+" / bevel armor skin"};mesh.SetVertices(vertices);mesh.boneWeights=weights.ToArray();mesh.subMeshCount=3;
            for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);
            var binds=new Matrix4x4[15];for(int i=0;i<15;i++)binds[i]=model.joints[i].worldToLocalMatrix*root.transform.localToWorldMatrix;
            mesh.bindposes=binds;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var skin=new GameObject("Armor Skin").AddComponent<SkinnedMeshRenderer>();skin.transform.SetParent(root.transform,false);skin.sharedMesh=mesh;skin.sharedMaterials=mats;skin.bones=model.joints;skin.rootBone=model.joints[0];skin.localBounds=new Bounds(Vector3.up*.9f,new Vector3(3,3,3));skin.updateWhenOffscreen=true;model.skin=skin;
            return model;
        }
    }
}
