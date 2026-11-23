using UnityEngine;

namespace NeonArena
{
    public sealed class EnemyShield : MonoBehaviour
    {
        public Collider surface;
        public Renderer plate;
        public Transform gauge;
        public Transform field;
        public void ConfigureToModel(CharacterModel model)
        {
            if(!model||!model.skin)return;
            Bounds bounds=model.skin.sharedMesh.bounds;
            float front=float.MinValue,low=float.MaxValue,high=float.MinValue,width=0;
            for(int i=0;i<8;i++)
            {
                Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var p=transform.parent.InverseTransformPoint(model.skin.transform.TransformPoint(corner));
                front=Mathf.Max(front,p.z);low=Mathf.Min(low,p.y);high=Mathf.Max(high,p.y);width=Mathf.Max(width,Mathf.Abs(p.x)*2);
            }
            float height=Mathf.Max(1.6f,high-low+.16f);width=Mathf.Clamp(width+.18f,1.45f,2.5f);
            transform.localPosition=new Vector3(0,(low+high)*.5f,front+.23f);
            var box=surface as BoxCollider;if(box){box.center=Vector3.zero;box.size=new Vector3(width,height,.14f);}
            if(field)field.localScale=new Vector3(width,height,2);
            if(gauge)gauge.localPosition=new Vector3(0,height*.42f,.09f);
        }
        public bool IsShieldType { get; private set; }
        public float Maximum { get; private set; }
        public float Current { get; private set; }
        public bool IsIntact => gameObject.activeInHierarchy && Current > 0;
        MaterialPropertyBlock block;
        EnemyController owner;
        void Awake() { block = new MaterialPropertyBlock(); owner = GetComponentInParent<EnemyController>(); }
        public void Initialize(bool enabled, int wave)
        {
            IsShieldType=enabled;
            Maximum = 90 * (1 + 0.06f * Mathf.Max(0, wave - 1)); Current = Maximum;
            surface.enabled = enabled; gameObject.SetActive(enabled); Refresh();
        }
        public bool Hit(float damage, bool piercing, bool ricochet, Vector3? impact=null)
        {
            if (!IsIntact) return true;
            // A combined shot tries to break the shield before reflecting from an intact plate.
            float amount = piercing ? damage * 3 : ricochet ? 0 : damage * 0.2f;
            if(ArenaGame.Instance&&ArenaGame.Instance.Frontier)amount=piercing?damage*.8f:ricochet?0:damage*.4f;
            Current = Mathf.Max(0, Current - amount);
            ArenaAudio.Instance?.Play(Current == 0 ? SoundCue.ShieldBreak : piercing ? SoundCue.ShieldPierce : ricochet ? SoundCue.ShieldReflect : SoundCue.ShieldHit, transform.position);
            if (owner.IsVisibleToPlayer)
                ImpactPool.Instance?.Emit(transform.position, piercing ? Color.cyan : new Color(0.25f, 0.55f, 1), Current == 0 ? 30 : 6);
            ArenaGame.Instance?.RegisterDamageFeedback();
            Refresh();
            Vector3 point=impact??transform.position;Pulse(point);
            if(owner.IsVisibleToPlayer)FrontierCombatReadability.Instance?.ShieldImpact(owner,point,Current==0,ricochet);
            if(Current==0)owner.ShieldStagger();
            if (Current == 0) { surface.enabled = false; gameObject.SetActive(false); }
            return Current == 0;
        }
        void Refresh()
        {
            if (gauge) gauge.localScale = new Vector3(Mathf.Max(0.01f, Current / Maximum), 1, 1);
            if (!plate || block == null) return;
            Color color = Color.Lerp(new Color(1, .3f, .12f,.17f), new Color(.40f,.32f,.76f,.12f), Current / Maximum);
            plate.GetPropertyBlock(block); block.SetColor("_Color", color); block.SetColor("_EmissionColor", new Color(.69f,.63f,1)*.9f); plate.SetPropertyBlock(block);
        }
        public void BreakWithLaser(Vector3? impact=null) { if(IsIntact) Hit(Maximum*5,false,false,impact); }
        public void Pulse(Vector3 point){if(!plate)return;if(block==null)block=new MaterialPropertyBlock();plate.GetPropertyBlock(block);block.SetFloat("_HitTime",Time.time);block.SetVector("_HitPos",point);plate.SetPropertyBlock(block);}
        public Vector3 Outside(Vector3 normal, float radius)
            => surface.ClosestPoint(transform.position + normal * 5) + normal * (radius + 0.025f);
    }
}
