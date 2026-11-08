using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VuotThac.Boats
{
    // Shared vector artwork for the HUD and world pickups; sharp at any resolution.
    public static class ItemIconArt
    {
        static readonly Dictionary<RaceItem,Mesh> meshes=new Dictionary<RaceItem,Mesh>();
        sealed class Painter
        {
            public readonly VertexHelper mesh;
            readonly bool compact;
            public Painter(VertexHelper target,bool compact=false){mesh=target;this.compact=compact;}
            public void Triangle(Vector2 a,Vector2 b,Vector2 c,Color ca,Color cb,Color cc)
            {
                int start=mesh.currentVertCount;
                mesh.AddVert(a,ca,Vector2.zero);mesh.AddVert(b,cb,Vector2.zero);mesh.AddVert(c,cc,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);
            }
            public void Disc(Vector2 center,float radius,Color inside,Color outside,int segments=64)
            {
                if(compact)segments=Mathf.Min(segments,24);
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    Triangle(center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,inside,outside,outside);
                }
            }
            public void Ring(Vector2 center,float inner,float outer,Color top,Color bottom,float fraction=1)
            {
                int count=Mathf.Max(1,Mathf.CeilToInt((compact?32:80)*fraction));
                for(int i=0;i<count;i++)
                {
                    float a=Mathf.PI*.5f-i*Mathf.PI*2*fraction/count,b=Mathf.PI*.5f-(i+1)*Mathf.PI*2*fraction/count;
                    var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                    var ca=Color.Lerp(bottom,top,(u.y+1)*.5f);var cb=Color.Lerp(bottom,top,(v.y+1)*.5f);
                    Triangle(center+u*inner,center+u*outer,center+v*outer,ca,ca,cb);
                    Triangle(center+u*inner,center+v*outer,center+v*inner,ca,cb,cb);
                }
            }
            static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
            public void Polygon(Vector2[] points,Color top,Color bottom,float scale=1,Vector2 offset=default)
            {
                var vertices=new List<Vector2>();foreach(var point in points)vertices.Add(point*scale+offset);
                float area=0;for(int i=0;i<vertices.Count;i++)area+=Cross(vertices[i],vertices[(i+1)%vertices.Count]);
                if(area<0)vertices.Reverse();
                int guard=vertices.Count*vertices.Count;
                while(vertices.Count>2&&guard-->0)
                {
                    bool cut=false;
                    for(int i=0;i<vertices.Count;i++)
                    {
                        int previous=(i+vertices.Count-1)%vertices.Count,next=(i+1)%vertices.Count;
                        var a=vertices[previous];var b=vertices[i];var c=vertices[next];if(Cross(b-a,c-b)<=.000001f)continue;
                        bool occupied=false;
                        for(int j=0;j<vertices.Count;j++)
                        {
                            if(j==previous||j==i||j==next)continue;var p=vertices[j];
                            if(Cross(b-a,p-a)>=0&&Cross(c-b,p-b)>=0&&Cross(a-c,p-c)>=0){occupied=true;break;}
                        }
                        if(occupied)continue;
                        Triangle(a,b,c,Color.Lerp(bottom,top,Mathf.InverseLerp(-.36f,.36f,a.y)),Color.Lerp(bottom,top,Mathf.InverseLerp(-.36f,.36f,b.y)),Color.Lerp(bottom,top,Mathf.InverseLerp(-.36f,.36f,c.y)));
                        vertices.RemoveAt(i);cut=true;break;
                    }
                    if(!cut)break;
                }
            }
            public void Stroke(Vector2 start,Vector2 end,float width,Color color)
            {Stroke(start,end,width,color,color);}
            public void Stroke(Vector2 start,Vector2 end,float width,Color top,Color bottom)
            {
                var side=Vector2.Perpendicular((end-start).normalized)*width*.5f;
                Polygon(new[]{start-side,end-side,end+side,start+side},top,bottom);
            }
            public void Log(Vector2 start,Vector2 end,float radius)
            {
                var bark=new Color(.28f,.11f,.035f);var light=new Color(.84f,.47f,.19f);
                Stroke(start,end,radius*2.2f,new Color(.10f,.055f,.02f));Disc(start,radius*1.1f,bark,bark,28);Disc(end,radius*1.1f,bark,bark,28);
                Stroke(start,end,radius*1.8f,light,bark);Disc(start,radius*.9f,light,bark,28);Disc(end,radius*.9f,light,bark,28);
                var side=Vector2.Perpendicular((end-start).normalized);
                Stroke(start+side*radius*.35f,end+side*radius*.35f,.018f,new Color(1,.7f,.35f));
                Stroke(start-side*radius*.3f,end-side*radius*.3f,.016f,new Color(.22f,.085f,.02f));
                Disc(end,radius*.78f,new Color(1,.85f,.55f),new Color(.8f,.48f,.21f),36);
                Ring(end,radius*.44f,radius*.49f,bark,bark);Ring(end,radius*.17f,radius*.21f,bark,bark);
                Stroke(end,end+new Vector2(radius*.5f,-radius*.35f),.012f,bark);
            }
        }
        public static Color Accent(RaceItem item)=>item==RaceItem.Shield?new Color(.25f,.86f,1):item==RaceItem.Log?new Color(1,.63f,.26f):item==RaceItem.Boost?new Color(1,.82f,.18f):new Color(.4f,.56f,.63f);
        public static void Paint(VertexHelper target,RaceItem item,float progress=0,bool compact=false)
        {
            target.Clear();var p=new Painter(target,compact);var accent=Accent(item);
            p.Disc(new Vector2(0,-.012f),.493f,new Color(0,0,0,.5f),Color.clear);
            p.Disc(Vector2.zero,.474f,new Color(.075f,.17f,.21f),new Color(.015f,.045f,.065f));
            p.Ring(Vector2.zero,.442f,.474f,new Color(1,.91f,.61f),new Color(.48f,.25f,.075f));
            p.Ring(Vector2.zero,.425f,.431f,new Color(.7f,.86f,.86f,.75f),new Color(.14f,.27f,.3f));
            p.Disc(new Vector2(0,.025f),.36f,new Color(accent.r,accent.g,accent.b,.16f),Color.clear);
            if(item==RaceItem.Boost)
            {
                var bolt=new[]{new Vector2(.045f,.34f),new Vector2(-.25f,-.035f),new Vector2(-.065f,-.035f),new Vector2(-.115f,-.34f),new Vector2(.26f,.09f),new Vector2(.065f,.09f),new Vector2(.145f,.34f)};
                p.Polygon(bolt,new Color(.38f,.15f,.02f),new Color(.2f,.06f,0),1.12f,new Vector2(0,-.012f));
                p.Polygon(bolt,new Color(1,.98f,.66f),new Color(1,.49f,.035f));
                p.Stroke(new Vector2(-.28f,.18f),new Vector2(-.18f,.28f),.025f,new Color(1,.91f,.52f));
                p.Stroke(new Vector2(.17f,-.24f),new Vector2(.29f,-.13f),.025f,new Color(1,.75f,.23f));
            }
            else if(item==RaceItem.Shield)
            {
                var shield=new[]{new Vector2(-.28f,.28f),new Vector2(0,.36f),new Vector2(.28f,.28f),new Vector2(.25f,-.08f),new Vector2(.14f,-.25f),new Vector2(0,-.36f),new Vector2(-.14f,-.25f),new Vector2(-.25f,-.08f)};
                p.Polygon(shield,new Color(.91f,.99f,1),new Color(.28f,.55f,.7f),1.07f);
                p.Polygon(shield,new Color(.2f,.85f,1),new Color(.025f,.26f,.52f),.82f);
                p.Polygon(new[]{new Vector2(-.19f,.20f),new Vector2(-.025f,.25f),new Vector2(-.025f,-.21f),new Vector2(-.16f,-.07f)},new Color(1,1,1,.26f),new Color(1,1,1,.02f));
                p.Polygon(new[]{new Vector2(-.15f,.015f),new Vector2(-.09f,.07f),new Vector2(-.025f,-.005f),new Vector2(.13f,.15f),new Vector2(.19f,.095f),new Vector2(-.025f,-.13f)},Color.white,new Color(.7f,.96f,1));
            }
            else if(item==RaceItem.Log)
            {
                p.Log(new Vector2(-.19f,.17f),new Vector2(.19f,-.15f),.105f);
                p.Log(new Vector2(-.23f,-.16f),new Vector2(.22f,.18f),.12f);
                p.Stroke(new Vector2(-.015f,-.12f),new Vector2(.08f,.01f),.045f,new Color(.86f,.73f,.48f));
            }
            else
            {
                p.Ring(Vector2.zero,.15f,.175f,accent,accent);
                p.Stroke(new Vector2(-.24f,-.24f),new Vector2(.24f,-.24f),.025f,accent);
                p.Stroke(new Vector2(-.13f,0),new Vector2(.13f,0),.025f,accent);
            }
            if(progress>0)p.Ring(Vector2.zero,.482f,.497f,accent,accent,Mathf.Clamp01(progress));
        }
        public static Mesh WorldMesh(RaceItem item)
        {
            if(meshes.TryGetValue(item,out var mesh)&&mesh!=null)return mesh;
            using(var helper=new VertexHelper())
            {
                Paint(helper,item,0,Application.isMobilePlatform);mesh=new Mesh{name="ItemBadge_"+item};helper.FillMesh(mesh);mesh.RecalculateBounds();meshes[item]=mesh;return mesh;
            }
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ItemIconGraphic : MaskableGraphic
    {
        RaceItem item;float progress;
        public void Show(RaceItem value,float remaining=0)
        {
            if(item==value&&Mathf.Abs(progress-remaining)<.01f)return;item=value;progress=remaining;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            ItemIconArt.Paint(helper,item,progress);var rect=GetPixelAdjustedRect();float size=Mathf.Min(rect.width,rect.height);
            var vertex=new UIVertex();for(int i=0;i<helper.currentVertCount;i++)
            {helper.PopulateUIVertex(ref vertex,i);vertex.position=rect.center+(Vector2)vertex.position*size;vertex.color*=color;helper.SetUIVertex(vertex,i);}
        }
    }
    public sealed class ItemPickupVisual : MonoBehaviour
    {
        static Camera viewCamera;static int cameraFrame=-1;
        Transform badge;
        MeshRenderer badgeRenderer;
        float nextVisibility;
        public void Initialize(RaceItem item)
        {
            badge=new GameObject("ItemBadge",typeof(MeshFilter),typeof(MeshRenderer)).transform;badge.SetParent(transform,false);badge.localScale=Vector3.one*1.35f;
            badge.GetComponent<MeshFilter>().sharedMesh=ItemIconArt.WorldMesh(item);
            var renderer=badgeRenderer=badge.GetComponent<MeshRenderer>();renderer.sharedMaterial=Resources.Load<Material>("UI/ItemIconWorld");
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void LateUpdate()
        {
            if(cameraFrame!=Time.frameCount){cameraFrame=Time.frameCount;viewCamera=Camera.main;}
            if(viewCamera==null||badge==null)return;
            if(Application.isMobilePlatform)
            {
                if(Time.unscaledTime>=nextVisibility)
                {
                    nextVisibility=Time.unscaledTime+.2f;
                    badgeRenderer.forceRenderingOff=(viewCamera.transform.position-transform.position).sqrMagnitude>160*160;
                }
                if(badgeRenderer.forceRenderingOff)return;
            }
            badge.rotation=viewCamera.transform.rotation*Quaternion.AngleAxis(Mathf.Sin(Time.time*1.4f+transform.position.x)*3,Vector3.forward);
        }
    }
}
