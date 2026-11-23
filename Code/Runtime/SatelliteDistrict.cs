using UnityEngine;
using System.Collections.Generic;

namespace NeonArena
{
    // Authored combat block: each hall has two exits and a ground-to-roof route.
    public sealed class SatelliteDistrict : MonoBehaviour
    {
        public GameObject[] phases=new GameObject[4];
        public void Apply(int wave){for(int i=0;i<phases.Length;i++)if(phases[i])phases[i].SetActive(wave>=i+1);}
    }
}
