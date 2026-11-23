using UnityEngine;

namespace NeonArena
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCamera : MonoBehaviour
    {
        public Transform target;
        [Range(7, 26)] public float distance = 19;
        [Range(-65, 75)] public float pitch = 45;
        public float yaw = 30;
        public float mouseSensitivity = 3;
        public bool TacticalView { get; private set; }
        public bool followCharacter=true;
        public bool UsesFollowControls=>followCharacter&&!TacticalView;
        public void FollowHeading(float heading,float dt)
        {
            if(!UsesFollowControls||dt<=0)return;
            float delta=Mathf.DeltaAngle(yaw,heading);
            yaw=Mathf.Repeat(yaw+Mathf.Clamp(delta*(1-Mathf.Exp(-9*dt)),-240*dt,240*dt),360);
        }
        public bool IsOrbiting => !TacticalView && ArenaGame.Instance && ArenaGame.Instance.IsPlaying && Input.GetMouseButton(1);
        Camera view;
        Vector3 focus;
        bool initialized;
        int focusFrame=-1;
        public bool shakeEnabled = true;
        float shake;
        public void Kick(float amount) { if (shakeEnabled) shake = Mathf.Min(0.16f, Mathf.Max(shake, amount)); }

        void Awake() { view = GetComponent<Camera>(); }
        float boom;
        float slopeFraming;
        readonly RaycastHit[] footingHits=new RaycastHit[24];
        readonly System.Collections.Generic.Dictionary<Collider,CameraOccluder[]> footingVisuals=new System.Collections.Generic.Dictionary<Collider,CameraOccluder[]>();
        void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F2)) { shakeEnabled = !shakeEnabled; shake = 0; }
            bool playing = ArenaGame.Instance && ArenaGame.Instance.IsPlaying;
            if (playing)
            {
                if (Input.GetKeyDown(KeyCode.Tab)) SetTactical(!TacticalView);
                if (!TacticalView)
                {
                    if (Input.GetMouseButton(1)) Orbit(Input.GetAxisRaw("Mouse X") * mouseSensitivity, Input.GetAxisRaw("Mouse Y") * mouseSensitivity);
                    float keyboardYaw = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
                    if(IsOrbiting||!followCharacter)Orbit(keyboardYaw * 95 * Time.deltaTime, 0);
                    else if(target)
                    {
                        var player=target.GetComponent<PlayerController>();
                        if(player&&player.enabled&&player.LookFrame!=Time.frameCount&&!ArenaGame.Instance.Cinema.ShotActive)FollowHeading(player.visual.eulerAngles.y,Time.deltaTime);
                    }
                    Zoom(Input.mouseScrollDelta.y);
                }
            }
            Cursor.lockState = playing && (UsesFollowControls||IsOrbiting) ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
            RenderView(false);
            ArenaGame.Instance?.Cinema.Render(view);
            if(playing && target && !TacticalView)
            {
                var heat=target.GetComponent<PlayerOverheat>();
                float entry=heat?Time.unscaledTime-heat.ActivatedAt:10;
                if(entry>=0 && entry<.6f)view.fieldOfView+=Mathf.Sin(entry/.6f*Mathf.PI)*3;
            }
            if (playing && shakeEnabled && shake > 0)
            {
                transform.position += (transform.right * Mathf.Sin(Time.time * 97) + transform.up * Mathf.Cos(Time.time * 83)) * shake;
                shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 0.8f);
            }
        }

        public void Orbit(float horizontal, float vertical)
        {
            yaw = Mathf.Repeat(yaw + horizontal, 360);
            pitch = Mathf.Clamp(pitch - vertical, -65, 75);
        }
        public void Zoom(float amount) => distance = Mathf.Clamp(distance - amount * 1.6f, 7, 26);
        public void SetTactical(bool tactical)
        {
            TacticalView = tactical;
            Cursor.lockState = CursorLockMode.None;
            RenderView(true);
        }

        public void RenderView(bool snap)
        {
            if (!view) view = GetComponent<Camera>();
            if (TacticalView)
            {
                view.orthographic = true;
                view.orthographicSize = Mathf.Max(ArenaTerrain.WorldHalfDepth+5, (ArenaTerrain.WorldHalfWidth+5) / Mathf.Max(0.5f, view.aspect));
                transform.position = new Vector3(0, ArenaTerrain.WorldHalfDepth*2.1f, -ArenaTerrain.WorldHalfDepth*1.2f);
                transform.LookAt(Vector3.zero);
                initialized = false;
                return;
            }
            view.orthographic = false;
            view.fieldOfView = 55;
            Vector3 desiredFocus = target ? target.position + Vector3.up * 0.8f : Vector3.up * 0.8f;
            // Frame ahead of the player so a centred reticle aims into the arena, not at their feet.
            bool frontier=ArenaGame.Instance&&ArenaGame.Instance.Frontier;
            bool onSlope=false;
            if(frontier&&target)
            {
                int hitCount=Physics.RaycastNonAlloc(target.position+Vector3.up*.45f,Vector3.down,footingHits,1.45f,(1<<9)|(1<<11),QueryTriggerInteraction.Ignore);
                for(int i=0;i<hitCount;i++)
                {
                    var ground=footingHits[i];if(ground.normal.y<.55f)continue;
                    // Refined ramps keep an invisible collision proxy and a separate deck model.
                    // Protect the rendered model too, without showing the primitive proxy again.
                    if(!footingVisuals.TryGetValue(ground.collider,out var surfaces))
                    {
                        var linked=new System.Collections.Generic.List<CameraOccluder>(ground.collider.GetComponentsInChildren<CameraOccluder>());
                        var deck=GameObject.Find(ground.collider.name+" / engineered deck");
                        if(deck)linked.AddRange(deck.GetComponentsInChildren<CameraOccluder>());
                        surfaces=linked.ToArray();
                        footingVisuals.Add(ground.collider,surfaces);
                    }
                    foreach(var surface in surfaces)if(surface)surface.ProtectFooting();
                    if(ground.distance<.8f&&ground.normal.y<.995f)onSlope=true;
                }
            }
            bool advance=focusFrame!=Time.frameCount;focusFrame=Time.frameCount;
            slopeFraming=snap?(onSlope?1:0):advance?Mathf.MoveTowards(slopeFraming,onSlope?1:0,Time.unscaledDeltaTime*3):slopeFraming;
            if(UsesFollowControls&&!frontier)desiredFocus+=Quaternion.Euler(0,yaw,0)*Vector3.forward*Mathf.Min(4.5f,distance*.25f);
            if(frontier&&target)desiredFocus=target.position+Vector3.up*Mathf.Lerp(1.5f,1.25f,slopeFraming)+Quaternion.Euler(0,yaw,0)*new Vector3(.55f,0,Mathf.Lerp(1.35f,.65f,slopeFraming));
            float framedDistance = distance;
            focus = snap || !initialized ? desiredFocus : advance?Vector3.Lerp(focus, desiredFocus, 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime)):focus;
            initialized = true;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 backwards = rotation * Vector3.back;
            Vector3 renderFocus = focus;
            // If focus drifts inside a bridge or wall, use the player's torso as the anchor.
            if (!frontier&&Physics.CheckSphere(renderFocus,.28f,1<<9,QueryTriggerInteraction.Ignore))
                renderFocus = target ? target.position + Vector3.up * .95f : renderFocus;
            float Clearance(Quaternion candidate)
            {
                Vector3 back = candidate * Vector3.back;
                float nearest=framedDistance;
                foreach(var obstacle in Physics.SphereCastAll(renderFocus,.25f,back,framedDistance,(1<<9)|(1<<11),QueryTriggerInteraction.Collide))
                {
                    var cutaway=obstacle.collider.GetComponent<CameraOccluder>();if(cutaway){cutaway.Reveal();continue;}
                    var roof=obstacle.collider.GetComponent<CutawayRoof>();if(roof && roof.IsCutaway)continue;
                    nearest=Mathf.Min(nearest,Mathf.Max(.3f,obstacle.distance-.15f));
                }
                return nearest;
            }
            // A negative pitch uses the shoulder view below; do not cast an unused boom under
            // the ground and accidentally request a cutaway of the playing surface.
            float safeDistance = pitch<=0?framedDistance:Clearance(rotation);
            if (!frontier&&safeDistance < 3.5f)
            {
                // Search actual clear sightlines; never lift the anchor through a solid floor.
                for(float elevation=Mathf.Max(pitch+10,60);elevation<=90;elevation+=5)
                {
                    Quaternion candidate=Quaternion.Euler(elevation,yaw,0);float clear=Clearance(candidate);
                    if(clear>safeDistance){safeDistance=clear;rotation=candidate;}
                    if(clear>=Mathf.Min(8,framedDistance))break;
                }

            }
            // Under a solid deck, a shallow pitch keeps the same manual yaw and clears the ceiling.
            if(!frontier&&safeDistance<3.5f)foreach(float lowPitch in new[]{15f,10f,5f})
            {
                var candidate=Quaternion.Euler(lowPitch,yaw,0);float clear=Clearance(candidate);
                if(clear>safeDistance){safeDistance=clear;rotation=candidate;}
                if(clear>=6)break;
            }
            backwards=rotation*Vector3.back;
            boom=snap||boom<=0||safeDistance<boom?safeDistance:advance?Mathf.Lerp(boom,safeDistance,1-Mathf.Exp(-5*Time.unscaledDeltaTime)):boom;
            Vector3 position=renderFocus + backwards * boom;
            if(pitch<20 && target)
            {
                // Blend into an elevated shoulder camera before looking above the horizon.
                // Looking up rotates the view, rather than swinging the camera under the floor.
                float blend=Mathf.Clamp01((20-pitch)/20);
                Quaternion heading=Quaternion.Euler(0,yaw,0);
                float upward=Mathf.InverseLerp(0,-65,pitch);
                Vector3 anchor=target.position+Vector3.up*Mathf.Lerp(1.15f,.85f,upward);
                Vector3 shoulder=anchor+heading*(Vector3.back*Mathf.Lerp(Mathf.Min(5,distance*.45f),1.25f,upward)+Vector3.right*.55f);
                Vector3 offset=shoulder-anchor;
                float clear=offset.magnitude;
                foreach(var hit in Physics.SphereCastAll(anchor,.25f,offset.normalized,clear,(1<<9)|(1<<11),QueryTriggerInteraction.Collide))
                {
                    var cut=hit.collider.GetComponent<CameraOccluder>();if(cut){cut.Reveal();continue;}
                    var roof=hit.collider.GetComponent<CutawayRoof>();if(roof&&roof.IsCutaway)continue;
                    clear=Mathf.Min(clear,Mathf.Max(.3f,hit.distance-.15f));
                }
                shoulder=anchor+offset.normalized*clear;
                position=Vector3.Lerp(position,shoulder,blend);
                rotation=Quaternion.Slerp(rotation,Quaternion.Euler(pitch,yaw,0),blend);
            }
            transform.SetPositionAndRotation(position, rotation);
            // Imported facades may be one-sided; check from the camera as well as from the focus.
            // The forward framing offset must never leave the character behind an opaque wall.
            if(target)
            {
                Vector3 toPlayer=target.position+Vector3.up*.85f-transform.position;
                bool backfaces=Physics.queriesHitBackfaces;
                try
                {
                    Physics.queriesHitBackfaces=true;
                    foreach(var hit in Physics.SphereCastAll(transform.position,.45f,toPlayer.normalized,toPlayer.magnitude,(1<<9)|(1<<11),QueryTriggerInteraction.Collide))
                        hit.collider.GetComponent<CameraOccluder>()?.Reveal();
                }
                finally{Physics.queriesHitBackfaces=backfaces;}
                CameraOccluder.RevealImportedAlong(transform.position,target.position+Vector3.up*.85f);
            }
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
