using UnityEngine;

namespace NeonArena
{
    // Animator is the only clip player. The post-animation pass owns directional feet
    // and the rifle chain; neither pass moves the controller or a camera transform.
    public sealed class RetargetedMotion
    {
        readonly Animator animator;
        readonly CharacterModel model;
        readonly PlayerController player;
        readonly Vector3 hipRest;
        readonly Vector3[] footRest = new Vector3[2];
        readonly Vector3[] sampledFeet = new Vector3[2];
        readonly Quaternion[] sampledFootRotation = new Quaternion[2];
        readonly Quaternion[] footBindRotation = new Quaternion[2];
        readonly Quaternion[] contactRotation = new Quaternion[2];
        readonly Vector3[] plantedFoot = new Vector3[2];
        readonly Vector3[] footContactOffset = new Vector3[2];
        readonly float[] contactWeight = new float[2], previousFootHeight = new float[2], plantTime = new float[2];
        readonly bool[] canPlant = { true, true };
        bool footHistory;
        readonly Quaternion[] armRest = new Quaternion[6];
        readonly Transform[] shoulders = new Transform[2];
        readonly Quaternion[] shoulderRest = new Quaternion[2];
        readonly Quaternion[] handFrame = new Quaternion[2];
        readonly Quaternion[] heldWrist = new Quaternion[2];
        readonly Vector3[] palmOffset = new Vector3[2];
        readonly Quaternion[] bladeArmPose = new Quaternion[6];
        readonly Transform[] fingers = new Transform[30];
        readonly Quaternion[] fingerRest = new Quaternion[30];
        readonly Vector3[] fingerCurlAxis = new Vector3[30];
        float bladeReturn;
        bool heldBlade;
        bool wristHistory;
        string current = "";
        bool wasGrounded = true;
        float landing, recoil, moveYaw, groundWeight = 1, hurtWeight, dashWeight, speed;
        float gaitSway, gaitBounce;
        float airTime, landingStrength, previousVertical, launchWeight, airTuck, compression, landingPoseWeight;
        Vector3 dashDirection = Vector3.forward;
        public float Compression => compression;
        public float AirTuck => airTuck;
        public float DashBrace => dashWeight;
        Vector3 strideDirection = Vector3.forward;
        int shots;

