using System.IO;
using UnityEditor;
using UnityEngine;
using VuotThac.Boats;

namespace VuotThac.EditorTools.Boats
{
    public static partial class BoatFactory
    {
        const string CrewAsset = "Assets/_Game/Generated/RowerRefinement.asset";
        [MenuItem("Vượt Thác/Chỉnh người và mái chèo")]
        public static void RefineCrew()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<BoatCatalog>(CatalogPath);
            var reference=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/People/Grab3D/Source/man-base-character-rigged.fbx"));
            var referenceRig=reference.GetComponent<Animator>();
            var armIds=new[]{HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand};
            var armPositions=new Vector3[armIds.Length];for(int n=0;n<armIds.Length;n++)armPositions[n]=referenceRig.GetBoneTransform(armIds[n]).localPosition;
            Object.DestroyImmediate(reference);
            Directory.CreateDirectory("Backups/BeforeRowingRefinement");
            if(AssetDatabase.LoadMainAssetAtPath(CrewAsset)==null)AssetDatabase.CreateAsset(new Mesh{name="RowerRefinement"},CrewAsset);
            var skin=AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+"/CrewSkin.mat");
            if(skin==null){skin=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/People/Grab3D/RealisticMan.mat")){name="CrewSkin"};AssetDatabase.CreateAsset(skin,MaterialFolder+"/CrewSkin.mat");}
            skin.SetFloat("_BumpScale",0.65f);skin.SetFloat("_Smoothness",0.20f);EditorUtility.SetDirty(skin);
            var neoprene=M("CrewNeoprene",new Color(0.045f,0.065f,0.08f),0.12f);
            var hands=M("CrewHands",new Color(0.70f,0.51f,0.39f),0.18f);
            hands.SetColor("_BaseColor",new Color(.80f,.67f,.58f));EditorUtility.SetDirty(hands);
            var nylon=M("CrewPFDNylon",new Color(0.88f,0.27f,0.045f),0.12f);
            nylon.SetTexture("_BumpMap",BoatTextures.Normal("Sailcloth"));nylon.SetTextureScale("_BumpMap",new Vector2(5,5));nylon.SetFloat("_BumpScale",0.16f);nylon.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(nylon);
            Color[] colors={new Color(.06f,.18f,.26f),new Color(.10f,.27f,.26f),new Color(.21f,.09f,.08f),new Color(.12f,.14f,.19f)};
            for(int i=0;i<catalog.boats.Length;i++)
            {
                var path=AssetDatabase.GetAssetPath(catalog.boats[i].prefab);
                string backup="Backups/BeforeRowingRefinement/"+Path.GetFileName(path);
                if(!File.Exists(backup))File.Copy(path,backup);
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if(root.name=="ThuyenRong"||root.name=="TamBan")
                    {
                        var old=root.transform.Find("Model/BoatDetails");if(old!=null)Object.DestroyImmediate(old.gameObject);
                        var spec=Specs().Find(s=>s.id==root.name);var geometry=new BoatMesh();spec.build(geometry);
                        AddDetails(root.transform.Find("Model"),spec,geometry.LastHull,true);rowerSeats.Clear();
                    }
                    int index=0;
                    foreach(var actor in root.GetComponentsInChildren<BoatRower>())
                    {
                        actor.phaseOffset=root.name=="ThuyenRong"?0:index*.05f;
                        for(int n=0;n<armIds.Length;n++)actor.rig.GetBoneTransform(armIds[n]).localPosition=armPositions[n]*1.12f;
                        if(root.name=="ThuyenThung")actor.transform.localPosition=new Vector3(.40f,-.10f,-.20f);
                        if(root.name=="BeTre")actor.transform.localPosition=new Vector3(.55f,.12f,-1.1f);
                        if(root.name=="XuongBaLa"||root.name=="DocMoc"||root.name=="ThuyenRong"||root.name=="PhaoCaoSu")
                        {var position=actor.transform.localPosition;position.y=.13f;actor.transform.localPosition=position;}
                        actor.Capture();
                        foreach(var bone in actor.rig.GetComponentsInChildren<Transform>())
                            if(bone.name=="BuoyancyVest"||bone.name=="PFDShoulderStrap")Object.DestroyImmediate(bone.gameObject);
                        var shirt=M("CrewJersey"+((i+index)%colors.Length),colors[(i+index)%colors.Length],.16f);
                        shirt.SetTexture("_BumpMap",BoatTextures.Normal("Sailcloth"));shirt.SetTextureScale("_BumpMap",new Vector2(6,6));shirt.SetFloat("_BumpScale",.12f);shirt.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(shirt);
                        foreach(var r in actor.rig.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            r.quality=SkinQuality.Bone4;
                            if(r.name=="WorkVest")r.sharedMaterial=shirt;
                            else {r.sharedMaterial=skin;for(int s=0;s<r.sharedMesh.blendShapeCount;s++)r.SetBlendShapeWeight(s,((i*7+index*3+s*5)%9-4)*6);}
                        }
                        foreach(var r in actor.GetComponentsInChildren<MeshRenderer>())
                        {
                            if(r.name=="ClosedGripHand")r.sharedMaterial=hands;
                            if(r.name.EndsWith("WorkBoot")){r.sharedMaterials=new[]{neoprene,neoprene,Gray};r.transform.localScale=new Vector3(.9f,.72f,.95f);}
                        }
                        var vest=new BoatMesh{smoothAll=true};float h=actor.standing?.91f:0;
                        foreach(float sign in new[]{-1f,1f})
                        {
                            vest.Sphere(nylon,new Vector3(sign*.096f,h+.33f,.14f),new Vector3(.088f,.165f,.028f),14,20);
                            vest.Box(White,new Vector3(sign*.096f,h+.43f,.168f),new Vector3(.030f,.065f,.006f));
                            vest.Cylinder(neoprene,new Vector3(sign*.115f,h+.46f,.135f),new Vector3(sign*.115f,h+.48f,-.10f),.023f,12);
                        }
                        vest.Sphere(nylon,new Vector3(0,h+.32f,-.12f),new Vector3(.17f,.16f,.022f),14,20);
                        vest.Box(neoprene,new Vector3(0,h+.20f,.161f),new Vector3(.33f,.028f,.012f));
                        vest.Box(Gray,new Vector3(0,h+.20f,.174f),new Vector3(.043f,.039f,.015f));
                        var mesh=SavedCrewMesh(actor.standing?"FittedPFDStanding":"FittedPFDSeated",vest,out var mats);
                        var go=new GameObject("BuoyancyVest");go.transform.SetParent(actor.transform,false);
                        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=mats;
                        go.transform.SetParent(actor.rig.GetBoneTransform(HumanBodyBones.Chest),true);
                        if(actor.stroke==BoatRower.Stroke.Oar)MakeLongOar(actor);
                        if(root.name=="ThuyenThung")MakeLongOar(actor,1.85f);
                        var visible=new System.Collections.Generic.List<Renderer>();
                        foreach(var r in actor.GetComponentsInChildren<Renderer>())
                            if(r!=actor.firstPersonArms&&r.name!="ClosedGripHand"&&(actor.paddle==null||!r.transform.IsChildOf(actor.paddle)))visible.Add(r);
                        actor.bodyRenderers=visible.ToArray();actor.Capture();actor.Pose(.30f,.65f);index++;
                    }
                    if(root.name=="ThuyenThung"||root.name=="TamBan"||root.name=="XuongBaLa"||root.name=="DocMoc"||root.name=="ThuyenRong"||root.name=="PhaoCaoSu")
                    {
                        var old=root.transform.Find("Model/CrewSeatSupport");if(old!=null)Object.DestroyImmediate(old.gameObject);
                        var m=new BoatMesh();bool basket=root.name=="ThuyenThung";
                        if(basket||root.name=="TamBan")m.Box(WoodDark,basket?new Vector3(.35f,-.20f,-.20f):new Vector3(0,-.10f,-1.65f),basket?new Vector3(.65f,.05f,.24f):new Vector3(.70f,.10f,.60f));
                        else foreach(var actor in root.GetComponentsInChildren<BoatRower>())
                            m.Box(root.name=="PhaoCaoSu"?neoprene:WoodDark,new Vector3(0,.025f,actor.transform.localPosition.z),new Vector3(root.GetComponent<BoatController>().hullBeam*.76f,.045f,.20f));
                        var mesh=SavedCrewMesh("CrewSupport_"+root.name,m,out var mats);
                        var go=new GameObject("CrewSeatSupport");go.transform.SetParent(root.transform.Find("Model"),false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=mats;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
        }
        static Mesh SavedCrewMesh(string name,BoatMesh builder,out Material[] mats)
        {
            foreach(var a in AssetDatabase.LoadAllAssetsAtPath(CrewAsset))if(a is Mesh m&&m.name==name)
            {
                var temp=builder.Build(name,out mats);
                m.Clear();m.indexFormat=temp.indexFormat;m.SetVertices(temp.vertices);m.SetNormals(temp.normals);m.SetUVs(0,temp.uv);m.subMeshCount=temp.subMeshCount;
                for(int i=0;i<temp.subMeshCount;i++)m.SetTriangles(temp.GetTriangles(i),i);
                m.RecalculateBounds();m.UploadMeshData(false);EditorUtility.SetDirty(m);Object.DestroyImmediate(temp);return m;
            }
            var mesh=builder.Build(name,out mats);AssetDatabase.AddObjectToAsset(mesh,CrewAsset);return mesh;
        }
        static void MakeLongOar(BoatRower actor,float length=2.4f)
        {
            var m=new BoatMesh();
            m.Cylinder(Wood,new Vector3(0,-length*.5f,0),new Vector3(0,length*.5f,0),.018f,18);
            m.Sphere(WoodLight,new Vector3(0,-length*.5f,0),new Vector3(.09f,.22f,.035f),12,20);
            actor.paddle.GetComponent<MeshFilter>().sharedMesh=SavedCrewMesh("FixedLengthOar_"+length.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),m,out var mats);
            actor.paddle.GetComponent<MeshRenderer>().sharedMaterials=mats;
        }
    }
}
