using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class PrefabLightmapData : MonoBehaviour {
    [System.Serializable]
    struct RendererInfo {
        public Renderer renderer;
        public int      lightmapIndex;
        public Vector4  lightmapOffsetScale;
    }

    [System.Serializable]
    struct LightInfo {
        public Light light;
        public int   lightmapBaketype;
        public int   mixedLightingMode;
    }

    [SerializeField]
    RendererInfo[] m_RendererInfo;
    [SerializeField]
    Texture2D[] m_Lightmaps;
    [SerializeField]
    Texture2D[] m_LightmapsDir;
    [SerializeField]
    Texture2D[] m_ShadowMasks;
    [SerializeField]
    LightInfo[] m_LightInfo;

    void Awake() { Init(); }

    void Init()
    {
        if (m_RendererInfo == null || m_RendererInfo.Length == 0)
            return;

        var lightmaps = LightmapSettings.lightmaps;
        int[] offsetsindexes = new int[m_Lightmaps.Length];
        int counttotal = lightmaps.Length;
        List<LightmapData> combinedLightmaps = new List<LightmapData>();

        for (int i = 0; i < m_Lightmaps.Length; i++)
        {
            bool exists = false;
            for (int j = 0; j < lightmaps.Length; j++)
            {

                if (m_Lightmaps[i] == lightmaps[j].lightmapColor)
                {
                    exists = true;
                    offsetsindexes[i] = j;

                }

            }
            if (!exists)
            {
                offsetsindexes[i] = counttotal;
                var newlightmapdata = new LightmapData
                {
                    lightmapColor = m_Lightmaps[i],
                    lightmapDir = m_LightmapsDir[i],
                    shadowMask = m_ShadowMasks[i],                    
                };
                
                combinedLightmaps.Add(newlightmapdata);

                counttotal += 1;


            }

        }

        var combinedLightmaps2 = new LightmapData[counttotal];

        lightmaps.CopyTo(combinedLightmaps2, 0);
        combinedLightmaps.ToArray().CopyTo(combinedLightmaps2, lightmaps.Length);
        LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
        ApplyRendererInfo(m_RendererInfo, offsetsindexes, m_LightInfo);
        LightmapSettings.lightmaps = combinedLightmaps2;
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }

    // called second
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Init(); }

    // called when the game is terminated
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    static void ApplyRendererInfo(RendererInfo[] infos, int[] lightmapOffsetIndex, LightInfo[] lightsInfo)
    {
        for (int i = 0; i < infos.Length; i++)
        {
            var info = infos[i];

            info.renderer.lightmapIndex = lightmapOffsetIndex[info.lightmapIndex];
            info.renderer.lightmapScaleOffset = info.lightmapOffsetScale;

            // You have to release shaders.
            Material[] mat = info.renderer.sharedMaterials;
            for (int j = 0; j < mat.Length; j++)
            {
                if (mat[j] != null && Shader.Find(mat[j].shader.name) != null)
                    mat[j].shader = Shader.Find(mat[j].shader.name);
            }

        }

        for (int i = 0; i < lightsInfo.Length; i++)
        {
            LightBakingOutput bakingOutput = new LightBakingOutput();
            bakingOutput.isBaked = true;
            bakingOutput.lightmapBakeType = (LightmapBakeType)lightsInfo[i].lightmapBaketype;
            bakingOutput.mixedLightingMode = (MixedLightingMode)lightsInfo[i].mixedLightingMode;

            lightsInfo[i].light.bakingOutput = bakingOutput;

        }


    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Bake Prefab Lightmaps", false, 70)]
    static void GenerateLightmapInfo() {
        if (UnityEditor.Lightmapping.isRunning) {
            Debug.LogError($"Please wait until lightmap baking finishes!");
            return;
        }
        if (UnityEditor.Lightmapping.giWorkflowMode != UnityEditor.Lightmapping.GIWorkflowMode.OnDemand) {
            Debug.LogError("ExtractLightmapData requires that you have baked you lightmaps and Auto mode is disabled.");
            return;
        }
        
        //UnityEditor.EditorUtility.DisplayProgressBar("Baking Prefab Lightmaps", "Baking lightmaps", 0);

        //UnityEditor.Lightmapping.completed += OnBakeCompleted;

        /*if (!UnityEditor.Lightmapping.BakeAsync()) {
            UnityEditor.Lightmapping.completed -= OnBakeCompleted;
            Debug.LogError($"Baking could not be started.");
        } else {
            Debug.Log($"Baking has started.");
        }*/
        
        OnBakeCompleted();
    }

    private static void OnBakeCompleted() {
        //UnityEditor.Lightmapping.completed -= OnBakeCompleted;
        PrefabLightmapData[] prefabs = FindObjectsOfType<PrefabLightmapData>();
        //Debug.Log(prefabs.Length);
        //UnityEditor.EditorUtility.ClearProgressBar();
        
        foreach (var instance in prefabs) {
            var gameObject    = instance.gameObject;
            var rendererInfos = new List<RendererInfo>();
            var lightmaps     = new List<Texture2D>();
            var lightmapsDir  = new List<Texture2D>();
            var shadowMasks   = new List<Texture2D>();
            var lightsInfos   = new List<LightInfo>();

            GenerateLightmapInfo(gameObject, rendererInfos, lightmaps, lightmapsDir, shadowMasks, lightsInfos);

            instance.m_RendererInfo = rendererInfos.ToArray();
            instance.m_Lightmaps    = lightmaps.ToArray();
            instance.m_LightmapsDir = lightmapsDir.ToArray();
            instance.m_LightInfo    = lightsInfos.ToArray();
            instance.m_ShadowMasks  = shadowMasks.ToArray();

            var targetPrefab =
                UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance.gameObject) as GameObject;
            if (targetPrefab != null) {
                GameObject root = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(instance.gameObject); // 根结点

                //如果当前预制体是是某个嵌套预制体的一部分（IsPartOfPrefabInstance）
                if (root != null) {
                    GameObject rootPrefab =
                        UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                    string rootPath = UnityEditor.AssetDatabase.GetAssetPath(rootPrefab);

                    //打开根部预制体
                    UnityEditor.PrefabUtility.UnpackPrefabInstanceAndReturnNewOutermostRoots(root,
                                                                                             UnityEditor
                                                                                                 .PrefabUnpackMode
                                                                                                 .OutermostRoot);
                    try {
                        //Apply各个子预制体的改变
                        UnityEditor.PrefabUtility.ApplyPrefabInstance(instance.gameObject,
                                                                      UnityEditor.InteractionMode.AutomatedAction);
                    } catch { } finally {
                        //重新更新根预制体
                        UnityEditor.PrefabUtility.SaveAsPrefabAssetAndConnect(root,
                                                                              rootPath,
                                                                              UnityEditor.InteractionMode
                                                                                         .AutomatedAction);
                    }
                } else {
                    UnityEditor.PrefabUtility.ApplyPrefabInstance(instance.gameObject,
                                                                  UnityEditor.InteractionMode.AutomatedAction);
                }
            }
        }
    }

    private static void GenerateLightmapInfo(GameObject      root,
                                             List<RendererInfo> rendererInfos,
                                             List<Texture2D> lightmaps,
                                             List<Texture2D> lightmapsDir, 
                                             List<Texture2D> shadowMasks,
                                             List<LightInfo> lightsInfo) {
        UnityEditor.EditorUtility.DisplayProgressBar($"Baking Prefab Lightmaps for {root.name}", $"Generating LightmapInfo for {root.name}", 0);

        var renderersWithNoLightmapIndex = 0;
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        for (var i = 0; i < renderers.Length; i++) {
            var renderer = renderers[i];
            UnityEditor.EditorUtility.DisplayProgressBar($"Baking Prefab Lightmaps for {root.name}",
                                                         $"Settings textures for {renderer.name}",
                                                         (i / (float) renderers.Length) / 2);
            
            var lightmapIndex = renderer.lightmapIndex;

            if (lightmapIndex == -1) {
                renderersWithNoLightmapIndex++;
                continue;
            }

            if (lightmapIndex >= LightmapSettings.lightmaps.Length) {
                Debug.LogError($"A renderer in {renderer.gameObject.name} has a lightmapIndex out of the range of LightmapSettings.lightmaps");
                continue;
            }
            
            var info = new RendererInfo {renderer = renderer};

            if (renderer.lightmapScaleOffset != Vector4.zero) {
                info.lightmapOffsetScale = renderer.lightmapScaleOffset;

                Texture2D lightmap    = LightmapSettings.lightmaps[lightmapIndex].lightmapColor;
                Texture2D lightmapDir = LightmapSettings.lightmaps[lightmapIndex].lightmapDir;
                Texture2D shadowMask  = LightmapSettings.lightmaps[lightmapIndex].shadowMask;

                info.lightmapIndex = lightmaps.IndexOf(lightmap);
                if (info.lightmapIndex == -1) {
                    info.lightmapIndex = lightmaps.Count;
                    lightmaps.Add(lightmap);
                    lightmapsDir.Add(lightmapDir);
                    shadowMasks.Add(shadowMask);
                }

                rendererInfos.Add(info);
            }
        }
        
        Debug.LogWarning($"Renderers with no lightmap index: {renderersWithNoLightmapIndex} out of {renderers.Length}");

        var lights = root.GetComponentsInChildren<Light>(true);

        for (var i = 0; i < lights.Length; i++) {
            Light l = lights[i];
            UnityEditor.EditorUtility.DisplayProgressBar($"Baking Prefab Lightmaps for {root.name}",
                                                         $"Processing light {l.name}",
                                                         .5f + ((i / (float) lights.Length) / 2));

            LightInfo lightInfo = new LightInfo();
            lightInfo.light             = l;
            lightInfo.lightmapBaketype  = (int) l.lightmapBakeType;
            lightInfo.mixedLightingMode = (int) UnityEditor.LightmapEditorSettings.mixedBakeMode;
            lightsInfo.Add(lightInfo);
        }
        
        UnityEditor.EditorUtility.ClearProgressBar();
    }
#endif
}