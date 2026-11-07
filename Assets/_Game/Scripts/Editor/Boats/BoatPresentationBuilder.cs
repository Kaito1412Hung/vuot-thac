using UnityEditor;
using UnityEngine;
using VuotThac.Boats;
using VuotThac.EditorTools.Track;

namespace VuotThac.EditorTools.Boats
{
    public static partial class BoatFactory
    {
        static void BuildWaterOcclusion(Transform parent,Spec spec,BoatMesh.HullShape? hull)
        {
            if(spec.id=="BeTre")return;
            const string path=MaterialFolder+"/BoatWaterOcclusion.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("VuotThac/BoatWaterOcclusion"));AssetDatabase.CreateAsset(mat,path);}
            var builder=new BoatMesh();
            if(hull.HasValue)
            {
                var h=hull.Value;
                Vector3 Point(int row,float side)
                {
                    float t=Mathf.Lerp(-(1-h.sternCut),1,row/64f);
                    float full=t>=0?h.bowFullness:h.sternFullness;
                    float width=h.beam*0.5f*Mathf.Max(0,1-Mathf.Pow(Mathf.Abs(t),full));
                    return new Vector3(side*width*0.88f,h.freeboard+h.rocker*t*t+0.005f,t*h.length*0.5f);
                }
                for(int row=0;row<64;row++)builder.DoubleQuad(mat,Point(row,-1),Point(row+1,-1),Point(row+1,1),Point(row,1));
            }
            else if(spec.id=="ThuyenThung")
            {
                for(int i=0;i<64;i++)
                {
                    float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
                    builder.DoubleTri(mat,new Vector3(0,0.13f,0),new Vector3(Mathf.Sin(a)*0.87f,0.13f,Mathf.Cos(a)*0.87f),new Vector3(Mathf.Sin(b)*0.87f,0.13f,Mathf.Cos(b)*0.87f));
                }
            }
            else builder.DoubleQuad(mat,new Vector3(-0.60f,0.10f,-1.45f),new Vector3(-0.60f,0.10f,1.45f),new Vector3(0.60f,0.10f,1.45f),new Vector3(0.60f,0.10f,-1.45f));
            var mesh=builder.Build("WaterExclusion_"+spec.id,out var mats);AssetDatabase.AddObjectToAsset(mesh,MeshAsset);
            var go=new GameObject("DryInteriorWaterMask");go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=mats;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        static Mesh closedBodyMesh;
        static void AddGripHands(GameObject human,Animator animator)
        {
            var body=human.GetComponentInChildren<SkinnedMeshRenderer>();
            if(closedBodyMesh==null)
            {
                closedBodyMesh=Object.Instantiate(body.sharedMesh);closedBodyMesh.name="RowerBodyWithoutOpenHands";
                var weights=closedBodyMesh.boneWeights;
                int left=System.Array.IndexOf(body.bones,animator.GetBoneTransform(HumanBodyBones.LeftHand));
                int right=System.Array.IndexOf(body.bones,animator.GetBoneTransform(HumanBodyBones.RightHand));
                bool Hand(int index)
                {
                    var w=weights[index];float total=0;
                    if(w.boneIndex0==left||w.boneIndex0==right)total+=w.weight0;if(w.boneIndex1==left||w.boneIndex1==right)total+=w.weight1;
                    if(w.boneIndex2==left||w.boneIndex2==right)total+=w.weight2;if(w.boneIndex3==left||w.boneIndex3==right)total+=w.weight3;
                    return total>0.45f;
                }
                for(int sub=0;sub<closedBodyMesh.subMeshCount;sub++)
                {
                    var tris=closedBodyMesh.GetTriangles(sub);var kept=new System.Collections.Generic.List<int>();
                    for(int i=0;i<tris.Length;i+=3)if(!(Hand(tris[i])&&Hand(tris[i+1])&&Hand(tris[i+2])))kept.AddRange(new[]{tris[i],tris[i+1],tris[i+2]});
                    closedBodyMesh.SetTriangles(kept,sub);
                }
                AssetDatabase.AddObjectToAsset(closedBodyMesh,MeshAsset);
            }
            body.sharedMesh=closedBodyMesh;
            foreach(bool left in new[]{true,false})
            {
                var hand=new BoatMesh{smoothAll=true};
                hand.Sphere(Skin,new Vector3(0,-0.018f,0.037f),new Vector3(0.044f,0.019f,0.046f),12,18);
                hand.Sphere(Skin,Vector3.zero,new Vector3(0.026f,0.022f,0.022f),10,18);
                for(int finger=0;finger<4;finger++)
                {
                    float x=-0.030f+finger*0.020f;
                    Vector3 previous=new Vector3(x,-0.026f,0.066f);
                    for(int joint=1;joint<=10;joint++)
                    {
                        float angle=joint/10f*Mathf.PI*1.32f;
                        var next=new Vector3(x,-Mathf.Cos(angle)*0.026f,0.075f+Mathf.Sin(angle)*0.026f);
                        hand.Cylinder(Skin,previous,next,0.0085f,12);hand.Sphere(Skin,next,Vector3.one*0.0085f,6,10);previous=next;
                    }
                }
                float side=left?-1:1;
                var thumbA=new Vector3(side*0.040f,-0.015f,0.022f);var thumbB=new Vector3(side*0.050f,0.012f,0.060f);var thumbC=new Vector3(side*0.018f,0.026f,0.083f);
                hand.Cylinder(Skin,thumbA,thumbB,0.012f,16);hand.Sphere(Skin,thumbB,Vector3.one*0.012f,8,14);hand.Cylinder(Skin,thumbB,thumbC,0.010f,16);hand.Sphere(Skin,thumbC,Vector3.one*0.010f,8,14);
                var mesh=hand.Build(left?"ClosedLeftHand":"ClosedRightHand",out var mats);AssetDatabase.AddObjectToAsset(mesh,MeshAsset);
                var go=new GameObject("ClosedGripHand");go.transform.SetParent(animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand),false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=mats;
            }
        }
        static void BuildRealisticCrew(GameObject boat,Transform parent,Spec spec)
        {
            const string folder="Assets/ThirdParty/People/Grab3D/";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Source/man-base-character-rigged.fbx");
            var skin=AssetDatabase.LoadAssetAtPath<Material>(folder+"RealisticMan.mat");
            var template=VillagerAppearance.Prepare(source,"Male");
            for(int index=0;index<rowerSeats.Count;index++)
            {
                var seat=rowerSeats[index];
                var root=new GameObject("Rower_"+index);root.transform.SetParent(parent,false);root.transform.localPosition=seat.position;
                var human=(GameObject)PrefabUtility.InstantiatePrefab(source);human.name="HumanRower";human.transform.SetParent(root.transform,false);
                var animator=human.GetComponent<Animator>();animator.enabled=false;animator.applyRootMotion=false;
                foreach(var renderer in human.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.sharedMaterial=skin;renderer.updateWhenOffscreen=true;}
                VillagerAppearance.Apply(template,human,animator,index%6,Orange,Dark,Dark,Gray);
                AddGripHands(human,animator);
                float scale=1.74f/(template.top-template.bottom);human.transform.localScale=Vector3.one*scale;
                var hip=animator.GetBoneTransform(HumanBodyBones.Hips);
                var desiredHip=new Vector3(0,seat.standing?0.91f:0,0);
                human.transform.localPosition+=desiredHip-root.transform.InverseTransformPoint(hip.position);
                var actor=root.AddComponent<BoatRower>();actor.rig=animator;actor.side=seat.side;actor.standing=seat.standing;
                actor.stroke=(BoatRower.Stroke)(int)seat.paddle;actor.phaseOffset=index*0.06f;
                var eye=new GameObject("FirstPersonEye");eye.transform.SetParent(root.transform,false);actor.eye=eye.transform;
                if(seat.paddle!=Paddle.None)
                {
                    var paddleMesh=new BoatMesh();float length=seat.paddle==Paddle.Double?2.25f:seat.paddle==Paddle.Pole?3.2f:1.6f;
                    paddleMesh.Cylinder(seat.paddle==Paddle.Double?Dark:Wood,new Vector3(0,-length*0.5f,0),new Vector3(0,length*0.5f,0),0.018f,18);
                    if(seat.paddle!=Paddle.Pole)
                    {
                        paddleMesh.Sphere(seat.paddle==Paddle.Double?Yellow:WoodLight,new Vector3(0,-length*0.5f,0),new Vector3(0.09f,0.22f,0.035f),12,20);
                        if(seat.paddle==Paddle.Double)paddleMesh.Sphere(Yellow,new Vector3(0,length*0.5f,0),new Vector3(0.09f,0.22f,0.035f),12,20);
                    }
                    var mesh=paddleMesh.Build("Paddle_"+spec.id+"_"+index,out var materials);AssetDatabase.AddObjectToAsset(mesh,MeshAsset);
                    var tool=new GameObject("AnimatedPaddle");tool.transform.SetParent(root.transform,false);tool.AddComponent<MeshFilter>().sharedMesh=mesh;tool.AddComponent<MeshRenderer>().sharedMaterials=materials;actor.paddle=tool.transform;
                }
                // Vest straps and reflective tabs follow the chest rather than floating beside the torso.
                var chest=animator.GetBoneTransform(HumanBodyBones.Chest)??animator.GetBoneTransform(HumanBodyBones.Spine);
                var vest=new BoatMesh();
                foreach(float sign in new[]{-1f,1f})
                {
                    vest.Sphere(Orange,desiredHip+new Vector3(sign*0.105f,0.32f,0.145f),new Vector3(0.105f,0.20f,0.065f),12,20);
                    vest.Box(White,desiredHip+new Vector3(sign*0.105f,0.43f,0.208f),new Vector3(0.035f,0.09f,0.012f));
                }
                vest.Sphere(Orange,desiredHip+new Vector3(0,0.31f,-0.13f),new Vector3(0.205f,0.20f,0.06f),12,20);
                var vestMesh=vest.Build("PFD_"+spec.id+"_"+index,out var vestMaterials);AssetDatabase.AddObjectToAsset(vestMesh,MeshAsset);
                var vestGo=new GameObject("BuoyancyVest");vestGo.transform.SetParent(root.transform,false);vestGo.AddComponent<MeshFilter>().sharedMesh=vestMesh;vestGo.AddComponent<MeshRenderer>().sharedMaterials=vestMaterials;vestGo.transform.SetParent(chest,true);
                foreach(float side in new[]{-1f,1f})
                {
                    var strap=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(strap.GetComponent<Collider>());strap.name="PFDShoulderStrap";
                    strap.transform.SetParent(root.transform,false);strap.transform.localPosition=desiredHip+new Vector3(side*0.115f,0.31f,0.14f);strap.transform.localScale=new Vector3(0.04f,0.31f,0.018f);
                    strap.GetComponent<MeshRenderer>().sharedMaterial=Dark;strap.transform.SetParent(chest,true);
                }
                var visible=new System.Collections.Generic.List<Renderer>();
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(renderer.name!="ClosedGripHand"&&(actor.paddle==null||!renderer.transform.IsChildOf(actor.paddle)))visible.Add(renderer);
                actor.bodyRenderers=visible.ToArray();
                var body=human.GetComponentInChildren<SkinnedMeshRenderer>();
                var armsMesh=Object.Instantiate(body.sharedMesh);armsMesh.name="FirstPersonArms_"+spec.id+"_"+index;
                var weights=armsMesh.boneWeights;
                var leftArm=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);var rightArm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var armBones=new System.Collections.Generic.HashSet<int>();
                for(int bone=0;bone<body.bones.Length;bone++)if(body.bones[bone].IsChildOf(leftArm)||body.bones[bone].IsChildOf(rightArm))armBones.Add(bone);
                bool IsArm(int vertex)
                {
                    var w=weights[vertex];float total=0;
                    if(armBones.Contains(w.boneIndex0))total+=w.weight0;if(armBones.Contains(w.boneIndex1))total+=w.weight1;
                    if(armBones.Contains(w.boneIndex2))total+=w.weight2;if(armBones.Contains(w.boneIndex3))total+=w.weight3;
                    return total>0.55f;
                }
                for(int sub=0;sub<armsMesh.subMeshCount;sub++)
                {
                    var triangles=armsMesh.GetTriangles(sub);var kept=new System.Collections.Generic.List<int>();
                    for(int k=0;k<triangles.Length;k+=3)if(IsArm(triangles[k])&&IsArm(triangles[k+1])&&IsArm(triangles[k+2]))kept.AddRange(new[]{triangles[k],triangles[k+1],triangles[k+2]});
                    armsMesh.SetTriangles(kept,sub);
                }
                AssetDatabase.AddObjectToAsset(armsMesh,MeshAsset);
                var armsGo=new GameObject("FirstPersonArms");armsGo.transform.SetParent(body.transform,false);
                var arms=armsGo.AddComponent<SkinnedMeshRenderer>();arms.sharedMesh=armsMesh;arms.bones=body.bones;arms.rootBone=body.rootBone;
                arms.sharedMaterials=body.sharedMaterials;arms.localBounds=body.localBounds;arms.updateWhenOffscreen=true;
                arms.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;arms.enabled=false;actor.firstPersonArms=arms;
                actor.Capture();actor.Pose(0.18f+index*0.06f,0.65f);
            }
        }

