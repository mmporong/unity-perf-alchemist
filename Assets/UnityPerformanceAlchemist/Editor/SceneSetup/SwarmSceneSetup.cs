using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityPerformanceAlchemist.Samples;

namespace UnityPerformanceAlchemist.Editor
{
    public class SwarmSceneSetup : EditorWindow
    {
        [MenuItem("Window/Alchemist/1. Setup Swarm Test Scene", priority = 1)]
        public static void CreateTestScene()
        {
            // 1. 새로운 씬 생성 (저장되지 않은 빈 씬)
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            newScene.name = "Alchemist_Swarm_Test";

            // 2. 메인 카메라 세팅 (탑다운 뷰)
            GameObject cameraObj = GameObject.Find("Main Camera");
            if (cameraObj != null)
            {
                cameraObj.transform.position = new Vector3(0, 40, -10);
                cameraObj.transform.rotation = Quaternion.Euler(75, 0, 0);
                
                Camera cam = cameraObj.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f); // 어두운 배경
                cam.farClipPlane = 1000f;
            }

            // 3. 방향광 (Directional Light) 세팅
            GameObject lightObj = GameObject.Find("Directional Light");
            if (lightObj != null)
            {
                Light light = lightObj.GetComponent<Light>();
                light.intensity = 1.2f;
                // 그림자 연산 제거
                light.shadows = LightShadows.None; 
            }

            // 4. Swarm Manager(병목 시뮬레이션 주체) 객체 생성
            GameObject swarmManager = new GameObject("Swarm_Manager_Bottleneck");
            swarmManager.transform.position = Vector3.zero;
            
            // 병목 스크립트 컴포넌트 부착
            var simulation = swarmManager.AddComponent<RtsSwarmSimulation>();
            simulation.unitCount = 1500; // 극심한 병목을 유발할 수치

            // 5. 씬 뷰 카메라를 메인 카메라와 동기화
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.AlignViewToObject(cameraObj.transform);
            }

            Debug.Log("[Alchemist] 🧪 Swarm Test Scene Setup Complete! Press 'Play' to see the bottleneck (Low FPS).");
        }
    }
}
