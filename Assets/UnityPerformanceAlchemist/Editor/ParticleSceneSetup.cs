using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityPerformanceAlchemist.Samples;

namespace UnityPerformanceAlchemist.Editor
{
    public class ParticleSceneSetup : EditorWindow
    {
        [MenuItem("Window/Alchemist/4. Setup Particle Overdraw Scene", priority = 4)]
        public static void CreateParticleTestScene()
        {
            // 1. 새로운 씬 생성
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            newScene.name = "Alchemist_Particle_Overdraw";

            // 2. 카메라 세팅 (파티클 전체가 보이도록 약간 위에서 내려다보는 뷰)
            GameObject cameraObj = GameObject.Find("Main Camera");
            if (cameraObj != null)
            {
                cameraObj.transform.position = new Vector3(0, 12, -20);
                cameraObj.transform.rotation = Quaternion.Euler(25, 0, 0);

                Camera cam = cameraObj.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
                cam.farClipPlane = 500f;
            }

            // 3. 방향광 세팅 (그림자 제거 → 순수 GPU Fill Rate 병목 유도)
            GameObject lightObj = GameObject.Find("Directional Light");
            if (lightObj != null)
            {
                Light light = lightObj.GetComponent<Light>();
                light.intensity = 0.8f;
                light.shadows = LightShadows.None;
            }

            // 4. 파티클 오버드로 시뮬레이터 생성
            GameObject particleManager = new GameObject("Particle_Overdraw_Bottleneck");
            particleManager.transform.position = Vector3.zero;

            var simulator = particleManager.AddComponent<ParticleOverdrawSimulator>();
            simulator.systemCount = 50;
            simulator.particlesPerSystem = 100;

            // 5. 씬 뷰 동기화
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.AlignViewToObject(cameraObj.transform);
            }

            Debug.Log("[Alchemist] 🎆 Particle Overdraw Test Scene Setup Complete! Open Profiler (GPU/Rendering tab) and press 'Play'.");
        }
    }
}