        public RetargetedMotion(CharacterModel m, PlayerController p)
        {
            model=m;player=p;animator=m.GetComponent<Animator>();
            hipRest=BindPose(m.joints[0]).GetColumn(3);
            if(!animator)return;
            animator.applyRootMotion=false;
            for(int side=0;side<2;side++)
            {
                int arm=side==0?3:6;
                footRest[side]=BindPose(m.joints[side==0?11:14]).GetColumn(3);
                footBindRotation[side]=BindPose(m.joints[side==0?11:14]).rotation;
                shoulders[side]=animator.GetBoneTransform(side==0?HumanBodyBones.LeftShoulder:HumanBodyBones.RightShoulder);
                if(shoulders[side])shoulderRest[side]=BindLocalRotation(shoulders[side]);
                for(int i=0;i<3;i++)armRest[side*3+i]=BindLocalRotation(m.joints[arm+i]);
                var hand=m.joints[arm+2];
                var middle=animator.GetBoneTransform(side==0?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
                var thumb=animator.GetBoneTransform(side==0?HumanBodyBones.LeftThumbProximal:HumanBodyBones.RightThumbProximal);
                var bind=BindPose(hand);Vector3 origin=bind.GetColumn(3);
                Vector3 fingers=middle?(Vector3)BindPose(middle).GetColumn(3)-origin:Vector3.right*(side==0?-1:1);
                Vector3 thumbAxis=thumb?(Vector3)BindPose(thumb).GetColumn(3)-origin:Vector3.forward;
                handFrame[side]=Quaternion.Inverse(bind.rotation)*Quaternion.LookRotation(fingers,Vector3.ProjectOnPlane(thumbAxis,fingers));
                // Wrist origins are behind the palm, not at the cylindrical handle.
                palmOffset[side]=handFrame[side]*new Vector3(side==0?.022f:-.022f,0,.065f);
            }
            var fingerBones=new[]{HumanBodyBones.LeftIndexProximal,HumanBodyBones.LeftIndexIntermediate,HumanBodyBones.LeftIndexDistal,
                HumanBodyBones.LeftMiddleProximal,HumanBodyBones.LeftMiddleIntermediate,HumanBodyBones.LeftMiddleDistal,
                HumanBodyBones.LeftRingProximal,HumanBodyBones.LeftRingIntermediate,HumanBodyBones.LeftRingDistal,
                HumanBodyBones.LeftLittleProximal,HumanBodyBones.LeftLittleIntermediate,HumanBodyBones.LeftLittleDistal,
                HumanBodyBones.LeftThumbProximal,HumanBodyBones.LeftThumbIntermediate,HumanBodyBones.LeftThumbDistal,
                HumanBodyBones.RightIndexProximal,HumanBodyBones.RightIndexIntermediate,HumanBodyBones.RightIndexDistal,
                HumanBodyBones.RightMiddleProximal,HumanBodyBones.RightMiddleIntermediate,HumanBodyBones.RightMiddleDistal,
                HumanBodyBones.RightRingProximal,HumanBodyBones.RightRingIntermediate,HumanBodyBones.RightRingDistal,
                HumanBodyBones.RightLittleProximal,HumanBodyBones.RightLittleIntermediate,HumanBodyBones.RightLittleDistal,
                HumanBodyBones.RightThumbProximal,HumanBodyBones.RightThumbIntermediate,HumanBodyBones.RightThumbDistal};
            for(int i=0;i<fingers.Length;i++)
            {
                fingers[i]=animator.GetBoneTransform(fingerBones[i]);
                // This imported avatar maps the wrists but its Humanoid hand tables
                // are empty. The original weighted finger bones still exist in the
                // skin: solve those directly instead of silently skipping every curl.
                if(!fingers[i])
                {
                    string name=new[]{"Index","Middle","Ring","Little","Thumb"}[(i%15)/3]+(i%3+1)+(i<15?"_L":"_R");
                    foreach(var source in model.sourceBones)if(source&&source.name==name){fingers[i]=source;break;}
                }
                if(!fingers[i])continue;
                int side=i/15;var hand=m.joints[side==0?5:8];
                fingerRest[i]=BindLocalRotation(fingers[i]);
                fingerCurlAxis[i]=Quaternion.Inverse(BindPose(fingers[i]).rotation)*BindPose(hand).rotation*handFrame[side]*Vector3.up;
            }
        }

        Matrix4x4 BindPose(Transform bone)
        {
            var bones=model.skin.bones;var poses=model.skin.sharedMesh.bindposes;
            for(int i=0;i<bones.Length;i++)if(bones[i]==bone)return poses[i].inverse;
            return model.transform.worldToLocalMatrix*bone.localToWorldMatrix;
        }
        Quaternion BindLocalRotation(Transform bone)
        {
            return bone.parent&&bone.parent!=model.transform
                ?Quaternion.Inverse(BindPose(bone.parent).rotation)*BindPose(bone).rotation
                :BindPose(bone).rotation;
        }
        public void Reset()
        {
            current="";landing=recoil=moveYaw=hurtWeight=dashWeight=speed=gaitSway=gaitBounce=0;
            wasGrounded=player.Grounded;groundWeight=wasGrounded?1:0;
            airTime=landingStrength=previousVertical=launchWeight=airTuck=compression=landingPoseWeight=0;
            dashDirection=Vector3.forward;
            strideDirection=Vector3.forward;shots=player.weapon.ShotsFired;
            footHistory=wristHistory=false;
            heldBlade=false;bladeReturn=0;
            CurlFingers(0);
            for(int side=0;side<2;side++){contactWeight[side]=plantTime[side]=0;canPlant[side]=true;footContactOffset[side]=Vector3.zero;}
            if(animator)animator.speed=1;
        }
        public void Evaluate(float dt,Vector3 velocity,MotionState state)
        {
            if(!animator||!animator.avatar||!animator.avatar.isHuman||dt<=0)return;
            float blend=1-Mathf.Exp(-14*dt);
            speed=Mathf.Lerp(speed,velocity.magnitude,blend);
            bool grounded=player.Grounded;
            if(grounded&&!wasGrounded)
            {
                landing=.38f;
                landingStrength=Mathf.Lerp(.45f,1,Mathf.InverseLerp(2,10,-previousVertical));
            }
            if(!grounded&&wasGrounded)airTime=0;
            airTime=grounded?0:airTime+dt;
            previousVertical=player.VerticalSpeed;
            wasGrounded=grounded;landing=Mathf.Max(0,landing-dt);
            float landAge=.38f-landing;
            // Briefly hold the loaded shape before releasing into the next stride.
            float landPulse=landing>0?Mathf.SmoothStep(0,1,landAge/.065f)*Mathf.SmoothStep(0,1,landing/.23f)*landingStrength:0;
            landingPoseWeight=grounded?landPulse:0;
            // The controller jumps immediately. Compress in the first visible launch
            // frames, extend through push-off, then gather the feet at the apex.
            launchWeight=!grounded&&player.VerticalSpeed>0?1-Mathf.SmoothStep(0,1,airTime/.085f):0;
            float tuckTarget=!grounded?Mathf.SmoothStep(0,1,(airTime-.065f)/.13f)*(1-Mathf.SmoothStep(0,1,(-player.VerticalSpeed-2)/6)):0;
            airTuck=Mathf.Lerp(airTuck,tuckTarget,1-Mathf.Exp(-22*dt));
            dashWeight=Mathf.Lerp(dashWeight,player.Dash.IsActive?1:0,1-Mathf.Exp(-(player.Dash.IsActive?24:10)*dt));
            if(player.Dash.IsActive)dashDirection=player.visual.InverseTransformDirection(player.Dash.Direction).normalized;
            compression=landPulse*.17f+launchWeight*.12f+dashWeight*.10f;
            string clip=state==MotionState.Dash?"Sprint":!grounded?(player.VerticalSpeed>0?"Jump":"Fall"):
                landing>0?"Land":speed>3?"Run":speed>.3f?"Walk":"Idle";
            if(!animator.HasState(0,Animator.StringToHash(clip)))clip=clip=="Sprint"?"Run":clip=="Fall"?"Jump":"Idle";
            if(current!=clip)
            {
                // Preserve the footfall phase when changing gait; never restart the next
                // run on its first contact while the previous leg is still in flight.
                bool gait=IsGait(clip)&&IsGait(current);
                var info=animator.GetCurrentAnimatorStateInfo(0);
                float phase=gait?Mathf.Repeat(info.normalizedTime,1):0;
                animator.CrossFade(clip,.16f,0,phase);current=clip;
            }
            float playback=clip=="Run"?Mathf.Clamp(speed/4,.8f,1.65f):clip=="Walk"?Mathf.Clamp(speed/1.7f,.65f,1.5f):clip=="Sprint"?1.5f:1;
            animator.speed=Mathf.Lerp(animator.speed,playback,blend);
            var b=model.joints;
            b[0].localPosition=new Vector3(hipRest.x,Mathf.Clamp(b[0].localPosition.y,hipRest.y-.12f,hipRest.y+.08f),hipRest.z);
            // Capture untouched mocap foot trajectories before the upper/lower split.
            for(int side=0;side<2;side++)
            {
                var foot=b[side==0?11:14];
                sampledFeet[side]=model.transform.InverseTransformPoint(foot.position);
                sampledFootRotation[side]=foot.rotation;
            }
            Vector3 local=player.visual.InverseTransformDirection(velocity);local.y=0;
            Vector3 desired=local.sqrMagnitude>.16f?local.normalized:Vector3.forward;
            strideDirection=Vector3.Lerp(strideDirection,desired,blend);
            groundWeight=Mathf.Lerp(groundWeight,grounded?1:0,blend);
            // Feet describe the travel direction. The pelvis only turns within a natural
            // aiming stance; a backward stride never reflects a Humanoid Euler angle.
            float desiredYaw=Mathf.Atan2(local.x,Mathf.Abs(local.z)+.1f)*Mathf.Rad2Deg*.35f*Mathf.Clamp01(speed);
            moveYaw=Mathf.Lerp(moveYaw,grounded?desiredYaw:0,blend);
            b[0].rotation=Quaternion.AngleAxis(moveYaw,Vector3.up)*b[0].rotation;
            b[1].rotation=Quaternion.AngleAxis(-moveYaw,Vector3.up)*b[1].rotation;
            // Move the pelvis, never scale the model or move the camera/controller.
            // Feet were captured first so grounded contacts absorb this displacement
            // through knee flexion instead of sinking through the floor.
            float pushOff=!grounded?Mathf.Sin(Mathf.Clamp01(airTime/.17f)*Mathf.PI)*.045f:0;
            b[0].localPosition+=Vector3.up*(pushOff-compression);
            float bracePitch=dashDirection.z*dashWeight*19+landPulse*11+launchWeight*9;
            float braceRoll=-dashDirection.x*dashWeight*17;
            b[0].rotation=Quaternion.AngleAxis(braceRoll,player.visual.forward)*Quaternion.AngleAxis(bracePitch,player.visual.right)*b[0].rotation;
            b[1].rotation=Quaternion.AngleAxis(-bracePitch*.30f,player.visual.right)*Quaternion.AngleAxis(-braceRoll*.28f,player.visual.forward)*b[1].rotation;
            ApplyFeet(grounded,dt);

            // Overheat owns the entire upper-body mode, including idle/run. A hidden
            // rifle must never pull the support hand away from the blade handle.
            if(player.Overheat.Active){ApplyBlade(dt);return;}
            if(heldBlade){heldBlade=false;bladeReturn=.16f;wristHistory=false;}

            Vector3 aim=(player.AimPoint-player.weapon.muzzle.position).normalized;
            float aimPitch=Mathf.Asin(Mathf.Clamp(aim.y,-1,1))*Mathf.Rad2Deg;
            if(shots!=player.weapon.ShotsFired){shots=player.weapon.ShotsFired;recoil=.09f;}
            recoil=Mathf.Max(0,recoil-dt);
            hurtWeight=Mathf.Lerp(hurtWeight,state==MotionState.Hurt?1:0,blend);

            b[1].rotation=Quaternion.AngleAxis(-Mathf.Clamp(aimPitch*.38f,-20,25)-recoil*65-hurtWeight*10,player.visual.right)*b[1].rotation;
            // Keep the imported leg animation; share its footfall rhythm through the
            // thorax before solving the rifle. A staggered shoulder line gives the
            // short support arm room to reach the actual rear handguard.
            float gaitPhase=animator.GetCurrentAnimatorStateInfo(0).normalizedTime*Mathf.PI*2;
            float gaitWeight=Mathf.Clamp01(speed/4)*groundWeight*(1-dashWeight);
            gaitSway=Mathf.Lerp(gaitSway,Mathf.Sin(gaitPhase)*gaitWeight,blend);
            gaitBounce=Mathf.Lerp(gaitBounce,Mathf.Cos(gaitPhase*2)*gaitWeight,blend);
            b[1].rotation=Quaternion.AngleAxis(18+gaitSway*2.2f,player.visual.up)*
                Quaternion.AngleAxis(gaitSway*1.2f,player.visual.forward)*
                Quaternion.AngleAxis(gaitBounce*1.5f+landing*14,player.visual.right)*b[1].rotation;
            b[2].rotation=Quaternion.AngleAxis(-18-gaitSway*1.1f,player.visual.up)*b[2].rotation;
            ApplyRifle(dt);
            if(bladeReturn>0)
            {
                bladeReturn=Mathf.Max(0,bladeReturn-dt);float weight=Mathf.SmoothStep(0,1,bladeReturn/.16f);
                for(int side=0;side<2;side++)for(int i=0;i<3;i++)
                {int arm=side==0?3:6;b[arm+i].localRotation=Quaternion.Slerp(b[arm+i].localRotation,bladeArmPose[side*3+i],weight);}
                CurlFingers(weight);
            }
        }
        static bool IsGait(string clip)=>clip=="Walk"||clip=="Run"||clip=="Sprint";

        void CurlFingers(float weight)
        {
            for(int i=0;i<fingers.Length;i++)
            {
                if(!fingers[i])continue;
                float angle=i%15>=12?18:i%3==0?48:i%3==1?72:45;
                fingers[i].localRotation=fingerRest[i]*Quaternion.AngleAxis((i<15?1:-1)*angle*weight,fingerCurlAxis[i]);
            }
        }

        void ApplyBlade(float dt)
        {
            var b=model.joints;var heat=player.Overheat;var basis=player.visual;
            Vector3 guardAxis=new Vector3(-.64f,.68f,.36f).normalized;
            Vector3 centre=new Vector3(.10f,1.26f,.29f),axis=guardAxis;
            float chest=24,age=heat.StrikeAge;
            if(heat.Striking)
            {
                // Wind-up -> cut -> recovery. The midpoint of the actual cutting
                // window is .06 s, exactly the existing gameplay impact time.
                Vector3 start=heat.Style==0?new Vector3(-.98f,-.06f,.18f):heat.Style==1?new Vector3(-.70f,.69f,.18f):new Vector3(0,.98f,.18f);
                Vector3 end=heat.Style==0?new Vector3(.98f,-.06f,.18f):heat.Style==1?new Vector3(.70f,-.69f,.18f):new Vector3(0,-.98f,.18f);
                Vector3 loaded=new Vector3(.24f,1.30f,.22f),finished=heat.Style==2?new Vector3(.30f,1.26f,.25f):new Vector3(-.03f,1.27f,.29f);
                if(age<.025f)
                {
                    float t=Mathf.SmoothStep(0,1,age/.025f);axis=Vector3.Slerp(guardAxis,start.normalized,t);
                    centre=Vector3.Lerp(centre,loaded,t);chest=Mathf.Lerp(24,36,t);
                }
                else if(age<.095f)
                {
                    float t=Mathf.SmoothStep(0,1,(age-.025f)/.070f);
                    axis=Vector3.Slerp(start.normalized,end.normalized,t);
                    // The rear cutting head clears the torso on the right at contact.
                    Vector3 contact=new Vector3(.29f,1.30f,.25f);
                    centre=t<.5f?Vector3.Lerp(loaded,contact,t*2):Vector3.Lerp(contact,finished,(t-.5f)*2);
                    chest=Mathf.Lerp(36,68,t);
                }
                else
                {
                    float t=Mathf.SmoothStep(0,1,(age-.095f)/(PlayerOverheat.StrikeDuration-.095f));
                    axis=Vector3.Slerp(end.normalized,guardAxis,t);centre=Vector3.Lerp(finished,centre,t);chest=Mathf.Lerp(68,24,t);
                }
            }
            b[1].rotation=Quaternion.AngleAxis(chest,basis.up)*b[1].rotation;
            b[2].rotation=Quaternion.AngleAxis(-chest*.65f,basis.up)*b[2].rotation;
            for(int side=0;side<2;side++)
            {
                if(shoulders[side])shoulders[side].localRotation=shoulderRest[side];
                for(int i=0;i<3;i++)b[(side==0?3:6)+i].localRotation=armRest[side*3+i];
            }
            // Keep the blade's narrow edge in the cut plane. In the overhead cut,
            // use a sideways up axis so LookRotation never reaches a pole singularity.
            Vector3 bladeUp=Vector3.up;
            if(heat.Striking)
            {
                Vector3 cutNormal=heat.Style==2?Vector3.right:heat.Style==1?new Vector3(.7f,.7f,0).normalized:Vector3.up;
                float rollWeight=Mathf.Min(Mathf.SmoothStep(0,1,age/.025f),1-Mathf.SmoothStep(0,1,(age-.095f)/.145f));
                bladeUp=Vector3.Slerp(Vector3.up,cutNormal,rollWeight);
            }
            Quaternion rotation=basis.rotation*Quaternion.LookRotation(axis,bladeUp);
            Vector3 worldCentre=basis.TransformPoint(centre);
            worldCentre+=b[0].position-model.transform.TransformPoint(hipRest);
            Quaternion leftWrist=rotation*Quaternion.LookRotation(Vector3.left,Vector3.forward)*Quaternion.Inverse(handFrame[0]);
            Quaternion rightWrist=rotation*Quaternion.LookRotation(Vector3.right,Vector3.forward)*Quaternion.Inverse(handFrame[1]);
            Vector3 leftOffset=rotation*Vector3.forward*.11f-leftWrist*palmOffset[0];
            Vector3 rightOffset=rotation*Vector3.back*.11f-rightWrist*palmOffset[1];
            // Project the shared rigid handle into both anatomical reach spheres.
            // This preserves grip separation rather than stretching a short arm.
            for(int pass=0;pass<5;pass++)for(int side=0;side<2;side++)
            {
                int arm=side==0?3:6;Vector3 offset=side==0?leftOffset:rightOffset;
                float reach=(Vector3.Distance(b[arm].position,b[arm+1].position)+Vector3.Distance(b[arm+1].position,b[arm+2].position))*.97f;
                Vector3 delta=worldCentre+offset-b[arm].position;
                if(delta.magnitude>reach)worldCentre=b[arm].position+delta.normalized*reach-offset;
            }
            RobotLocomotion.Solve(b[3],b[4],b[5],worldCentre+leftOffset,basis.rotation*new Vector3(-.65f,-1,-.15f));
            RobotLocomotion.Solve(b[6],b[7],b[8],worldCentre+rightOffset,basis.rotation*new Vector3(.65f,-1,-.15f));
            b[5].rotation=leftWrist;b[8].rotation=rightWrist;
            CurlFingers(1);
            heat.SetRiggedBlade(worldCentre,rotation,b[5].TransformPoint(palmOffset[0]),b[8].TransformPoint(palmOffset[1]));
            for(int side=0;side<2;side++)for(int i=0;i<3;i++)bladeArmPose[side*3+i]=b[(side==0?3:6)+i].localRotation;
            heldBlade=true;bladeReturn=0;wristHistory=false;
        }

        void ApplyFeet(bool grounded,float dt)
        {
            var b=model.joints;
            for(int side=0;side<2;side++)
            {
                int leg=side==0?9:12;Vector3 sampled=sampledFeet[side];
                float stride=sampled.z-footRest[side].z;
                Vector3 adapted=sampled;
                adapted.x=footRest[side].x+(sampled.x-footRest[side].x)*.5f+strideDirection.x*stride;
                adapted.z=footRest[side].z+strideDirection.z*stride;
                // Keep sideways footfalls on their own side of the body, avoiding
                // crossed knees when the forward running clip is redirected by 90 deg.
                if(side==0)adapted.x=Mathf.Min(adapted.x,-.045f);else adapted.x=Mathf.Max(adapted.x,.045f);
                Vector3 poseFoot=Vector3.Lerp(sampled,adapted,groundWeight);
                if(!grounded)
                {
                    // Deliberately asymmetric gathered legs. Keep animated ankle
                    // orientation in flight; sole flattening belongs only to contact.
                    Vector3 gathered=footRest[side]+new Vector3(side==0?-.025f:.025f,side==0?.34f:.23f,side==0?.12f:-.20f);
                    poseFoot=Vector3.Lerp(poseFoot,gathered,airTuck*.90f);
                }
                if(landingPoseWeight>.001f)
                {
                    // Catch the body with both legs instead of inheriting a raised
                    // swing foot from the outgoing air/run clip. This allows the
                    // existing pelvis drop to read as a load, not another footstep.
                    Vector3 catchFoot=footRest[side]+new Vector3(side==0?-.045f:.045f,0,side==0?.065f:-.065f);
                    poseFoot=Vector3.Lerp(poseFoot,catchFoot,landingPoseWeight);
                }
                if(dashWeight>.001f)
                {
                    float stagger=side==0?.27f:-.24f;
                    Vector3 braceFoot=footRest[side]+dashDirection*stagger;
                    braceFoot.x+=side==0?-.055f:.055f;
                    braceFoot.y+=grounded?.025f:(side==0?.25f:.12f);
                    poseFoot=Vector3.Lerp(poseFoot,braceFoot,dashWeight*.85f);
                }
                Vector3 target=model.transform.TransformPoint(poseFoot);
                float rise=footHistory?(sampled.y-previousFootHeight[side])/dt:0;
                previousFootHeight[side]=sampled.y;
                bool support=false;
                Vector3 desiredContactOffset=Vector3.zero;
                Quaternion soleRotation=contactWeight[side]>0?contactRotation[side]:sampledFootRotation[side];
                if(grounded&&!player.Dash.IsActive&&Physics.Raycast(target+Vector3.up*.3f,Vector3.down,out var hit,.85f,1<<9,QueryTriggerInteraction.Ignore)&&hit.normal.y>.7f)
                {
                    float sole=footRest[side].y*model.transform.lossyScale.y;
                    float clearance=target.y-(hit.point.y+sole);
                    // Only the low, settling foot enters stance. The rising/swing foot
                    // keeps both its mocap lift and ankle pitch, even on flat terrain.
                    bool catching=landingPoseWeight>.35f;
                    bool lifting=!catching&&rise>.65f&&sampled.y>footRest[side].y+.035f;
                    support=!lifting&&clearance<.20f&&clearance>-.15f&&(catching||sampled.y<=sampledFeet[1-side].y+.045f);
                    if(lifting||clearance>.23f)canPlant[side]=true;
                    if(support&&canPlant[side])
                    {
                        plantedFoot[side]=new Vector3(target.x,hit.point.y+sole,target.z);
                        plantTime[side]=.14f;canPlant[side]=false;
                    }
                    float reach=(Vector3.Distance(b[leg].position,b[leg+1].position)+Vector3.Distance(b[leg+1].position,b[leg+2].position))*.98f;
                    Vector3 localPlant=model.transform.InverseTransformPoint(plantedFoot[side]);
                    bool crossed=side==0?localPlant.x>-.025f:localPlant.x<.025f;
                    if(!support||crossed||Vector3.Distance(b[leg].position,plantedFoot[side])>reach||Vector3.Distance(target,plantedFoot[side])>.32f)plantTime[side]=0;
                    if(support)
                    {
                        Vector3 contact=new Vector3(target.x,hit.point.y+sole,target.z);
                        if(plantTime[side]>0)contact=plantedFoot[side];
                        desiredContactOffset=contact-target;
                    }
                    Vector3 facing=Quaternion.AngleAxis(moveYaw,Vector3.up)*player.visual.forward;
                    Quaternion flat=Quaternion.LookRotation(Vector3.ProjectOnPlane(facing,hit.normal),hit.normal)*footBindRotation[side];
                    if(contactWeight[side]<.01f)contactRotation[side]=sampledFootRotation[side];
                    contactRotation[side]=Quaternion.Slerp(contactRotation[side],flat,1-Mathf.Exp(-28*dt));
                    soleRotation=contactRotation[side];
                }
                else {canPlant[side]=true;plantTime[side]=0;}
                plantTime[side]=Mathf.Max(0,plantTime[side]-dt);
                contactWeight[side]=Mathf.MoveTowards(contactWeight[side],support?1:0,dt*(support?20:28));
                footContactOffset[side]=Vector3.Lerp(footContactOffset[side],desiredContactOffset,1-Mathf.Exp(-35*dt));
                target+=footContactOffset[side];
                // A lateral direction change can invalidate a world plant. Clamp only
                // the tiny release tail rather than dragging the foot across the body.
                Vector3 localTarget=model.transform.InverseTransformPoint(target);
                if(side==0)localTarget.x=Mathf.Min(localTarget.x,-.025f);else localTarget.x=Mathf.Max(localTarget.x,.025f);
                target=model.transform.TransformPoint(localTarget);
                Vector3 kneePole=Quaternion.AngleAxis(moveYaw,Vector3.up)*player.visual.forward;
                RobotLocomotion.Solve(b[leg],b[leg+1],b[leg+2],target,kneePole);
                b[leg+2].rotation=Quaternion.Slerp(sampledFootRotation[side],soleRotation,contactWeight[side]);
            }
            footHistory=true;
        }
        void ApplyRifle(float dt)
        {
            var b=model.joints;var weapon=player.weapon.visuals;
            // Replace unarmed arm swing, retaining a small phase-linked clavicle
            // response. IK owns the arms only after this upper-body layering pass.
            for(int side=0;side<2;side++)
            {
                if(shoulders[side])
                {
                    shoulders[side].localRotation=shoulderRest[side];
                    shoulders[side].rotation=Quaternion.AngleAxis(gaitBounce*1.6f-recoil*24,player.visual.right)*
                        Quaternion.AngleAxis((side==0?-1:1)*gaitSway*1.3f,player.visual.forward)*shoulders[side].rotation;
                }
                int arm=side==0?3:6;
                for(int i=0;i<3;i++)b[arm+i].localRotation=armRest[side*3+i];
            }
            if(weapon.aimPivot)
            {
                Vector3 shoulder=b[6].position+player.visual.rotation*new Vector3(-.065f,-.035f,.025f);
                weapon.SeatAtShoulder(shoulder,player.AimPoint);
            }
            Vector3 right=player.weapon.muzzle.right,up=player.weapon.muzzle.up,forward=player.weapon.muzzle.forward;
            RobotLocomotion.Solve(b[6],b[7],b[8],weapon.RightGrip,right*.32f-Vector3.up);
            RobotLocomotion.Solve(b[3],b[4],b[5],weapon.LeftGrip,-right*.24f-Vector3.up);
            // Frames come from this avatar's finger/thumb axes, not a generic wrist axis.
            // Keep the grasp frame independent of the unarmed clip/IK elbow roll.
            // Limiting from the freshly reset arm pose left a different wrist roll
            // every frame, despite an accurate positional grip.
            var left=Quaternion.LookRotation(right,forward)*Quaternion.Inverse(handFrame[0]);
            var rightHand=Quaternion.LookRotation(-right,up+forward*.4f)*Quaternion.Inverse(handFrame[1]);
            float follow=1-Mathf.Exp(-30*dt);
            heldWrist[0]=wristHistory?Quaternion.Slerp(heldWrist[0],left,follow):left;
            heldWrist[1]=wristHistory?Quaternion.Slerp(heldWrist[1],rightHand,follow):rightHand;
            b[5].rotation=heldWrist[0];b[8].rotation=heldWrist[1];wristHistory=true;
        }
    }
}
