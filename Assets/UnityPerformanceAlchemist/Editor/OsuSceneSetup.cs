using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityPerformanceAlchemist.Samples;

namespace UnityPerformanceAlchemist.Editor
{
    public class OsuSceneSetup : EditorWindow
    {
        [MenuItem("Window/Alchemist/2. Setup Osu GC Spike Scene", priority = 2)]
        public static void CreateOsuTestScene()
        {
            // 1. 새로운 빈 씬 생성
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            newScene.name = "Alchemist_Osu_GC_Spike";

            // 2. 2D 리듬게임용 카메라 세팅
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.transform.position = new Vector3(0, 0, -10);
            
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.orthographic = true; // 2D 뷰
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.05f); // 매우 어두운 배경 (osu! 스타일)
            cam.tag = "MainCamera";

            // 3. 판정선(Hit Line) 시각화 보조 객체 생성
            GameObject hitLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hitLine.name = "HitLine_Visual";
            hitLine.transform.position = new Vector3(0, -4f, 0); // OsuGCSimulator의 hitLineY와 동일
            hitLine.transform.localScale = new Vector3(12f, 0.1f, 1f);
            
            var lineRenderer = hitLine.GetComponent<MeshRenderer>();
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            
            // 빨간색 머티리얼 적용
            Material redMat = new Material(Shader.Find("Standard"));
            redMat.color = Color.red;
            lineRenderer.material = redMat;

            // 4. GC 병목 시뮬레이터 객체 생성
            GameObject beatmapManager = new GameObject("Beatmap_Spike_Manager");
            beatmapManager.transform.position = Vector3.zero;
            
            // 스크립트 부착
            var simulator = beatmapManager.AddComponent<OsuGCSimulator>();
            simulator.spawnRatePerSecond = 120f; // Deathstream 모드

            // 5. 씬 뷰 동기화
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.AlignViewToObject(cameraObj.transform);
            }

            Debug.Log("[Alchemist] 🎵 Osu! GC Spike Test Scene Setup Complete! Open Profiler (Memory tab) and press 'Play'.");
        }
    }
}
