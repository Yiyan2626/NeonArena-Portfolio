using System.Collections;
using System.Linq;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class DetailAimTests
{
    ArenaGame game;
    Vector2 Centre=>new Vector2(Screen.width*.5f,Screen.height*.5f);
    [UnitySetUp]public IEnumerator Setup(){Time.timeScale=1;yield return SceneManager.LoadSceneAsync("SatelliteCity");yield return null;game=ArenaGame.Instance;game.StartSession();game.autoSpawn=false;game.enabled=false;game.player.enabled=false;}
    [Test]public void UpwardCameraRemainsAboveGroundAndResolvesSkyEveryTime()
    {
        var p=game.player;var cam=p.viewCamera.GetComponent<ArenaCamera>();cam.yaw=0;
        p.Teleport(new Vector3(0,1.9f,-10));cam.Orbit(0,1000);cam.RenderView(true);
        Assert.That(cam.pitch,Is.EqualTo(-65));Assert.That(cam.transform.forward.y,Is.GreaterThan(.85f));Assert.That(cam.transform.position.y,Is.GreaterThan(p.transform.position.y+.8f));
        p.AimAndFire(false,Centre);var first=p.AimPoint;
        Assert.That(first.y,Is.GreaterThan(p.transform.position.y+40));
        cam.Orbit(60,0);cam.RenderView(true);p.AimAndFire(false,Centre);
        Assert.That(Vector3.Distance(first,p.AimPoint),Is.GreaterThan(20),"Sky aim must not retain the previous point");
    }
    [Test]public void ElevatedReticleHitIsPreservedAndWeaponActuallyAimsAtIt()
    {
        var p=game.player;var cam=p.viewCamera.GetComponent<ArenaCamera>();p.Teleport(new Vector3(0,1.9f,-10));cam.yaw=0;cam.pitch=-25;cam.RenderView(true);
        var ray=p.viewCamera.ScreenPointToRay(Centre);var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.layer=8;target.transform.position=ray.GetPoint(20);target.transform.localScale=Vector3.one*2;
        try{
            Physics.SyncTransforms();Assert.That(target.GetComponent<Collider>().Raycast(ray,out var hit,100),Is.True);
            p.AimAndFire(false,Centre);Assert.That(Vector3.Distance(p.AimPoint,hit.point),Is.LessThan(.02f));
            Assert.That(p.weapon.muzzle.forward.y,Is.GreaterThan(.3f));
            Assert.That(p.weapon.SimulateFire(.2f,true,p.AimPoint),Is.True);
            Assert.That(Vector3.Dot(p.weapon.LastShotDirection,(hit.point-p.weapon.LastShotOrigin).normalized),Is.GreaterThan(.9999f));
        }finally{Object.DestroyImmediate(target);}
    }
    [UnityTest]public IEnumerator CameraFadeCannotMakeAnAimedWallNonSolid()
    {
        var p=game.player;var cam=p.viewCamera.GetComponent<ArenaCamera>();p.Teleport(new Vector3(0,1.9f,-10));cam.yaw=0;cam.pitch=-5;cam.RenderView(true);
        var ray=p.viewCamera.ScreenPointToRay(Centre);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;wall.transform.position=ray.GetPoint(14);wall.transform.localScale=new Vector3(3,3,.2f);
        var cut=wall.AddComponent<CameraOccluder>();cut.Reveal();
        try{yield return new WaitForSeconds(.25f);cam.RenderView(true);Physics.SyncTransforms();ray=p.viewCamera.ScreenPointToRay(Centre);
            Assert.That(cut.Fade,Is.LessThan(.5f));Assert.That(wall.GetComponent<Collider>().Raycast(ray,out var hit,100),Is.True);
            p.AimAndFire(false,Centre);Assert.That(Vector3.Distance(p.AimPoint,hit.point),Is.LessThan(.05f));Assert.That(wall.GetComponent<Collider>().enabled,Is.True);
        }finally{Object.DestroyImmediate(wall);}
    }
    [Test]public void ShieldIsInFrontOfEntireHeavyModelAndResetsOnReuse()
    {
        var e=game.SpawnEnemy();e.enabled=false;e.Spawn(new Vector3(0,1.9f,-2),game.player,1,false,true);
        var skin=e.Motion.Model.skin;var bound=skin.sharedMesh.bounds;float front=float.MinValue;
        for(int i=0;i<8;i++){var c=bound.center+Vector3.Scale(bound.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));front=Mathf.Max(front,e.visual.InverseTransformPoint(skin.transform.TransformPoint(c)).z);}
        var box=(BoxCollider)e.Shield.surface;
        Assert.That(e.Shield.transform.localPosition.z-box.size.z*.5f,Is.GreaterThan(front+.10f));
        Assert.That(e.Shield.plate.sharedMaterial.shader.name,Is.EqualTo("NeonArena/EnergyField"));
        float z=e.Shield.transform.localPosition.z;e.Shield.Hit(100,true,false);e.Spawn(e.transform.position,game.player,3,false,true);
        Assert.That(e.Shield.IsIntact,Is.True);Assert.That(e.Shield.transform.localPosition.z,Is.EqualTo(z).Within(.001f));
    }
    [UnityTest]public IEnumerator CapturedFloorBoundsDoNotTriggerACameraCutaway()
    {
        var p=game.player;var cam=p.viewCamera.GetComponent<ArenaCamera>();p.Teleport(new Vector3(0,1.9f,-10));cam.yaw=0;cam.pitch=35;cam.distance=10;
        var g=new GameObject("Captured multi-height floor");g.layer=9;g.transform.position=new Vector3(0,1.8f,-10);
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-10,0,-10),new Vector3(-10,0,10),new Vector3(10,0,10),new Vector3(10,0,-10),new Vector3(8,1,8),new Vector3(8,1,9),new Vector3(9,1,8)};mesh.triangles=new[]{0,1,2,0,2,3,4,5,6};mesh.RecalculateBounds();mesh.RecalculateNormals();
        g.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=new Material(Shader.Find("NeonArena/CitySurface"));g.AddComponent<MeshCollider>().sharedMesh=mesh;var cut=g.AddComponent<CameraOccluder>();
        try{
            Physics.SyncTransforms();cam.RenderView(true);yield return new WaitForSeconds(.2f);Assert.That(cut.Fade,Is.EqualTo(1));Assert.That(renderer.forceRenderingOff,Is.False);
            cam.pitch=-65;cam.RenderView(true);yield return new WaitForSeconds(.2f);Assert.That(cut.Fade,Is.EqualTo(1));Assert.That(renderer.forceRenderingOff,Is.False);
        }finally{Object.DestroyImmediate(renderer.sharedMaterial);Object.DestroyImmediate(g);Object.DestroyImmediate(mesh);}
    }
    [Test]public void AllAuthoredDesksPartitionsAndFloorsShareTheDetailPass()
    {
        var originals=Object.FindObjectsOfType<MeshRenderer>().Where(r=>new[]{"Reception island","Office workstation","Office framed glazing","Office partition base","Upper terminal"}.Contains(r.name)).ToArray();
        Assert.That(originals.Length,Is.GreaterThan(15));foreach(var r in originals)Assert.That(r.enabled,Is.False,r.name);
        var root=GameObject.Find("Interior detail pass 0.14");Assert.That(root.transform.childCount,Is.EqualTo(originals.Length));
        foreach(var r in Object.FindObjectsOfType<MeshRenderer>().Where(r=>r.name.Contains(" / ground floor")||r.name.Contains(" / upper gallery")))
        {Assert.That(r.sharedMaterial.name,Is.EqualTo("Composite floor"));Assert.That(r.sharedMaterial.GetTexture("_BumpMap"),Is.Not.Null);}
        var receiver=game.player.weapon.visuals.receiver.GetComponentInChildren<MeshFilter>();Assert.That(receiver.sharedMesh.vertexCount,Is.GreaterThan(1000));
    }
}
