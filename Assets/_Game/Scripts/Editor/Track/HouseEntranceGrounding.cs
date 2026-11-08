using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VuotThac.Track;
using Object=UnityEngine.Object;

namespace VuotThac.EditorTools.Track
{
    public static class HouseEntranceGrounding
    {
        static Transform terrain,mountains;
        public static string LastReport {get;private set;}
        static float Ground(Vector3 p)
        {
            float y=float.MinValue;
            foreach(var h in Physics.RaycastAll(new Vector3(p.x,500,p.z),Vector3.down,1000,~0,QueryTriggerInteraction.Ignore))
                if(h.transform.IsChildOf(terrain)||(mountains!=null&&h.transform.IsChildOf(mountains)))y=Mathf.Max(y,h.point.y);
            if(y==float.MinValue)throw new InvalidOperationException("No terrain beneath house entrance at "+p);
            return y;
        }
        static Mesh SaveMesh(Mesh fresh,string key)
        {
            // Each keyed mesh has a separate asset, retaining stable references when the repair is repeated.
            string path="Assets/_Game/Generated/HouseEntranceGrounding/"+key+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(fresh,path);return fresh;}
            saved.Clear();saved.indexFormat=fresh.indexFormat;saved.SetVertices(fresh.vertices);saved.SetNormals(fresh.normals);saved.SetUVs(0,fresh.uv);saved.subMeshCount=fresh.subMeshCount;
            for(int i=0;i<fresh.subMeshCount;i++)saved.SetTriangles(fresh.GetTriangles(i),i);
            saved.RecalculateBounds();saved.UploadMeshData(false);EditorUtility.SetDirty(saved);Object.DestroyImmediate(fresh);return saved;
        }
        static Mesh WithoutFixedSteps(Mesh source,float front,string key)
        {
            var result=Object.Instantiate(source);result.name=source.name;
            var vertices=source.vertices;
            bool InStep(int index){var p=vertices[index];return p.x>=.049f&&p.x<=1.551f&&p.y>=-.551f&&p.y<=-.049f&&p.z<-front-1.249f&&p.z>=-front-2.351f;}
            for(int s=0;s<source.subMeshCount;s++)
            {
                var old=source.GetTriangles(s);var kept=new List<int>();
                for(int i=0;i<old.Length;i+=3)if(!(InStep(old[i])&&InStep(old[i+1])&&InStep(old[i+2])))kept.AddRange(new[]{old[i],old[i+1],old[i+2]});
                result.SetTriangles(kept,s);
            }
            return SaveMesh(result,key);
        }
        static void Box(FlatMeshBuilder mesh,Vector3 min,Vector3 max)
        {
            var a=new Vector3(min.x,min.y,min.z);var b=new Vector3(max.x,min.y,min.z);var c=new Vector3(max.x,min.y,max.z);var d=new Vector3(min.x,min.y,max.z);
            var e=new Vector3(min.x,max.y,min.z);var f=new Vector3(max.x,max.y,min.z);var g=new Vector3(max.x,max.y,max.z);var h=new Vector3(min.x,max.y,max.z);
            Vector3 inside=(min+max)*.5f;
            Quad(mesh,a,b,f,e,inside);Quad(mesh,b,c,g,f,inside);Quad(mesh,c,d,h,g,inside);Quad(mesh,d,a,e,h,inside);Quad(mesh,e,f,g,h,inside);Quad(mesh,d,c,b,a,inside);
        }
        static void Quad(FlatMeshBuilder mesh,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 inside)
        {mesh.TriOutward(0,a,b,c,inside);mesh.TriOutward(0,a,c,d,inside);}
        static float LowGround(Transform house,float x0,float x1,float z0,float z1)
        {
            float low=float.MaxValue;
            foreach(float x in new[]{x0,(x0+x1)*.5f,x1})foreach(float z in new[]{z0,(z0+z1)*.5f,z1})
            {var p=house.TransformPoint(x,0,z);p.y=Ground(p)-.10f;low=Mathf.Min(low,house.InverseTransformPoint(p).y);}
            return low;
        }
        static void Skirt(FlatMeshBuilder mesh,Transform house,float x0,float x1,float z0,float z1,float top)
        {
            int nx=Mathf.CeilToInt((x1-x0)/.4f),nz=Mathf.CeilToInt((z1-z0)/.4f);
            for(int i=0;i<nx;i++)foreach(float z in new[]{z0,z1-.12f})
            {float a=Mathf.Lerp(x0,x1,i/(float)nx),b=Mathf.Lerp(x0,x1,(i+1)/(float)nx);Box(mesh,new Vector3(a,Mathf.Min(top-.05f,LowGround(house,a,b,z,z+.12f)),z),new Vector3(b,top,z+.12f));}
            for(int i=0;i<nz;i++)foreach(float x in new[]{x0,x1-.12f})
            {float a=Mathf.Lerp(z0,z1,i/(float)nz),b=Mathf.Lerp(z0,z1,(i+1)/(float)nz);Box(mesh,new Vector3(x,Mathf.Min(top-.05f,LowGround(house,x,x+.12f,a,b)),a),new Vector3(x+.12f,top,b));}
        }
        public static void Repair(RiverTrack track)
        {
            Directory.CreateDirectory("Assets/_Game/Generated/HouseEntranceGrounding");AssetDatabase.Refresh();
            var generated=track.transform.Find("Generated");terrain=generated.Find("Terrain");mountains=generated.Find("Mountains");Physics.SyncTransforms();
            var houses=new List<MeshFilter>();foreach(var f in generated.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null&&(f.sharedMesh.name=="Cabin"||f.sharedMesh.name=="HouseTall"))houses.Add(f);
            var cleaned=new Dictionary<string,Mesh>();int stepsTotal=0,hidden=0,tiles=0;
            var entrances=new List<Tuple<Transform,float,float>>();
            for(int index=0;index<houses.Count;index++)
            {
                var house=houses[index];bool tall=house.sharedMesh.name=="HouseTall";float front=tall?2.25f:2;
                string type=tall?"HouseTall":"Cabin";
                if(!cleaned.ContainsKey(type))cleaned[type]=WithoutFixedSteps(house.sharedMesh,front,type);
                house.sharedMesh=cleaned[type];var collider=house.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=cleaned[type];
                var old=house.transform.Find("GroundedEntrance");if(old!=null)Object.DestroyImmediate(old.gameObject);
                float start=-front-1.45f,run=.25f/house.transform.lossyScale.z,top=.19f;
                int count=0;float bottom=top;
                for(int n=1;n<=64;n++)
                {
                    float end=start-n*run;
                    var p=house.transform.TransformPoint(.8f,0,end);float height=float.MinValue;
                    foreach(float x in new[]{.05f,.8f,1.55f})foreach(float z in new[]{end,end+run*.5f,end+run})height=Mathf.Max(height,Ground(house.transform.TransformPoint(x,0,z))+.025f);
                    int hint=-1;var water=track.SampleWater(p,ref hint);
                    float drop=house.transform.TransformPoint(.8f,top,start).y-height;
                    if(drop>0&&drop/n<=.20f&&!water.inWater&&water.edgeDistance>.6f){count=n;p.y=height;bottom=house.transform.InverseTransformPoint(p).y;break;}
                }
                if(count==0)throw new InvalidOperationException("Cannot safely connect entrance for "+house.name);
                var mesh=new FlatMeshBuilder();
                Skirt(mesh,house.transform,-2.65f,2.65f,-front-.125f,front+.125f,-.13f);
                Skirt(mesh,house.transform,-2.50f,2.50f,start,-front+.05f,.065f);
                for(int i=0;i<count;i++)
                {
                    float z0=start-(i+1)*run,z1=start-i*run;
                    float y=Mathf.Lerp(top,bottom,(i+1)/(float)count);
                    float baseY=Mathf.Min(y-.08f,LowGround(house.transform,.05f,1.55f,z0,z1));
                    Box(mesh,new Vector3(.05f,baseY,z0),new Vector3(1.55f,y,z1+.008f));
                }
                var go=new GameObject("GroundedEntrance");go.transform.SetParent(house.transform,false);go.isStatic=true;
                var saved=SaveMesh(mesh.ToMesh("GroundedEntrance_"+index),"Entrance_"+index);
                go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterial=house.GetComponent<Renderer>().sharedMaterials[3];go.AddComponent<MeshCollider>().sharedMesh=saved;
                entrances.Add(Tuple.Create(house.transform,start,start-count*run));stepsTotal+=count;
            }
            int movedTrees=0;
            var trees=new List<Transform>();foreach(string name in new[]{"Trees","DeepForest","MountainForest"}){var group=generated.Find(name);if(group!=null)foreach(Transform tree in group)trees.Add(tree);}
            foreach(var tree in trees)foreach(var e in entrances)
            {
                var local=e.Item1.InverseTransformPoint(tree.position);
                if(local.x<=-.25f||local.x>=1.85f||local.z>=e.Item2||local.z<=e.Item3-.5f)continue;
                float offset=tree.position.y-Ground(tree.position);bool moved=false;
                foreach(float x in new[]{-.95f,2.55f,-1.8f,3.4f})
                {
                    var p=e.Item1.TransformPoint(x,0,local.z);int hint=-1;var water=track.SampleWater(p,ref hint);if(water.inWater||water.edgeDistance<.7f)continue;
                    bool occupied=false;foreach(var other in trees){if(other==tree)continue;var delta=other.position-p;delta.y=0;if(delta.sqrMagnitude<1.8f){occupied=true;break;}}
                    if(occupied)continue;p.y=Ground(p)+offset;tree.position=p;movedTrees++;moved=true;break;
                }
                if(!moved)throw new InvalidOperationException("Tree blocks grounded entrance: "+tree.name);
            }
            foreach(var t in generated.GetComponentsInChildren<Transform>(true))if(t.name=="StonePath")
            {
                bool covered=false;
                foreach(var e in entrances){var p=e.Item1.InverseTransformPoint(t.position);if(p.x>-.25f&&p.x<1.85f&&p.z<e.Item2+.25f&&p.z>e.Item3-.15f){covered=true;break;}}
                t.gameObject.SetActive(!covered);if(covered){hidden++;continue;}
                float low=float.MaxValue,high=float.MinValue;
                foreach(float x in new[]{-.5f,0,.5f})foreach(float z in new[]{-.5f,0,.5f}){float y=Ground(t.TransformPoint(x,0,z));low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
                var pos=t.position;pos.y=(high+.025f+low-.08f)*.5f;t.position=pos;
                var scale=t.localScale;scale.y=(high-low+.105f)/t.parent.lossyScale.y;t.localScale=scale;tiles++;
            }
            Physics.SyncTransforms();AssetDatabase.SaveAssets();LastReport="Houses="+houses.Count+", grounded steps="+stepsTotal+", supported path tiles="+tiles+", tiles beneath stairs hidden="+hidden+", trees moved aside="+movedTrees;
        }
    }
}
