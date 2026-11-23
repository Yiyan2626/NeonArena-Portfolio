using UnityEngine;

namespace NeonArena
{
    public enum MotionState { Idle,Run,Jump,Fall,Jet,Land,Dash,Aim,Hurt,Slash,Windup,Attack,Recover }
    [DefaultExecutionOrder(100)]
    public sealed class CharacterMotion : MonoBehaviour
    {
        public CharacterModel Model { get; private set; }
        public MotionState State { get; private set; }
        public float Speed { get; private set; }
        public float BodyCompression => retargeted?.Compression ?? 0;
        public float AirTuck => retargeted?.AirTuck ?? 0;
        public float DashBrace => retargeted?.DashBrace ?? 0;
        PlayerController player;EnemyController enemy;Health health;
        Transform visual;Vector3 previous;float cycle,hurt,land,attack,recoil;bool wasGrounded=true;
        int shots;Vector3[] rest;readonly Vector3[] pose=new Vector3[15];RobotLocomotion robot;
        RetargetedMotion retargeted;
        static readonly string[] Keys={"Ion Ranger","Hunter Automaton","Strider Automaton","Bulwark Automaton","Aegis Warden","Guardian Sentry"};
        public void Initialize(PlayerController p,EnemyController e)
        {
            player=p;enemy=e;visual=p?p.visual:e.visual;health=GetComponent<Health>();health.Damaged+=OnDamage;
            foreach(Transform child in visual)
                if(child.name=="Torso"||child.name=="Helmet"||child.name=="Visor"||child.name=="Chest Core"||child.name=="Boot"||child.name=="Shoulder"||child.name=="Armored Shell"||child.name=="Eye Bar"||child.name=="Core"||child.name=="Top Reactor"||child.name=="Tread")child.gameObject.SetActive(false);
            SetKind(p?0:1);ResetMotion();
        }
        public void SetKind(int kind)
        {
            if(Model && Model.kind==kind)return;
            if(Model){Model.gameObject.SetActive(false);Destroy(Model.gameObject);}
            bool frontier=ArenaGame.Instance&&ArenaGame.Instance.Frontier&&(kind==0||kind==4);
            var asset=Resources.Load<GameObject>(frontier?"Frontier/"+(kind==0?"Ranger":"Titan"):"Characters/"+Keys[kind]);
            var prefab=asset?asset.GetComponent<CharacterModel>():null;
            if(frontier&&!prefab)Debug.LogError("Missing frontier character prefab kind="+kind);
            Model=prefab?Instantiate(prefab,visual):CharacterModel.Create(kind==5?1:kind);
            Model.transform.SetParent(visual,false);Model.transform.localScale=Vector3.one*(frontier?1:kind==4?1.5f:player?1:.92f);
            rest=new Vector3[Model.joints.Length];for(int i=0;i<rest.Length;i++)rest[i]=Model.joints[i].localPosition;
            robot=player&&Model.imported&&!frontier?new RobotLocomotion(Model,player):null;
            retargeted=player&&frontier?new RetargetedMotion(Model,player):null;
            var flash=GetComponent<HitFlash>();if(flash)flash.surfaces=new Renderer[]{Model.skin};
        }
        float nextHurt;
        void OnDamage(float amount){if(enemy&&Time.time<nextHurt)return;hurt=.15f;nextHurt=Time.time+.24f;}
        public void Melee(){attack=.26f;}
        public void ResetMotion(){previous=transform.position;Speed=cycle=hurt=land=attack=recoil=0;State=MotionState.Idle;wasGrounded=true;shots=0;robot?.Reset();retargeted?.Reset();}
        void LateUpdate(){Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            if(!Model||dt<=0||!ArenaGame.Instance||!ArenaGame.Instance.IsPlaying)return;
            Vector3 displacement=transform.position-previous;previous=transform.position;
            if(displacement.sqrMagnitude>9){displacement=Vector3.zero;retargeted?.Reset();robot?.Reset();}
            var velocity=Vector3.ProjectOnPlane(displacement,Vector3.up)/dt;
            Speed=Mathf.Lerp(Speed,velocity.magnitude,1-Mathf.Exp(-14*dt));cycle+=dt*(2+Mathf.Min(Speed,8)*1.5f);
            hurt=Mathf.Max(0,hurt-dt);land=Mathf.Max(0,land-dt);attack=Mathf.Max(0,attack-dt);recoil=Mathf.Max(0,recoil-dt);
            bool grounded=!player||player.Grounded;
            if(player){if(grounded&&!wasGrounded)land=.17f;wasGrounded=grounded;if(player.weapon.ShotsFired!=shots){shots=player.weapon.ShotsFired;recoil=.10f;}}
            State=hurt>0?MotionState.Hurt:player&&player.Overheat.Striking?MotionState.Slash:player&&player.Dash.IsActive?MotionState.Dash:player&&player.Flight.Thrusting?MotionState.Jet:!grounded?(player.VerticalSpeed>0?MotionState.Jump:MotionState.Fall):land>0?MotionState.Land:attack>0?MotionState.Attack:Speed>.3f?MotionState.Run:player&&player.Focus.HasTarget?MotionState.Aim:MotionState.Idle;
            if(enemy && enemy.Elite && enemy.Elite.IsElite)
            {if(enemy.Elite.Phase==ElitePhase.Windup)State=MotionState.Windup;else if(enemy.Elite.Phase==ElitePhase.Attack)State=MotionState.Attack;else if(enemy.Elite.Phase==ElitePhase.Recovery)State=MotionState.Recover;}
            if(retargeted!=null){retargeted.Evaluate(dt,velocity,State);return;}
            if(robot!=null){robot.Evaluate(dt,velocity,State);return;}
            if(Model.imported&&enemy&&ArenaGame.Instance.Frontier&&Model.kind==4)
            {
                var j=Model.joints;float titanStride=Mathf.Sin(cycle)*Mathf.Clamp01(Speed/2);
                for(int i=0;i<j.Length;i++)j[i].localRotation=Quaternion.identity;
                j[9].localRotation=Quaternion.Euler(titanStride*18,0,0);j[12].localRotation=Quaternion.Euler(-titanStride*18,0,0);
                j[10].localRotation=Quaternion.Euler(Mathf.Max(0,-titanStride)*24,0,0);j[13].localRotation=Quaternion.Euler(Mathf.Max(0,titanStride)*24,0,0);
                j[6].localRotation=Quaternion.Euler(-25,0,35);j[7].localRotation=Quaternion.Euler(-30,0,0);
                j[3].localRotation=Quaternion.Euler(0,0,-55);j[4].localRotation=Quaternion.Euler(-40,0,0);return;
            }
            if(Model.imported && !player)
            {
                foreach(var bone in Model.sourceBones)
                {
                    string n=bone.name;float swing=Mathf.Sin(cycle+(n.Contains("_R")?Mathf.PI:0))*Mathf.Clamp01(Speed/2)*16;
                    if(n.Contains("leg"))bone.localRotation=Quaternion.Euler(swing,0,swing*.25f);
                    else if(n.Contains("Tentacle")||n.Contains("tail"))bone.localRotation=Quaternion.Euler(Mathf.Sin(cycle*.4f+bone.GetSiblingIndex())*5,0,Mathf.Cos(cycle*.35f)*4);
                    else if(n.Contains("Mask")||n.Contains("Face_Guard"))bone.localRotation=Quaternion.Euler(0,0,enemy.Ranged&&enemy.Ranged.Charging?Mathf.Sin(Time.time*18)*7:0);
                }
                Model.transform.localPosition=Vector3.up*(Model.kind==5?.18f+Mathf.Sin(cycle*.6f)*.08f:Mathf.Abs(Mathf.Sin(cycle))*.025f)-Vector3.forward*(hurt/.15f)*.14f;
                Model.transform.localRotation=Quaternion.Euler(-hurt/.15f*13,0,Mathf.Sin(hurt*70)*hurt*28);
                return;
            }
            float stride=Mathf.Sin(cycle)*Mathf.Clamp01(Speed/4),bob=Mathf.Abs(Mathf.Sin(cycle))*Mathf.Clamp01(Speed/4)*.045f;
            System.Array.Clear(pose,0,pose.Length);
            pose[9].x=stride*32;pose[12].x=-stride*32;pose[10].x=Mathf.Max(0,-stride)*38;pose[13].x=Mathf.Max(0,stride)*38;
            pose[3].x=-stride*22;pose[6].x=stride*22;pose[4].x=pose[7].x=-15;
            if(player && Speed>.3f)
            {
                Vector3 local=visual.InverseTransformDirection(velocity);
                float hipYaw=Mathf.Atan2(local.x,Mathf.Abs(local.z)+.1f)*Mathf.Rad2Deg*.45f;
                pose[0].y=hipYaw;pose[1].y=-hipYaw;
                if(local.z<-.1f){pose[9].x=-pose[9].x;pose[12].x=-pose[12].x;}
            }
            if(player){pose[3]=new Vector3(-32,20,-65);pose[4].x=-40;pose[6]=new Vector3(-20,0,8);pose[7].x=-55;pose[1].x=-recoil*45;}
            if(player && Model.imported){pose[3]=new Vector3(-10,0,5);pose[4].x=-15;pose[6]=new Vector3(-35,0,-5);pose[7].x=-50;}
            if(State==MotionState.Jump||State==MotionState.Fall||State==MotionState.Jet)
            {pose[9].x=-20;pose[12].x=15;pose[10].x=45;pose[13].x=30;pose[1].x=State==MotionState.Jet?12:-8;}
            if(State==MotionState.Dash){pose[1].x=30;pose[9].x=-50;pose[12].x=40;}
            if(State==MotionState.Land){bob=-.10f;pose[10].x=pose[13].x=30;pose[9].x=pose[12].x=-20;}
            if(State==MotionState.Hurt){pose[1].x=-18;pose[2].x=-10;}
            if(State==MotionState.Slash)
            {float t=1-player.Overheat.Cooldown/PlayerOverheat.StrikeInterval;pose[6]=new Vector3(-70,Mathf.Lerp(-65,85,t),player.Overheat.Style==1?-45:10);pose[7].x=-15;pose[3]=new Vector3(15,0,-30);pose[1].y=Mathf.Sin(t*Mathf.PI)*25;}
            if(State==MotionState.Windup)
            {
                var move=enemy.Elite.Move;
                if(move==EliteMove.HammerSweep){pose[6]=new Vector3(-65,-70,25);pose[7].x=-25;pose[1].y=-20;pose[3].z=-30;}
                else if(move==EliteMove.Ram){pose[1].x=30;pose[2].x=-18;pose[6].x=pose[3].x=-65;pose[7].x=pose[4].x=-35;}
                else {pose[6].x=-155;pose[7].x=-15;pose[3].z=-35;pose[9].x=pose[12].x=-20;pose[10].x=pose[13].x=30;bob=-.08f;}
            }
            if(State==MotionState.Attack)
            {
                pose[7].x=-15;pose[1].x=25;
                if(enemy&&enemy.Elite.IsElite)
                {
                    if(enemy.Elite.Move==EliteMove.HammerSweep)pose[6]=new Vector3(-65,Mathf.Lerp(-70,100,1-enemy.Elite.Remaining/.25f),25);
                    else if(enemy.Elite.Move==EliteMove.Ram){pose[6].x=pose[3].x=-70;pose[1].x=38;pose[9].x=-45;pose[12].x=35;}
                    else {pose[6].x=25;pose[1].x=40;bob=-.12f;}
                }
                else pose[6].x=-45;
            }
            if(State==MotionState.Recover){pose[1].x=20;pose[2].x=15;}
            float blend=1-Mathf.Exp(-20*dt);
            for(int i=0;i<15;i++)Model.joints[i].localRotation=Quaternion.Slerp(Model.joints[i].localRotation,Quaternion.Euler(pose[i]),blend);
            Model.joints[0].localPosition=rest[0]+Vector3.up*(bob+Mathf.Sin(cycle*.5f)*.006f);
        }
        void OnDestroy(){if(health)health.Damaged-=OnDamage;}
    }
}