        static void AddBoatFittings(Transform model,Spec spec)
        {
            var details=new BoatMesh();
            float bow=spec.length*0.34f;
            if(spec.id=="CaNoMay")
            {
                var center=rowerSeats[0].position+new Vector3(0,0.3f,0.35f);
                var wheel=new System.Collections.Generic.List<Vector3>();
                for(int i=0;i<40;i++){float angle=i*Mathf.PI*2/40;wheel.Add(center+new Vector3(Mathf.Cos(angle)*0.23f,Mathf.Sin(angle)*0.23f,0));}
                details.Tube(Dark,wheel,0.014f,12);
                for(int i=0;i<3;i++)details.Cylinder(Gray,center,wheel[i*13],0.012f,12);
                details.Cylinder(Dark,center,center+new Vector3(0,-0.24f,0.27f),0.022f,16);
            }
            if(spec.id=="Kayak"||spec.id=="PhaoCaoSu")
            {
                for(int i=0;i<3;i++)
                {
                    float z=bow-i*0.15f;
                    details.Cylinder(Dark,new Vector3(-spec.beam*0.27f,0.19f,z-0.14f),new Vector3(spec.beam*0.27f,0.19f,z),0.009f,10);
                    details.Cylinder(Dark,new Vector3(spec.beam*0.27f,0.19f,z-0.14f),new Vector3(-spec.beam*0.27f,0.19f,z),0.009f,10);
                }
            }
            else
            {
                // Coiled painter line and timber ribs add believable construction detail.
                var rope=new System.Collections.Generic.List<Vector3>();
                for(int i=0;i<72;i++)
                {
                    float angle=i*Mathf.PI*0.19f,radius=0.10f+i*0.0012f;
                    rope.Add(new Vector3(Mathf.Sin(angle)*radius,0.18f,bow+Mathf.Cos(angle)*radius));
                }
                for(int i=1;i<rope.Count;i++)details.Cylinder(Hat,rope[i-1],rope[i],0.008f,8);
            }
            var mesh=details.Build("Fittings_"+spec.id,out var materials);AssetDatabase.AddObjectToAsset(mesh,MeshAsset);
            var go=new GameObject("DeckFittings");go.transform.SetParent(model,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;
        }
    }
}
