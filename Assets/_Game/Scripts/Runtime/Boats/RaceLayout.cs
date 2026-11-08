using System.Collections.Generic;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.Boats
{
    // Per-race obstacle layout; authored banks, barriers, falls and cave structures stay intact.
    public sealed class RaceLayout : MonoBehaviour
    {
        readonly List<GameObject> hidden=new List<GameObject>();
        readonly List<MeshFilter> templates=new List<MeshFilter>();
        readonly List<Vector3> positions=new List<Vector3>();
        readonly Collider[] overlaps=new Collider[32];
        readonly List<BoatCourse> routes=new List<BoatCourse>();
        Material wood;
        public int Seed {get;private set;}
        public int ObstacleCount=>positions.Count;
        public int Signature {get;private set;}
        public int ReplacedCount=>hidden.Count;
        public void Initialize(RiverTrack track,int seed)
        {
            Seed=seed;var random=new System.Random(seed);var meshes=new HashSet<Mesh>();
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                bool riverRock=filter.transform.parent!=null&&filter.transform.parent.name=="RiverRocks";
                bool branchRock=filter.name.StartsWith("BranchRock_");
                if(!(riverRock||branchRock)||filter.sharedMesh==null||filter.GetComponent<Collider>()==null)continue;
                if(meshes.Add(filter.sharedMesh))templates.Add(filter);
                hidden.Add(filter.gameObject);
            }
            templates.Sort((a,b)=>string.CompareOrdinal(a.sharedMesh.name,b.sharedMesh.name));
            foreach(var go in hidden)go.SetActive(false);
            for(int i=0;i<4;i++)routes.Add(new BoatCourse(track,i));
            wood=new Material(Shader.Find("Universal Render Pipeline/Lit"));wood.color=new Color(.3f,.17f,.075f);
            for(int route=0;route<routes.Count;route++)
            {
                var course=routes[route];
                for(float d=90+(float)random.NextDouble()*18;d<course.Length-50&&positions.Count<75;d+=24+(float)random.NextDouble()*20)
                {
                    var center=course.At(d,out var tangent);int hint=-1;var water=track.SampleWater(center,ref hint);
                    float mainDistance=RaceProgress.CourseDistance(track,water);
                    bool excluded=mainDistance<track.StartDistance+65||mainDistance>track.FinishDistance-35;
                    foreach(var b in track.Branches)if(Mathf.Abs(mainDistance-b.fromDistance)<45||Mathf.Abs(mainDistance-b.toDistance)<25)excluded=true;
                    foreach(var fall in track.falls)if(fall!=null&&Mathf.Abs(mainDistance-fall.distance)<30+fall.length)excluded=true;
                    if(excluded)continue;
                    float half=water.channel<0?(track.width+track.appliedRaceStripWidth)*.5f:track.Branches[water.channel].HalfWidth;
                    float minimum=half<10?4.2f:6;
                    float maximum=half-1.7f;if(maximum<minimum)continue;
                    float offset=Mathf.Lerp(minimum,maximum,(float)random.NextDouble())*(random.Next(2)==0?-1:1);
                    var p=center+Vector3.Cross(Vector3.up,tangent)*offset;hint=-1;water=track.SampleWater(p,ref hint);
                    if(!water.inWater||water.edgeDistance>-.9f)continue;
                    bool blocked=false;
                    foreach(var old in positions)if(Vector3.Distance(old,p)<12){blocked=true;break;}
                    // Leave a continuous passage around every navigable route, including shared junctions.
                    foreach(var path in routes)
                    {
                        int segment=0;float lateral;float nearest=path.Nearest(p,ref segment,out lateral,true);
                        var point=path.At(nearest,out _);var delta=p-point;delta.y=0;
                        if(delta.sqrMagnitude<4.2f*4.2f){blocked=true;break;}
                    }
                    p.y=water.height+.45f;
                    int count=Physics.OverlapSphereNonAlloc(p,1.25f,overlaps,~0,QueryTriggerInteraction.Ignore);
                    for(int i=0;i<count;i++)if(overlaps[i].GetComponentInParent<BoatController>()==null){blocked=true;break;}
                    if(blocked)continue;
                    bool log=random.NextDouble()<.3||templates.Count==0;
                    GameObject go;
                    if(log)
                    {
                        go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.GetComponent<Renderer>().sharedMaterial=wood;
                        go.transform.localScale=new Vector3(.55f,1.1f+(float)random.NextDouble()*.5f,.55f);
                        go.transform.rotation=Quaternion.AngleAxis((float)(random.NextDouble()*60-30),Vector3.up)*Quaternion.FromToRotation(Vector3.up,tangent);
                        p.y=water.height+.1f;
                    }
                    else
                    {
                        var source=templates[random.Next(templates.Count)];go=new GameObject("RaceRock",typeof(MeshFilter),typeof(MeshRenderer));
                        go.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;go.GetComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
                        float width=Mathf.Max(source.sharedMesh.bounds.size.x,source.sharedMesh.bounds.size.z);
                        float size=(.95f+(float)random.NextDouble()*.6f)/Mathf.Max(.01f,width);
                        go.transform.localScale=new Vector3(size,size*(.9f+(float)random.NextDouble()*.6f),size);
                        go.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                        go.AddComponent<MeshCollider>().sharedMesh=source.sharedMesh;p.y=water.height-.18f;
                    }
                    go.name=(log?"RandomDriftwood_":"RandomRock_")+positions.Count;
                    go.transform.SetParent(transform,true);go.transform.position=p;positions.Add(p);
                    unchecked{Signature=Signature*31+Mathf.RoundToInt(p.x*100);Signature=Signature*31+Mathf.RoundToInt(p.z*100);Signature=Signature*31+(log?1:0);}
                }
            }
        }
        public void Restore()
        {
            foreach(var go in hidden)if(go!=null)go.SetActive(true);hidden.Clear();
        }
        void OnDestroy(){Restore();if(wood!=null)Destroy(wood);}
    }
}
