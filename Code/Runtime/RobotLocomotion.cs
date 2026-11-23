using UnityEngine;

namespace NeonArena
{
    // Presentation only: the CharacterController remains the movement authority.
    // Two-bone solutions use measured source bone lengths, not humanoid assumptions.
    public sealed class RobotLocomotion
    {
        readonly CharacterModel model;
        readonly PlayerController player;
        readonly Vector3[] rest = new Vector3[15];
        readonly Quaternion[] rotation = new Quaternion[15];
        readonly Vector3[] feet = new Vector3[2];
        readonly Vector3[] target = new Vector3[15];
        float phase, landing, recoil, airTime, turnLean;
        bool grounded = true;
        int shots;
        float oldYaw;
        Vector3 localVelocity;
        public float FootLift { get; private set; }
        public RobotLocomotion(CharacterModel m, PlayerController p)
        {
            model=m;player=p;oldYaw=p.visual.eulerAngles.y;
            for(int i=0;i<15;i++){rest[i]=m.joints[i].localPosition;rotation[i]=m.joints[i].localRotation;}
            for(int i=0;i<2;i++)feet[i]=m.transform.InverseTransformPoint(m.joints[i==0?11:14].position);
        }
        public void Reset(){phase=landing=recoil=airTime=turnLean=0;localVelocity=Vector3.zero;grounded=true;shots=player.weapon.ShotsFired;}
        public void Evaluate(float dt,Vector3 velocity,MotionState state)
        {
            var b=model.joints;float blend=1-Mathf.Exp(-18*dt);
            localVelocity=Vector3.Lerp(localVelocity,player.visual.InverseTransformDirection(velocity),blend);
            float speed=localVelocity.magnitude,weight=Mathf.Clamp01(speed/3);
            // Distance-driven cycle stops when blocked by a wall; one full stride is 2.5 m.
            if(!player.Dash.IsActive)phase+=speed*dt*2*Mathf.PI/2.5f;
            bool onGround=player.Grounded;
            if(onGround&&!grounded)landing=1;
            grounded=onGround;airTime=onGround?0:airTime+dt;
            landing=Mathf.MoveTowards(landing,0,dt*5);
            if(shots!=player.weapon.ShotsFired){shots=player.weapon.ShotsFired;recoil=1;}
            recoil=Mathf.MoveTowards(recoil,0,dt*12);
            float yaw=player.visual.eulerAngles.y;
            turnLean=Mathf.Lerp(turnLean,Mathf.Clamp(Mathf.DeltaAngle(oldYaw,yaw)/Mathf.Max(dt,.001f)*.025f,-9,9),blend);oldYaw=yaw;
            float crouch=landing*.14f;
            System.Array.Clear(target,0,target.Length);
            float directionalYaw=Mathf.Atan2(localVelocity.x,Mathf.Abs(localVelocity.z)+.5f)*Mathf.Rad2Deg*.30f*weight;
            target[0]=new Vector3(speed*.55f,directionalYaw,-localVelocity.x*1.2f-turnLean);
            target[1]=new Vector3(-recoil*9,-directionalYaw*.8f,turnLean*.5f);
            target[2]=new Vector3(0,-directionalYaw*.2f,turnLean*.5f);
            for(int side=0;side<2;side++)
            {
                int hip=side==0?9:12;float swing=Mathf.Sin(phase+side*Mathf.PI)*weight;
                target[hip]=new Vector3(swing*29*Mathf.Sign(localVelocity.z==0?1:localVelocity.z)-landing*23,0,-swing*localVelocity.x*3);
                target[hip+1]=new Vector3(Mathf.Max(0,-swing)*47+landing*45,0,0);
                if(!onGround)
                {
                    float launch=Mathf.Clamp01(airTime/.14f);
                    target[hip]=new Vector3(Mathf.Lerp(8,side==0?-42:18,launch),0,side==0?-6:6);
                    target[hip+1]=new Vector3(player.VerticalSpeed>0?65:30,0,0);
                    target[hip+2]=new Vector3(-12,0,0);
                    if(player.Flight.Thrusting){target[hip].x=20;target[hip+1].x=48;}
                }
            }
            if(player.Dash.IsActive)
            {
                Vector3 dash=player.visual.InverseTransformDirection(player.Dash.Direction);
                target[0]=new Vector3(dash.z*27,0,-dash.x*22);
                target[9]=new Vector3(-55,0,-dash.x*16);target[12]=new Vector3(32,0,-dash.x*16);
                target[10].x=80;target[13].x=25;crouch=.12f;
            }
            if(state==MotionState.Hurt)target[1].x-=15;
            if(state==MotionState.Slash)
            {
                float t=Mathf.Clamp01(1-player.Overheat.Cooldown/PlayerOverheat.StrikeInterval);
                target[1].y=Mathf.Sin(t*Mathf.PI)*35;
                target[6]=new Vector3(-80,Mathf.Lerp(-80,105,t),player.Overheat.Style==1?-45:15);
                target[7].x=-25;target[3]=new Vector3(15,0,-35);
            }
            for(int i=0;i<15;i++)b[i].localRotation=Quaternion.Slerp(b[i].localRotation,rotation[i]*Quaternion.Euler(target[i]),blend);
            b[0].localPosition=Vector3.Lerp(b[0].localPosition,rest[0]+Vector3.up*(Mathf.Cos(phase*2)*.024f*weight-crouch),blend);
            FootLift=0;
            if(onGround&&!player.Dash.IsActive)
            {
                Vector3 direction=localVelocity.sqrMagnitude>.05f?localVelocity.normalized:Vector3.forward;
                for(int side=0;side<2;side++)
                {
                    int hip=side==0?9:12;float a=phase+side*Mathf.PI;
                    float lift=Mathf.Max(0,Mathf.Sin(a))*.18f*weight;
                    FootLift=Mathf.Max(FootLift,lift);
                    var foot=feet[side]+direction*(Mathf.Cos(a)*.30f*weight);
                    foot.x+=side==0?-.035f:.035f;
                    var desired=model.transform.TransformPoint(foot);
                    if(Physics.Raycast(desired+Vector3.up*.65f,Vector3.down,out var hit,1.15f,1<<9,QueryTriggerInteraction.Ignore)&&hit.normal.y>.65f)
                        desired.y=hit.point.y+feet[side].y+lift;
                    else desired.y+=lift;
                    Solve(b[hip],b[hip+1],b[hip+2],desired,player.visual.forward);
                    b[hip+2].rotation=Quaternion.Slerp(b[hip+2].rotation,player.visual.rotation,blend);
                }
            }
            if(state!=MotionState.Slash)
            {
                // Both hands follow the actual gun and its recoil, even while strafing/jumping.
                Vector3 muzzle=player.weapon.muzzle.position;
                Vector3 f=player.weapon.muzzle.forward,r=player.weapon.muzzle.right;
                Solve(b[6],b[7],b[8],player.weapon.visuals.RightGrip,r-Vector3.up*.6f);
                Solve(b[3],b[4],b[5],player.weapon.visuals.LeftGrip,-r-Vector3.up*.4f);
                b[8].rotation=player.weapon.muzzle.rotation;b[5].rotation=player.weapon.muzzle.rotation;
            }
        }
        public static void Solve(Transform upper,Transform lower,Transform tip,Vector3 target,Vector3 pole)
        {
            Vector3 start=upper.position,delta=target-start;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,tip.position);
            if(a<.001f||b<.001f||delta.sqrMagnitude<.00001f)return;
            float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);Vector3 axis=delta.normalized;
            // Preserve the existing bend plane near pole alignment. A fixed cross with
            // world-right also becomes zero for horizontal reaches and can flip a limb.
            Vector3 bend=Vector3.ProjectOnPlane(pole,axis);
            if(bend.sqrMagnitude<.0001f)bend=Vector3.ProjectOnPlane(lower.position-start,axis);
            if(bend.sqrMagnitude<.0001f)bend=Vector3.ProjectOnPlane(Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.forward,axis);
            bend.Normalize();
            float x=(a*a+d*d-b*b)/(2*d),y=Mathf.Sqrt(Mathf.Max(0,a*a-x*x));
            Vector3 elbow=start+axis*x+bend*y;
            upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(tip.position-lower.position,start+axis*d-lower.position)*lower.rotation;
        }
    }
}
