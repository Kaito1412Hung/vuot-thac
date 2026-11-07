using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VuotThac.Track;
namespace VuotThac.EditorTools.Track
{
    /// <summary>Interior-only art direction for the three river caves. Never rebuilds landscape or vegetation.</summary>
    public static class CaveInteriorThemes
    {
        const string Folder="Assets/_Game/Generated/CaveInteriorThemes";
        public class Frame {public Vector3 p,right,forward;public float half,height;}
        static Mesh pillar,pendant,dome;
        static Material stone,ivory,accent,water,wet;
        static int theme;
        static Transform root;
        static string prefix;
        static Mesh Store(Mesh mesh,string name)
        {
            string path=Folder+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);saved.SetVertices(mesh.vertices);saved.SetNormals(mesh.normals);saved.SetUVs(0,mesh.uv);for(int i=0;i<mesh.subMeshCount;i++)saved.SetTriangles(mesh.GetTriangles(i),i);saved.RecalculateBounds();saved.UploadMeshData(false);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
        }
        static Material Mat(string name,Color tint,float smooth=.24f)
        {
            string path=Folder+"/"+prefix+"_"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",tint);mat.SetFloat("_Smoothness",smooth);mat.SetFloat("_Cull",2);mat.enableInstancing=true;
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Generated/StoneGrain.asset"));mat.SetTextureScale("_BaseMap",new Vector2(.7f,.7f));
            mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",tint*.045f);mat.SetFloat("_SpecularHighlights",0);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(mat);return mat;
        }
        public static void SmoothVault(Mesh mesh)
        {
            var vertices=mesh.vertices;var sums=new Dictionary<Vector3Int,Vector3>();
            Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.RoundToInt(p.x*1000),Mathf.RoundToInt(p.y*1000),Mathf.RoundToInt(p.z*1000));
            var tri=mesh.GetTriangles(0);
            for(int i=0;i<tri.Length;i+=3){var n=Vector3.Cross(vertices[tri[i+1]]-vertices[tri[i]],vertices[tri[i+2]]-vertices[tri[i]]);for(int j=0;j<3;j++){var key=Key(vertices[tri[i+j]]);sums.TryGetValue(key,out var total);sums[key]=total+n;}}
            var normals=mesh.normals;if(normals.Length!=vertices.Length)normals=new Vector3[vertices.Length];
            for(int i=0;i<vertices.Length;i++)if(sums.TryGetValue(Key(vertices[i]),out var n)&&n.sqrMagnitude>1e-9f)normals[i]=n.normalized;
            mesh.SetNormals(normals);mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);
        }
        static Transform ObjectMesh(string name,Mesh mesh,Material mat,Vector3 p,Vector3 scale,Quaternion rotation)
        {
            var g=new GameObject(name);g.transform.SetParent(root,false);g.transform.SetPositionAndRotation(p,rotation);g.transform.localScale=scale;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;return g.transform;
        }
        static void WorldMesh(string name,Mesh mesh,Material mat)=>ObjectMesh(name,Store(mesh,prefix+"_"+name),mat,Vector3.zero,Vector3.one,Quaternion.identity);
        static Mesh Sculpt(string name,int type)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();const int rows=48,sides=48;
            for(int j=0;j<=rows;j++)for(int i=0;i<=sides;i++)
            {
                float t=j/(float)rows,a=i*Mathf.PI*2/sides;
                float radius=type==0?.34f+.48f*Mathf.Pow(1-t,4)+.24f*Mathf.Pow(t,7):type==1?Mathf.Pow(1-t,1.55f):Mathf.Pow(1-t,.42f);
                float rib=1+.09f*Mathf.Sin(a*9+.5f*Mathf.Sin(t*6))+.045f*Mathf.Cos(a*17+t*3)+.025f*Mathf.Sin(a*29-t*7);
                radius*=rib*(1+.05f*Mathf.Sin(t*39+a*2));float bend=.07f*Mathf.Sin(t*3);
                v.Add(new Vector3(Mathf.Cos(a)*radius+bend,type==1?-t:t,Mathf.Sin(a)*radius));uv.Add(new Vector2(i/(float)sides*3,t*5));
                if(j<rows&&i<sides){int n=j*(sides+1)+i;tri.AddRange(type==1?new[]{n,n+1,n+sides+1,n+1,n+sides+2,n+sides+1}:new[]{n,n+sides+1,n+1,n+1,n+sides+1,n+sides+2});}
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return Store(mesh,name);
        }
        public static List<Frame> Frames(RiverTrack track,Transform cave)
        {
            if(cave.parent.name=="Tunnels")
            {
                var tunnel=track.tunnels[int.Parse(cave.name.Split('_')[1])];var main=new List<Frame>();int mainRows=Mathf.CeilToInt(tunnel.length/2);
                for(int i=0;i<=mainRows;i++){float u=i/(float)mainRows,d=tunnel.distance-tunnel.length*.5f+tunnel.length*u;var s=track.EvaluateAtDistance(d);var a=track.AfterRaceInsertion(s.center-s.right*track.HalfWidth);var b=track.AfterRaceInsertion(s.center+s.right*track.HalfWidth);float swell=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.75f);main.Add(new Frame{p=(a+b)*.5f,right=(b-a).normalized,forward=s.tangent,half=Vector3.Distance(a,b)*.5f+track.waterSurfaceExtraWidth+.8f+8*swell,height=Mathf.Lerp(13,34,swell)});}return main;
            }
            int index=int.Parse(cave.name.Split('_')[1]);var branch=track.Branches[index];var shell=cave.Find("CaveShell");bool active=shell.gameObject.activeSelf;shell.gameObject.SetActive(true);Physics.SyncTransforms();
            var collider=shell.GetComponent<MeshCollider>();var found=new List<float>();
            for(float d=4;d<branch.Length-4;d+=1){var p=branch.EvaluateAtDistance(d).center+Vector3.right*branch.side*track.appliedRaceStripWidth*.5f;if(collider.Raycast(new Ray(p+Vector3.up,Vector3.up),out var hit,35))found.Add(d);}
            shell.gameObject.SetActive(active);var result=new List<Frame>();if(found.Count<3)return result;float from=found[0],to=found[found.Count-1];int rows=Mathf.CeilToInt((to-from)/2);
            for(int i=0;i<=rows;i++){float u=i/(float)rows,swell=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.75f);var s=branch.EvaluateAtDistance(Mathf.Lerp(from,to,u));result.Add(new Frame{p=s.center+Vector3.right*branch.side*track.appliedRaceStripWidth*.5f,right=s.right,forward=s.tangent,half=branch.HalfWidth+track.waterSurfaceExtraWidth+.8f+9*swell,height=Mathf.Lerp(9,28,swell)});}return result;
        }
        static void Lamp(string name,Vector3 p,Vector3 target,Color color,float intensity,float range,bool spot=false)
        {
            var g=new GameObject(name);g.transform.SetParent(root,false);g.transform.position=p;g.transform.LookAt(target);var l=g.AddComponent<Light>();l.type=spot?LightType.Spot:LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=spot?LightShadows.Soft:LightShadows.None;l.shadowStrength=.65f;l.shadowBias=.055f;l.spotAngle=78;l.innerSpotAngle=48;
        }
        static void Cascade(Frame f,int side,int id,float width,float height)
        {
            const int cols=96,rows=48;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            float lateral=f.half*.86f;
            for(int j=0;j<=rows;j++)for(int i=0;i<=cols;i++)
            {
                float u=i/(float)cols,t=j/(float)rows;
                float folds=.55f*Mathf.Sin(u*37+.4f*Mathf.Sin(t*6))+.21f*Mathf.Sin(u*83+t*2);
                float hem=1.0f+.35f*Mathf.Sin(u*17)+.16f*Mathf.Sin(u*41);
                float bulge=Mathf.Sin(t*Mathf.PI)*1.0f;
                var p=f.p+f.forward*((u-.5f)*width)+f.right*side*(lateral+folds-bulge)+Vector3.up*(1+hem*.22f+t*(height-hem*.22f));
                v.Add(p);uv.Add(new Vector2(u*width*.22f,t*height*.17f));
                if(j<rows&&i<cols){int n=j*(cols+1)+i;foreach(var a in new[]{n,n+cols+1,n+1,n+1,n+cols+1,n+cols+2})tri.Add(a);}
            }
            var mesh=new Mesh{name=prefix+"_Flowstone_"+id};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var material=new Material(accent);string path=Folder+"/"+prefix+"_Drapery.mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(path);if(saved==null){AssetDatabase.CreateAsset(material,path);saved=material;}else{EditorUtility.CopySerialized(material,saved);Object.DestroyImmediate(material);}saved.SetFloat("_Cull",0);EditorUtility.SetDirty(saved);
            WorldMesh("SculptedStoneCascade_"+id,mesh,saved);
            Lamp("ConcealedFlowstoneWash",f.p+f.right*side*(lateral-5)+Vector3.up*2.5f,f.p+f.right*side*lateral+Vector3.up*(height*.5f),theme==1?new Color(.70f,.88f,.86f):new Color(1,.80f,.55f),26,28,true);
        }
        static void Pool(Vector3 center,Vector3 right,Vector3 forward,float rx,float rz,int id,int tiers)
        {
            var basin=new FlatMeshBuilder();var rim=new FlatMeshBuilder();
            Vector3 Edge(int i,float expansion,float h){float a=i*Mathf.PI*2/128;float n=1+.055f*Mathf.Sin(a*5+id)+.025f*Mathf.Sin(a*11-id);return center+right*Mathf.Cos(a)*(rx+expansion)*n+forward*Mathf.Sin(a)*(rz+expansion)*n+Vector3.up*h;}
            for(int i=0;i<128;i++)
            {
                basin.TriUp(0,center,Edge(i,0,0),Edge(i+1,0,0));
                for(int t=0;t<tiers;t++)
                {float e=t*.22f,h=.18f-t*.025f;var a=Edge(i,e,h);var b=Edge(i+1,e,h);var c=Edge(i,e+.14f,h-.075f);var d=Edge(i+1,e+.14f,h-.075f);rim.TriUp(0,a,b,c);rim.TriUp(0,b,d,c);rim.Tri(0,a,Edge(i,e,h-.17f),b);rim.Tri(0,b,Edge(i,e,h-.17f),Edge(i+1,e,h-.17f));}
            }
            WorldMesh("MirrorRimstonePool_"+id,basin.ToMesh(prefix+"_Pool_"+id),water);WorldMesh("CarvedCalcitePoolEdges_"+id,rim.ToMesh(prefix+"_Rim_"+id),ivory);
            Lamp("PoolIndirectGlow",center+Vector3.up*4,center,theme==1?new Color(.42f,.82f,.78f):new Color(.72f,.82f,.79f),7,12);
            var splash=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/WaterSpray.mat");
            if(splash!=null)
            {
                var g=new GameObject("SlowCeilingDrips");g.transform.SetParent(root,false);g.transform.position=center+Vector3.up*5;
                var ps=g.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=2.5f;main.startSpeed=.02f;main.startSize=.025f;main.gravityModifier=.22f;main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;var emission=ps.emission;emission.rateOverTime=2;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.6f;ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=splash;
            }
        }
        static void Chandelier(Frame f,int id,float span,int count)
        {
            for(int i=0;i<count;i++)
            {
                float u=(i+.12f+.74f*Mathf.PerlinNoise(i*.79f+id,2.6f))/count;float x=(u-.5f)*span;float y=-1.8f+Mathf.Sqrt(Mathf.Max(.1f,1-x*x/(f.half*f.half)))*(f.height+1.8f);
                float length=(theme==1?1.5f:2.6f)+(theme==1?1.8f:3.0f)*(.5f+.5f*Mathf.Sin(i*2.17f+id));
                var p=f.p+f.right*x+f.forward*(Mathf.Sin(i*1.83f+id)*3.0f)+Vector3.up*(y+.5f);
                ObjectMesh("CeilingCalciteCluster",pendant,ivory,p,new Vector3(length*(theme==1?.075f:.15f),length,length*(theme==1?.065f:.13f)),Quaternion.Euler(0,i*67,0));
            }
        }
        static void UpperVaultLining(List<Frame> frames)
        {
            const int columns=64;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            bool GalleryOpening(Vector3 p)
            {
                foreach(int side in new[]{-1,1}){var f=frames[Mathf.Clamp(Mathf.RoundToInt((frames.Count-1)*(side<0?.4f:.67f)),2,frames.Count-3)];var center=f.p+f.right*side*f.half*.72f+Vector3.up*.85f;var d=p-center;float along=Vector3.Dot(d,f.right*side),x=Vector3.Dot(d,f.forward),y=d.y+1.8f;if(along>0&&along<9&&x*x/(5.0f*5.0f)+y*y/(10.4f*10.4f)<1)return true;}return false;
            }
            for(int row=0;row<frames.Count;row++)for(int c=0;c<=columns;c++)
            {
                var f=frames[row];float angle=c*Mathf.PI/columns;
                v.Add(f.p+f.right*Mathf.Cos(angle)*(f.half+.85f)+Vector3.up*(-1.8f+Mathf.Sin(angle)*(f.height+2.65f)));uv.Add(new Vector2(c*.18f,row*.4f));
                if(row<frames.Count-1&&c<columns)
                {var next=frames[row+1];float midAngle=(c+.5f)*Mathf.PI/columns;var mid=(f.p+next.p)*.5f+(f.right+next.right).normalized*Mathf.Cos(midAngle)*((f.half+next.half)*.5f+.85f)+Vector3.up*(-1.8f+Mathf.Sin(midAngle)*((f.height+next.height)*.5f+2.65f));if(!GalleryOpening(mid)){int i=row*(columns+1)+c;tri.AddRange(new[]{i,i+columns+1,i+1,i+1,i+columns+1,i+columns+2});}}
            }
            var mesh=new Mesh{name=prefix+"_ContinuousHighVault"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var mat=Mat("HighVaultLining",stone.GetColor("_BaseColor"));mat.shader=Shader.Find("VuotThac/CaveAwareExterior");mat.SetFloat("_CaveInteriorMode",1);mat.SetFloat("_CaveOwner",theme+1);mat.SetFloat("_Cull",2);EditorUtility.SetDirty(mat);WorldMesh("ContinuousHighVaultLining",mesh,mat);
            foreach(int side in new[]{-1,1})
            {
                var f=frames[Mathf.Clamp(Mathf.RoundToInt((frames.Count-1)*(side<0?.4f:.67f)),2,frames.Count-3)];var points=new List<Vector3>();var coords=new List<Vector2>();var indices=new List<int>();const int rows=24,arc=48;
                for(int row=0;row<=rows;row++)
                {
                    float u=row/(float)rows;var dir=(f.right*side+f.forward*(.22f*Mathf.Sin(u*2))).normalized;var right=Vector3.Cross(Vector3.up,dir).normalized;
                    var p=f.p+f.right*side*(f.half*.72f+u*24)+f.forward*(u*u*7)+Vector3.up*.85f;float half=Mathf.Lerp(5.6f,7.4f,Mathf.Sin(u*Mathf.PI*.75f))+.7f,height=Mathf.Lerp(9.5f,13.5f,Mathf.Sin(u*Mathf.PI*.8f))+.7f;
                    for(int c=0;c<=arc;c++){float angle=c*Mathf.PI/arc;points.Add(p+right*Mathf.Cos(angle)*half+Vector3.up*(-1.8f+Mathf.Sin(angle)*(height+1.8f)));coords.Add(new Vector2(c*.18f,row*.3f));if(row<rows&&c<arc){int i=row*(arc+1)+c;indices.AddRange(new[]{i,i+arc+1,i+1,i+1,i+arc+1,i+arc+2});}}
                    if(row==rows){int center=points.Count;points.Add(p);coords.Add(Vector2.zero);for(int c=0;c<arc;c++)indices.AddRange(new[]{center,row*(arc+1)+c+1,row*(arc+1)+c});}
                }
                var gallery=new Mesh{name=prefix+"_GalleryLining_"+side};gallery.SetVertices(points);gallery.SetUVs(0,coords);gallery.SetTriangles(indices,0);gallery.RecalculateNormals();gallery.RecalculateBounds();WorldMesh("SealedSideGalleryLining_"+side,gallery,mat);
            }
        }
        public static string Apply(RiverTrack track)
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","CaveInteriorThemes");
            pillar=Sculpt("NaturalJoinedCalciteColumn",0);pendant=Sculpt("SlenderCeilingPendant",1);dome=Sculpt("IrregularCalciteDome",2);
            string[] names={"AmberCurtainSanctuary","JadeMirrorGrotto","IvoryCathedral","PearlFlowerChamber","CopperCanyonHall"};int done=0;
            var centers=new List<Vector4>();var axes=new List<Vector4>();var bounds=new List<Bounds>();
            foreach(Transform cave in track.transform.Find("Generated/BranchCaves").Cast<Transform>().Concat(track.transform.Find("Generated/Tunnels").Cast<Transform>()))
            {
                theme=done;prefix=names[theme];var chamber=cave.Find("MajesticChambers");if(chamber==null)continue;var frames=Frames(track,cave);if(frames.Count<4)continue;
                var old=chamber.Find("DistinctInteriorTheme");if(old!=null)Object.DestroyImmediate(old.gameObject);root=new GameObject("DistinctInteriorTheme").transform;root.SetParent(chamber,false);
                foreach(var renderer in chamber.GetComponentsInChildren<MeshRenderer>())if(renderer.name=="GrandLimestoneVault")bounds.Add(renderer.bounds);
                for(int i=0;i<frames.Count;i+=3){var f=frames[i];centers.Add(new Vector4(f.p.x,f.p.y,f.p.z,f.half));axes.Add(new Vector4(f.right.x,f.right.z,f.height,4.1f+(done+1)*100));}
                foreach(int side in new[]{-1,1})
                {
                    var f=frames[Mathf.Clamp(Mathf.RoundToInt((frames.Count-1)*(side<0?.4f:.67f)),2,frames.Count-3)];
                    for(int i=0;i<=4;i++){float u=i/4f;var dir=(f.right*side+f.forward*(.22f*Mathf.Sin(u*2))).normalized;var right=Vector3.Cross(Vector3.up,dir).normalized;
                        var p=f.p+f.right*side*(f.half*.72f+u*24)+f.forward*(u*u*7)+Vector3.up*.85f;centers.Add(new Vector4(p.x,p.y,p.z,Mathf.Lerp(5.6f,7.4f,Mathf.Sin(u*Mathf.PI*.75f))));axes.Add(new Vector4(right.x,right.z,Mathf.Lerp(9.5f,13.5f,Mathf.Sin(u*Mathf.PI*.8f)),4.7f+(done+1)*100));}
                }
                stone=Mat("Vault",theme==0?new Color(.36f,.27f,.17f):theme==1?new Color(.25f,.32f,.31f):new Color(.38f,.37f,.33f));
                ivory=Mat("Calcite",theme==0?new Color(.77f,.62f,.37f):theme==1?new Color(.75f,.80f,.74f):new Color(.82f,.78f,.64f),.31f);
                accent=Mat("Mineral",theme==0?new Color(.68f,.43f,.18f):theme==1?new Color(.40f,.56f,.51f):new Color(.63f,.57f,.43f));wet=Mat("DampRock",theme==0?new Color(.37f,.27f,.17f):theme==1?new Color(.18f,.29f,.28f):new Color(.36f,.35f,.29f),.46f);
                water=Mat("MirrorWater",theme==1?new Color(.055f,.31f,.29f):new Color(.09f,.23f,.24f),.95f);water.SetTexture("_BaseMap",null);water.SetFloat("_SpecularHighlights",1);water.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                var floorMat=Mat("Floor",theme==0?new Color(.44f,.35f,.23f):theme==1?new Color(.34f,.40f,.37f):new Color(.46f,.43f,.35f));
                if(theme==3){stone.SetColor("_BaseColor",new Color(.32f,.30f,.32f));ivory.SetColor("_BaseColor",new Color(.85f,.81f,.75f));accent.SetColor("_BaseColor",new Color(.67f,.53f,.45f));}
                if(theme==4){stone.SetColor("_BaseColor",new Color(.34f,.23f,.16f));ivory.SetColor("_BaseColor",new Color(.63f,.46f,.29f));accent.SetColor("_BaseColor",new Color(.55f,.29f,.15f));floorMat.SetColor("_BaseColor",new Color(.36f,.27f,.19f));}
                floorMat.SetFloat("_Cull",0);
                int pendantIndex=0,stalagmiteIndex=0;
                foreach(var tr in chamber.GetComponentsInChildren<Transform>(true))
                {
                    if(tr==root||tr.IsChildOf(root))continue;
                    if(tr.name=="FlowstoneButtress")tr.gameObject.SetActive(false);
                    if(tr.name=="GreatCeilingPendant")tr.gameObject.SetActive(theme==2?pendantIndex++%4==0:theme==0&&pendantIndex++%3==0);
                    if(tr.name=="AncientFlutedStalagmite")tr.gameObject.SetActive(theme==0||theme==4||theme==1&&stalagmiteIndex++%2==0);
                    if(tr.name=="MonumentalFoldedFlowstone")tr.gameObject.SetActive(theme==0);
                    if(tr.name=="RoyalOrganFlowstone")tr.gameObject.SetActive(theme!=1);
                    if(tr.name=="TerracedCalcitePoolRims")tr.gameObject.SetActive(false);
                    var light=tr.GetComponent<Light>();if(light!=null)light.enabled=false;
                    var renderer=tr.GetComponent<MeshRenderer>();if(renderer==null)continue;
                    if(tr.name=="GrandLimestoneVault")SmoothVault(tr.GetComponent<MeshFilter>().sharedMesh);
                    Material material=tr.name=="GrandLimestoneVault"?stone:tr.name.Contains("Floor")||tr.name=="RiversideStoneWalkways"?floorMat:tr.name.Contains("Pool")&&!tr.name.Contains("Rim")?water:tr.name.Contains("Organ")||tr.name.Contains("Folded")?accent:ivory;
                    if(tr.name=="ExpandedUndergroundRiver")continue;
                    var mats=renderer.sharedMaterials;for(int i=0;i<mats.Length;i++)mats[i]=material;renderer.sharedMaterials=mats;
                }
                var hero=frames[frames.Count/2];
                UpperVaultLining(frames);
                for(int i=3;i<frames.Count-3;i+=theme==1?7:10)
                {
                    var f=frames[i];Color color=theme==0?new Color(1,.78f,.48f):theme==1?new Color(.64f,.84f,.84f):new Color(.91f,.86f,.73f);
                    Lamp("VaultSoftBounce",f.p+Vector3.up*(f.height*.53f),f.p,color,theme==1?18:22,36);
                }
                if(theme==0)
                {
                    Cascade(frames[frames.Count*2/5],-1,0,18,16);Cascade(frames[frames.Count*3/5],1,1,14,14);
                    Chandelier(hero,0,hero.half*1.22f,27);
                    for(int i=0;i<7;i++){var f=frames[Mathf.Clamp(frames.Count/2+i-3,2,frames.Count-3)];float side=i%2==0?-1:1;var p=f.p+f.right*side*(f.half*.82f)+Vector3.up;float height=f.height*.48f;ObjectMesh("GoldenCascadingDome",dome,i%2==0?ivory:accent,p,new Vector3(2.0f+i*.13f,height,2.1f),Quaternion.Euler(0,i*59,0));}
                }
                else if(theme==1)
                {
                    for(int i=frames.Count/4;i<frames.Count*3/4;i+=5){var f=frames[i];int side=i%2==0?-1:1;float inner=Mathf.Max(7.5f,f.half-4.8f);Pool(f.p+f.right*side*(inner+2.2f)+Vector3.up*1.14f,f.right,f.forward,1.5f,2.6f,100+i,5);Chandelier(f,i,f.half*.9f,17);}
                    Cascade(hero,-1,2,13,13);
                }
                else if(theme==2)
                {
                    for(int i=Mathf.Max(3,frames.Count/5);i<frames.Count*4/5;i+=8)
                    {
                        var f=frames[i];foreach(int side in new[]{-1,1})
                        {
                            float x=f.half*.77f;float ceiling=-1.8f+Mathf.Sqrt(1-.77f*.77f)*(f.height+1.8f);var p=f.p+f.right*x*side+Vector3.up;
                            ObjectMesh("TitanJoinedLimestoneColumn",pillar,ivory,p,new Vector3(2.1f,ceiling-.6f,1.8f),Quaternion.Euler(0,i*23,0));
                            Lamp("CathedralColumnUplight",p-f.right*side*4+f.forward*3+Vector3.up*2,p+Vector3.up*(ceiling*.55f),new Color(1,.88f,.65f),24,28,true);
                        }
                        Chandelier(f,i,f.half*1.1f,23);
                    }
                    Cascade(hero,1,3,19,15);
                }
                else if(theme==3)
                {
                    for(int k=frames.Count/4;k<frames.Count*3/4;k+=4)
                    {
                        var f=frames[k];Chandelier(f,k,f.half*1.25f,31);
                        for(int j=0;j<7;j++){float angle=j*Mathf.PI*2/7;var p=f.p+f.right*(Mathf.Cos(angle)*4)+f.forward*Mathf.Sin(angle)*4+Vector3.up*(f.height+.5f);var direction=(f.right*Mathf.Cos(angle)*.42f+f.forward*Mathf.Sin(angle)*.42f+Vector3.down).normalized;ObjectMesh("AragoniteStoneFlower",pendant,ivory,p,new Vector3(.32f,3.4f+(j%3),.26f),Quaternion.FromToRotation(Vector3.down,direction));}
                    }
                    Cascade(hero,-1,4,21,18);Lamp("PearlChamberSoftFill",hero.p+Vector3.up*12,hero.p,new Color(1,.83f,.77f),30,45);
                }
                else
                {
                    Cascade(frames[frames.Count/3],-1,5,19,18);Cascade(frames[frames.Count*2/3],1,6,17,15);
                    var bridgeFrame=frames[frames.Count/5];var bridge=new FlatMeshBuilder();const int segments=64;float half=bridgeFrame.half-3.4f;
                    Vector3 Bridge(int i,float z,bool top){float u=i/(float)segments,x=Mathf.Lerp(-half,half,u);float y=(top?2f:-1.6f)+Mathf.Sin(u*Mathf.PI)*(top?13f:14.4f)+.18f*Mathf.Sin(u*37);return bridgeFrame.p+bridgeFrame.right*x+bridgeFrame.forward*z+Vector3.up*y;}
                    for(int i=0;i<segments;i++){var a=Bridge(i,-1.7f,true);var b=Bridge(i+1,-1.7f,true);var c=Bridge(i,1.7f,true);var d=Bridge(i+1,1.7f,true);bridge.TriUp(0,a,c,b);bridge.TriUp(0,b,c,d);var e=Bridge(i,-1.7f,false);var f=Bridge(i+1,-1.7f,false);var g=Bridge(i,1.7f,false);var h=Bridge(i+1,1.7f,false);bridge.Tri(0,e,f,g);bridge.Tri(0,f,h,g);bridge.Tri(0,a,b,e);bridge.Tri(0,b,f,e);bridge.Tri(0,c,g,d);bridge.Tri(0,d,g,h);}
                    accent.SetFloat("_Cull",0);WorldMesh("NaturalCopperRockBridge",bridge.ToMesh(prefix+"_NaturalArch"),accent);var bridgeObject=root.Find("NaturalCopperRockBridge");bridgeObject.gameObject.AddComponent<MeshCollider>().sharedMesh=bridgeObject.GetComponent<MeshFilter>().sharedMesh;Lamp("CanyonArchWarmWash",hero.p-hero.forward*8+Vector3.up*4,hero.p+Vector3.up*12,new Color(1,.68f,.40f),28,34,true);
                }
                int poolID=0;foreach(var renderer in chamber.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="HiddenRimstonePool"))
                {
                    var p=renderer.bounds.center;renderer.enabled=false;
                    var f=frames.OrderBy(a=>(a.p-p).sqrMagnitude).First();Pool(p+Vector3.up*.025f,f.right,f.forward,theme==1?3.3f:2.5f,theme==1?4.2f:3.4f,poolID++,theme==1?7:3);
                    Lamp("GalleryWallWash",p+Vector3.up*5,p+f.forward*5,theme==0?new Color(1,.72f,.41f):theme==1?new Color(.47f,.79f,.76f):new Color(.89f,.85f,.72f),14,21);
                }
                done++;
            }
            var visibility=track.GetComponent<CaveInteriorVisibility>();if(visibility==null)visibility=track.gameObject.AddComponent<CaveInteriorVisibility>();visibility.centers=centers.Take(256).ToArray();visibility.axes=axes.Take(256).ToArray();visibility.Upload();EditorUtility.SetDirty(visibility);
            var shader=Shader.Find("VuotThac/CaveAwareExterior");var protectedMaterials=new Dictionary<Material,Material>();
            if(shader!=null)foreach(string group in new[]{"Terrain","Mountains","OriginalMountainRoofs","Trees","DeepForest","MountainForest"})
            foreach(var renderer in track.transform.Find("Generated/"+group).GetComponentsInChildren<MeshRenderer>())
            {
                if(!bounds.Any(b=>b.Intersects(renderer.bounds)))continue;var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {var source=materials[i];if(source==null||source.shader==shader)continue;if(!protectedMaterials.TryGetValue(source,out var masked))
                    {string path=Folder+"/ExteriorVisibility_"+source.name+".mat";masked=AssetDatabase.LoadAssetAtPath<Material>(path);if(masked==null){masked=new Material(source);AssetDatabase.CreateAsset(masked,path);}else EditorUtility.CopySerialized(source,masked);masked.shader=shader;EditorUtility.SetDirty(masked);protectedMaterials.Add(source,masked);}materials[i]=masked;}
                renderer.sharedMaterials=materials;EditorUtility.SetDirty(renderer);
            }
            AssetDatabase.SaveAssets();return $"Applied distinct interior themes to {done} caves. Exterior geometry, terrain and tree transforms unchanged.";
        }
    }
}







