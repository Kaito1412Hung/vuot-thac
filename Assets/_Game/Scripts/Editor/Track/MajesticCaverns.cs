using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VuotThac.Track;

namespace VuotThac.EditorTools.Track
{
    // Large limestone chambers and real, carved side galleries. All coordinates are world space.
    public static class MajesticCaverns
    {
        const string Folder="Assets/_Game/Generated/MajesticCaverns";
        class Frame { public Vector3 p,right;public float half,height,waterHalf; }
        class Hall { public string id;public Transform root;public bool forest,closed;public List<Frame> frames=new();public List<Hall> galleries=new();public Bounds bounds; }
        static readonly List<Hall> halls=new();
        static Material limestone,calcite,ochre,wet,sand;
        static int formations;
        static float Noise(Vector3 p)=>Mathf.PerlinNoise(p.x*.27f+p.z*.13f,p.y*.22f+p.z*.19f);
        static Mesh Store(Mesh mesh,string name)
        {
            string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,old);old.SetVertices(mesh.vertices);old.SetNormals(mesh.normals);old.SetUVs(0,mesh.uv);
            for(int i=0;i<mesh.subMeshCount;i++)old.SetTriangles(mesh.GetTriangles(i),i);old.RecalculateBounds();old.UploadMeshData(false);
            Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
        }
        static Material Mat(string name,Color color,float smooth)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Cull",name=="WarmKarstLimestone"||name=="LimestoneSandShelves"?2:0);m.enableInstancing=true;
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Generated/StoneGrain.asset"));EditorUtility.SetDirty(m);return m;
        }
        static Transform MeshObject(Transform parent,string name,Mesh mesh,Material material,bool collider=false)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            g.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            if(collider)g.AddComponent<MeshCollider>().sharedMesh=mesh;return g.transform;
        }
        static Vector3 Surface(Frame f,float angle,float thickness=0)
        {
            float wave=Mathf.Sin(angle*7+f.p.z*.09f)*.2f+Mathf.Sin(angle*17-f.p.x*.11f)*.11f;
            float x=Mathf.Cos(angle)*(f.half+thickness+wave);
            float y=-1.8f+Mathf.Sin(angle)*(f.height+1.8f+thickness+wave);
            return f.p+f.right*x+Vector3.up*y;
        }
        static void FinishBounds(Hall hall)
        {
            hall.bounds=new Bounds(hall.frames[0].p,Vector3.zero);
            foreach(var f in hall.frames){hall.bounds.Encapsulate(f.p+f.right*(f.half+4));hall.bounds.Encapsulate(f.p-f.right*(f.half+4));hall.bounds.Encapsulate(f.p+Vector3.up*(f.height+5));}
            hall.bounds.Expand(5);
        }
        static Hall Gallery(Hall hall,int side,int index)
        {
            int fi=Mathf.Clamp(Mathf.RoundToInt((hall.frames.Count-1)*(side<0?.4f:.67f)),2,hall.frames.Count-3);
            var f=hall.frames[fi];var along=Vector3.Cross(f.right,Vector3.up).normalized;
            var g=new Hall{id=hall.id+"_Gallery_"+index,root=hall.root,forest=true,closed=true};
            float length=hall.forest?21:24;
            for(int i=0;i<=14;i++)
            {
                float u=i/14f;var dir=(f.right*side+along*(.22f*Mathf.Sin(u*2))).normalized;
                var p=f.p+f.right*side*(f.half*.72f+u*length)+along*(u*u*7)+Vector3.up*.85f;
                g.frames.Add(new Frame{p=p,right=Vector3.Cross(Vector3.up,dir).normalized,half=Mathf.Lerp(5.6f,7.4f,Mathf.Sin(u*Mathf.PI*.75f)),height=Mathf.Lerp(9.5f,13.5f,Mathf.Sin(u*Mathf.PI*.8f))});
            }
            FinishBounds(g);hall.galleries.Add(g);return g;
        }
        static bool Opening(Hall hall,Vector3 p)
        {
            foreach(var g in hall.galleries)
            {
                var f=g.frames[0];var delta=p-f.p;float x=Vector3.Dot(delta,f.right);float y=delta.y+1.5f;
                var axis=Vector3.Cross(f.right,Vector3.up);
                if(x*x/(5.1f*5.1f)+y*y/(10.1f*10.1f)<1&&Vector3.Dot(delta,axis)>-1.5f)return true;
            }
            return false;
        }
        static void Shell(Hall h)
        {
            const int arc=56;var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();var outer=new List<int>();
            int rows=h.frames.Count;
            for(int layer=0;layer<2;layer++)for(int r=0;r<rows;r++)for(int c=0;c<=arc;c++)
            {float angle=c*Mathf.PI/arc;verts.Add(Surface(h.frames[r],angle,layer==0?0:3.2f));uv.Add(new Vector2(c*.24f,r*.6f));}
            int layerSize=rows*(arc+1);
            void Tri(List<int> list,int a,int b,int c,bool inward)
            {var mid=(verts[a]+verts[b]+verts[c])/3;var f=h.frames[Mathf.Clamp((a%layerSize)/(arc+1),0,rows-1)];var toward=f.p+Vector3.up*4-mid;if((Vector3.Dot(Vector3.Cross(verts[b]-verts[a],verts[c]-verts[a]),toward)>0)!=inward){int temp=b;b=c;c=temp;}list.Add(a);list.Add(b);list.Add(c);}
            void CutTri(List<int> list,int a,int b,int c,bool inward)
            {
                if(h.galleries.Count==0){Tri(list,a,b,c,inward);return;}
                var poly=new List<Vector3>{verts[a],verts[b],verts[c]};
                foreach(var g in h.galleries)
                {
                    float Field(Vector3 p){var f=g.frames[0];var delta=p-f.p;float x=Vector3.Dot(delta,f.right),y=delta.y+1.5f;return Mathf.Max(x*x/(5.1f*5.1f)+y*y/(10.1f*10.1f)-1,-Vector3.Dot(delta,Vector3.Cross(f.right,Vector3.up))-1.5f);}
                    var next=new List<Vector3>();for(int i=0;i<poly.Count;i++){var p=poly[i];var q=poly[(i+1)%poly.Count];float dp=Field(p),dq=Field(q);if(dp>=0)next.Add(p);if((dp>=0)!=(dq>=0)){float lo=0,hi=1;for(int step=0;step<18;step++){float u=(lo+hi)*.5f;if((Field(Vector3.Lerp(p,q,u))>=0)==(dp>=0))lo=u;else hi=u;}next.Add(Vector3.Lerp(p,q,(lo+hi)*.5f));}}poly=next;
                }
                for(int i=1;i<poly.Count-1;i++)
                {
                    int first=verts.Count;foreach(var p in new[]{poly[0],poly[i],poly[i+1]}){verts.Add(p);uv.Add(new Vector2(p.x+p.z*.2f,p.y)*.3f);}
                    var mid=(poly[0]+poly[i]+poly[i+1])/3;var f=h.frames[Mathf.Clamp((a%layerSize)/(arc+1),0,rows-1)];var toward=f.p+Vector3.up*4-mid;
                    if((Vector3.Dot(Vector3.Cross(verts[first+1]-verts[first],verts[first+2]-verts[first]),toward)>0)==inward)list.AddRange(new[]{first,first+1,first+2});else list.AddRange(new[]{first,first+2,first+1});
                }
            }
            for(int r=0;r<rows-1;r++)for(int c=0;c<arc;c++)
            {
                int a=r*(arc+1)+c,b=a+arc+1;var mid=(verts[a]+verts[b]+verts[b+1]+verts[a+1])*.25f;
                CutTri(tris,a,b,b+1,true);CutTri(tris,a,b+1,a+1,true);
                if(!(h.id.Contains("_Gallery_")&&r<4))
                {CutTri(outer,a+layerSize,b+layerSize,b+1+layerSize,false);CutTri(outer,a+layerSize,b+1+layerSize,a+1+layerSize,false);}
            }
            // Solid stone lips join the inside and outside; the far end of side chambers closes completely.
            foreach(int r in new[]{0,rows-1})for(int c=0;c<arc;c++)
            {if(r==0&&h.id.Contains("_Gallery_"))continue;int a=r*(arc+1)+c;outer.AddRange(new[]{a,a+layerSize,a+layerSize+1,a,a+layerSize+1,a+1});}
            if(h.closed)
            {
                int center=verts.Count;verts.Add(h.frames[rows-1].p);uv.Add(Vector2.zero);
                for(int c=0;c<arc;c++)Tri(tris,center,(rows-1)*(arc+1)+c,(rows-1)*(arc+1)+c+1,true);
            }
            var mesh=new Mesh{name=h.id+"_Vault"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.subMeshCount=2;mesh.SetTriangles(tris,0);mesh.SetTriangles(outer,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var obj=MeshObject(h.root,"GrandLimestoneVault",Store(mesh,mesh.name),limestone,true);obj.GetComponent<Renderer>().sharedMaterials=new[]{limestone,limestone};
            // Dry galleries and broad stone shelves above the river, with a continuous route on both sides.
            var floor=new FlatMeshBuilder();var widenedWater=new FlatMeshBuilder();
            for(int r=0;r<rows-1;r++)
            {
                var a=h.frames[r];var b=h.frames[r+1];
                foreach(int side in new[]{-1,1})
                {
                    float inner=h.forest?0:Mathf.Max(a.waterHalf,a.half-4.8f+Mathf.Sin(r*1.1f)*.65f);float innerB=h.forest?0:Mathf.Max(b.waterHalf,b.half-4.8f+Mathf.Sin((r+1)*1.1f)*.65f);
                    float height=h.forest?.12f:1.0f;
                    var p=a.p+a.right*inner*side+Vector3.up*height;var q=b.p+b.right*innerB*side+Vector3.up*height;
                    var s=a.p+a.right*(a.half+1)*side+Vector3.up*height;var t=b.p+b.right*(b.half+1)*side+Vector3.up*height;
                    floor.TriUp(0,p,q,t);floor.TriUp(0,p,t,s);
                    if(!h.forest){floor.Tri(0,p,q,p-Vector3.up*3);floor.Tri(0,q,q-Vector3.up*3,p-Vector3.up*3);}
                    floor.Tri(0,s,t,s-Vector3.up*4);floor.Tri(0,t,t-Vector3.up*4,s-Vector3.up*4);
                    if(!h.forest&&(inner>a.waterHalf+.02f||innerB>b.waterHalf+.02f))
                    {var wa=a.p+a.right*side*a.waterHalf;var wb=b.p+b.right*side*b.waterHalf;var wc=a.p+a.right*side*(inner+.04f);var wd=b.p+b.right*side*(innerB+.04f);widenedWater.TriUp(0,wa,wb,wd);widenedWater.TriUp(0,wa,wd,wc);}
                }
            }
            MeshObject(h.root,h.forest?"ExplorableGalleryFloor":"RiversideStoneWalkways",Store(floor.ToMesh(h.id+"_Floor"),h.id+"_Floor"),sand,true);
            if(!widenedWater.IsEmpty)MeshObject(h.root,"ExpandedUndergroundRiver",Store(widenedWater.ToMesh(h.id+"_Water"),h.id+"_Water"),AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Water.mat"));
        }
        static Mesh Formation(string id,bool down,bool column)
        {
            var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();const int rows=38,sides=40;
            for(int j=0;j<=rows;j++)for(int k=0;k<=sides;k++)
            {
                float t=j/(float)rows,a=k*Mathf.PI*2/sides;
                float profile=column?(.39f+.61f*(Mathf.Pow(1-t,4)+Mathf.Pow(t,5))):Mathf.Pow(1-t,down?1.1f:.5f);
                float flutes=1+.12f*Mathf.Cos(a*11+.4f*Mathf.Sin(t*5))+.045f*Mathf.Cos(a*23+t*2)+.04f*Mathf.Sin(t*41+a*2);
                float x=Mathf.Cos(a)*profile*flutes+.06f*Mathf.Sin(t*3.1f),z=Mathf.Sin(a)*profile*flutes;
                verts.Add(new Vector3(x,down?-t:t,z));uv.Add(new Vector2(k/(float)sides*4,t*8));
                if(j<rows&&k<sides){int n=j*(sides+1)+k;tris.AddRange(down?new[]{n,n+1,n+sides+1,n+1,n+sides+2,n+sides+1}:new[]{n,n+sides+1,n+1,n+1,n+sides+1,n+sides+2});}
            }
            var mesh=new Mesh{name=id};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return Store(mesh,id);
        }
        static void Place(Hall h,string name,Mesh mesh,Material mat,Vector3 p,Vector3 scale)
        {var obj=MeshObject(h.root,name,mesh,mat);obj.position=p;obj.localScale=scale;formations++;}
        static void Light(Hall h,Vector3 p,Color color,float intensity,float range)
        {var obj=new GameObject("HiddenConservationLighting");obj.transform.SetParent(h.root,false);obj.transform.position=p;var l=obj.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;}
        static void Curtain(Hall hall,Frame f,int side,int id)
        {
            var along=Vector3.Cross(f.right,Vector3.up);float top=f.height*.77f;
            Vector3 P(int x,int y)
            {
                float u=x/64f,v=y/18f;float drop=3.5f+5*Mathf.Pow(.5f+.5f*Mathf.Sin(u*12+id),2)+1.2f*Mathf.Sin(u*33);
                float fold=.4f+1.0f*Mathf.Sin(u*43)+.23f*Mathf.Sin(u*91);
                return f.p+along*((u-.5f)*15)+f.right*side*(f.half*.68f+fold*(.3f+.7f*v))+Vector3.up*(top-v*drop);
            }
            var verts=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();const int layerSize=65*19;
            for(int layer=0;layer<2;layer++)for(int x=0;x<=64;x++)for(int y=0;y<=18;y++){verts.Add(P(x,y)+f.right*side*layer*.26f);uv.Add(new Vector2(x/12f,y/8f));}
            void Face(int a,int b,int c,Vector3 normal){if(Vector3.Dot(Vector3.Cross(verts[b]-verts[a],verts[c]-verts[a]),normal)<0){int swap=b;b=c;c=swap;}tri.AddRange(new[]{a,b,c});}
            for(int layer=0;layer<2;layer++)for(int x=0;x<64;x++)for(int y=0;y<18;y++){int a=layer*layerSize+x*19+y,b=a+19;var n=f.right*side*(layer==0?-1:1);Face(a,b,a+1,n);Face(b,b+1,a+1,n);}
            for(int x=0;x<64;x++)foreach(int y in new[]{0,18}){int a=x*19+y,b=a+19;Face(a,a+layerSize,b,y==0?Vector3.up:Vector3.down);Face(b,a+layerSize,b+layerSize,y==0?Vector3.up:Vector3.down);}
            var mesh=new Mesh{name=hall.id+"_Curtain_"+id};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            MeshObject(hall.root,"MonumentalFoldedFlowstone",Store(mesh,mesh.name),ochre);
        }
        static void Decorate(Hall h,Mesh pendant,Mesh pillar,Mesh tower)
        {
            int rows=h.frames.Count;
            // A few massive formations, offset and unequal, leave open space around the river.
            for(int i=2;i<rows-2;i+=3)
            {
                var f=h.frames[i];float side=i%2==0?-1:1;var pos=f.p+f.right*side*(f.half*.82f)+Vector3.up*(h.forest?.12f:1);
                float height=f.height*(.34f+.13f*Mathf.PerlinNoise(i*.7f,rows));
                Place(h,"AncientFlutedStalagmite",tower,i%3==0?ochre:calcite,pos,new Vector3(height*.28f,height,height*.24f));
                for(int k=0;k<3;k++){float offset=(k-1)*1.8f;Place(h,"FlowstoneButtress",tower,wet,pos+Vector3.Cross(f.right,Vector3.up)*offset+f.right*side*1.1f,new Vector3(1.5f,height*(.23f+k*.1f),1.4f));}
                var roof=Surface(f,side<0?Mathf.PI*.72f:Mathf.PI*.28f)+Vector3.up*.25f;
                float drop=f.height*.28f;Place(h,"GreatCeilingPendant",pendant,calcite,roof,new Vector3(drop*.3f,drop,drop*.23f));
            }
            foreach(int side in new[]{-1,1})
            {
                int i=Mathf.RoundToInt((rows-1)*(side<0?.35f:.70f));var f=h.frames[i];float across=f.half*.80f;float a=side<0?Mathf.Acos(-.80f):Mathf.Acos(.80f);
                float height=Surface(f,a).y-f.p.y-.9f;
                Place(h,"CathedralLimestoneColumn",pillar,ochre,f.p+f.right*side*across+Vector3.up*.9f,new Vector3(2.0f,height,1.65f));
                Curtain(h,f,side,side<0?0:1);
            }
            for(int i=3;i<rows-2;i+=5)
            {
                var f=h.frames[i];float side=i%2==0?-1:1;
                Light(h,f.p+f.right*side*f.half*.65f+Vector3.up*3,new Color(1,.78f,.53f),h.forest?2.5f:3.3f,23);
                Light(h,f.p-f.right*side*f.half*.6f+Vector3.up*5,new Color(.59f,.70f,.72f),1.1f,18);
            }
            var middle=h.frames[rows/2];Light(h,middle.p+Vector3.up*(middle.height*.43f),new Color(.80f,.74f,.62f),3.0f,45);
            if(h.id.Contains("_Gallery_"))
            {
                var f=h.frames[rows-2];var along=Vector3.Cross(f.right,Vector3.up);
                for(int i=0;i<9;i++){float x=(i-4)*1.13f;float height=Surface(f,Mathf.Acos(Mathf.Clamp(x/f.half,-1,1))).y-f.p.y+.8f;Place(h,"RoyalOrganFlowstone",pillar,i%3==0?calcite:ochre,f.p+f.right*x-along*.8f+Vector3.up*.12f,new Vector3(.85f,height,.9f));}
                Light(h,f.p-along*3+Vector3.up*2,new Color(1,.72f,.41f),3.8f,16);
                var poolFrame=h.frames[rows*2/3];var center=poolFrame.p+poolFrame.right*2.1f+Vector3.up*.18f;
                var fb=new FlatMeshBuilder();var rim=new FlatMeshBuilder();
                Vector3 Edge(int i,float inset,float height){float a=i*Mathf.PI*2/96;float radius=1+.06f*Mathf.Sin(a*5)+.035f*Mathf.Sin(a*9);return center+poolFrame.right*Mathf.Cos(a)*(2.1f+inset)*radius+along*Mathf.Sin(a)*(3.1f+inset)*radius+Vector3.up*height;}
                for(int i=0;i<96;i++){fb.TriUp(0,center,Edge(i,0,0),Edge(i+1,0,0));foreach(float layer in new[]{0f,.23f,.46f}){var a=Edge(i,layer,.12f-layer*.17f);var b=Edge(i+1,layer,.12f-layer*.17f);var c=Edge(i,layer+.13f,.06f-layer*.17f);var d=Edge(i+1,layer+.13f,.06f-layer*.17f);rim.TriUp(0,a,b,c);rim.TriUp(0,b,d,c);}}
                var poolMat=Mat("DeepTurquoiseRimstonePools",new Color(.055f,.23f,.22f),.92f);poolMat.SetTexture("_BaseMap",null);
                MeshObject(h.root,"HiddenRimstonePool",Store(fb.ToMesh(h.id+"_Pool"),h.id+"_Pool"),poolMat);
                MeshObject(h.root,"TerracedCalcitePoolRims",Store(rim.ToMesh(h.id+"_PoolRims"),h.id+"_PoolRims"),calcite);
            }
        }
        static bool Inside(Vector3 p,Hall h)
        {
            if(!h.bounds.Contains(p))return false;
            float best=float.MaxValue;Frame closest=null;
            for(int i=0;i<h.frames.Count-1;i++)
            {
                var a=h.frames[i];var b=h.frames[i+1];var ab=b.p-a.p;float u=Mathf.Clamp01(Vector3.Dot(p-a.p,ab)/ab.sqrMagnitude);
                var center=Vector3.Lerp(a.p,b.p,u);float d=new Vector2(p.x-center.x,p.z-center.z).sqrMagnitude;
                if(d<best){best=d;closest=new Frame{p=center,right=Vector3.Lerp(a.right,b.right,u).normalized,half=Mathf.Lerp(a.half,b.half,u),height=Mathf.Lerp(a.height,b.height,u)};}
            }
            if(closest==null)return false;var delta=p-closest.p;
            var first=h.frames[0];var last=h.frames[h.frames.Count-1];
            if(Vector3.Dot(p-first.p,h.frames[1].p-first.p)<-2||Vector3.Dot(p-last.p,last.p-h.frames[h.frames.Count-2].p)>2)return false;
            float x=Vector3.Dot(delta,closest.right)/(closest.half+.1f),y=(delta.y+1.8f)/(closest.height+1.8f+.1f);
            return delta.y>-.1f&&x*x+y*y<1;
        }
        static int Carve(RiverTrack track)
        {
            int chunks=0;
            foreach(string group in new[]{"Terrain","Mountains"})foreach(var filter in track.transform.Find("Generated/"+group).GetComponentsInChildren<MeshFilter>())
            {
                var nearby=halls.Where(h=>h.bounds.Intersects(filter.GetComponent<Renderer>().bounds)).ToArray();if(nearby.Length==0)continue;
                var source=filter.sharedMesh;var points=source.vertices;var builder=new FlatMeshBuilder(source.subMeshCount);bool changed=false;
                bool In(Vector3 p){foreach(var h in nearby)if(Inside(p,h))return true;return false;}
                void Add(int sub,Vector3 a,Vector3 b,Vector3 c,int depth)
                {
                    var mid=(a+b+c)/3;float size=Mathf.Max((a-b).magnitude,Mathf.Max((a-c).magnitude,(b-c).magnitude));bool near=false;
                    foreach(var h in nearby)if(h.bounds.SqrDistance(mid)<size*size){near=true;break;}
                    if(near&&depth<2&&size>1.4f){var ab=(a+b)*.5f;var bc=(b+c)*.5f;var ca=(c+a)*.5f;Add(sub,a,ab,ca,depth+1);Add(sub,ab,b,bc,depth+1);Add(sub,ca,bc,c,depth+1);Add(sub,ab,bc,ca,depth+1);return;}
                    if(In(mid)&&Vector3.Cross(b-a,c-a).normalized.y<.03f){changed=true;return;}builder.Tri(sub,filter.transform.InverseTransformPoint(a),filter.transform.InverseTransformPoint(b),filter.transform.InverseTransformPoint(c));
                }
                for(int sub=0;sub<source.subMeshCount;sub++){var tri=source.GetTriangles(sub);for(int i=0;i<tri.Length;i+=3)Add(sub,filter.transform.TransformPoint(points[tri[i]]),filter.transform.TransformPoint(points[tri[i+1]]),filter.transform.TransformPoint(points[tri[i+2]]),0);}
                if(!changed)continue;var replacement=builder.ToMesh(source.name);EditorUtility.CopySerialized(replacement,source);source.SetVertices(replacement.vertices);for(int i=0;i<replacement.subMeshCount;i++)source.SetTriangles(replacement.GetTriangles(i),i);Object.DestroyImmediate(replacement);
                var collider=filter.GetComponent<MeshCollider>();if(collider!=null){collider.sharedMesh=null;collider.sharedMesh=source;}EditorUtility.SetDirty(source);chunks++;
            }
            return chunks;
        }
        public static string Build(RiverTrack track)
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","MajesticCaverns");
            limestone=Mat("WarmKarstLimestone",new Color(.37f,.32f,.26f),.18f);calcite=Mat("AgedIvoryCalcite",new Color(.61f,.53f,.39f),.3f);ochre=Mat("AmberMineralCurtains",new Color(.46f,.33f,.19f),.26f);wet=Mat("DampLimestone",new Color(.28f,.27f,.23f),.47f);sand=Mat("LimestoneSandShelves",new Color(.40f,.36f,.28f),.12f);
            var pendant=Formation("FlutedGreatPendant",true,false);var column=Formation("AncientJoinedColumn",false,true);var tower=Formation("MassiveStalagmite",false,false);
            halls.Clear();formations=0;int caves=0;
            foreach(string category in new[]{"ForestCave","Tunnels","BranchCaves"})foreach(Transform cave in track.transform.Find("Generated/"+category))
            {
                if(!cave.gameObject.activeInHierarchy)continue;
                var existing=cave.Find("MajesticChambers");if(existing!=null)Object.DestroyImmediate(existing.gameObject);
                var details=cave.Find("NaturalCavernDetails");if(details!=null)details.gameObject.SetActive(false);
                var legacy=cave.Find("CaveDetail");if(legacy!=null)legacy.gameObject.SetActive(false);
                var root=new GameObject("MajesticChambers").transform;root.SetParent(cave,false);
                var hall=new Hall{id=category+"_"+cave.name,root=root,forest=category=="ForestCave",closed=category=="ForestCave"};
                var path=new List<(Vector3 p,Vector3 right,float half)>();
                if(hall.forest)
                {
                    float bend=track.GetComponents<ForestCaveLandform>().First(p=>p.variant==int.Parse(cave.name.Split('_')[1])).bend;
                    for(float d=0;d<=48;d+=2)path.Add((cave.TransformPoint(new Vector3(Mathf.SmoothStep(0,1,d/48)*bend,.08f,d)),cave.right,4.7f));
                    cave.Find("CurvedCaveInterior").gameObject.SetActive(false);cave.Find("WalkableCaveFloor").gameObject.SetActive(false);
                }
                else if(category=="Tunnels")
                {
                    var tunnel=track.tunnels[int.Parse(cave.name.Split('_')[1])];int rows=Mathf.CeilToInt(tunnel.length/2);
                    for(int i=0;i<=rows;i++){float d=tunnel.distance-tunnel.length*.5f+tunnel.length*i/rows;var s=track.EvaluateAtDistance(d);var l=track.AfterRaceInsertion(s.center-s.right*track.HalfWidth);var r=track.AfterRaceInsertion(s.center+s.right*track.HalfWidth);path.Add(((l+r)*.5f,(r-l).normalized,Vector3.Distance(l,r)*.5f+track.waterSurfaceExtraWidth+.8f));}
                }
                else
                {
                    var branch=track.Branches[int.Parse(cave.name.Split('_')[1])];var shell=cave.Find("CaveShell").GetComponent<MeshCollider>();shell.gameObject.SetActive(true);Physics.SyncTransforms();
                    var found=new List<float>();for(float d=4;d<branch.Length-4;d+=1){var p=branch.EvaluateAtDistance(d).center+Vector3.right*branch.side*track.appliedRaceStripWidth*.5f;if(shell.Raycast(new Ray(p+Vector3.up,Vector3.up),out var hit,35))found.Add(d);}
                    if(found.Count<3){Object.DestroyImmediate(root.gameObject);continue;}
                    float from=found[0],to=found[found.Count-1];int rows=Mathf.CeilToInt((to-from)/2);
                    for(int i=0;i<=rows;i++){var s=branch.EvaluateAtDistance(Mathf.Lerp(from,to,i/(float)rows));path.Add((s.center+Vector3.right*branch.side*track.appliedRaceStripWidth*.5f,s.right,branch.HalfWidth+track.waterSurfaceExtraWidth+.8f));}
                }
                for(int i=0;i<path.Count;i++)
                {
                    float u=i/(float)(path.Count-1);float swell=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.75f);var p=path[i];
                    hall.frames.Add(new Frame{p=p.p,right=p.right,waterHalf=p.half-.8f,half=p.half+swell*(hall.forest?10:category=="Tunnels"?8:9),height=Mathf.Lerp(hall.forest?7:category=="Tunnels"?13:9,hall.forest?24:category=="Tunnels"?34:28,swell)});
                }
                if(!hall.forest)cave.Find("CaveShell").gameObject.SetActive(false);
                FinishBounds(hall);var a=Gallery(hall,-1,0);var b=Gallery(hall,1,1);halls.Add(hall);halls.Add(a);halls.Add(b);
                Shell(hall);Shell(a);Shell(b);Decorate(hall,pendant,column,tower);Decorate(a,pendant,column,tower);Decorate(b,pendant,column,tower);caves++;
            }
            int carved=Carve(track);CaveMountainExterior.Restore(track);Physics.SyncTransforms();AssetDatabase.SaveAssets();return $"Built {caves} monumental caves, {caves*2} connected side galleries, {formations} large formations; carved {carved} terrain/mountain chunks.";
        }
    }
}




