using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityPerformanceAlchemist.Samples;

namespace UnityPerformanceAlchemist.Editor
{
    public class UiSceneSetup : EditorWindow
    {
        [MenuItem("Window/Alchemist/3. Setup UGUI Rebuild Spike Scene", priority = 3)]
        public static void CreateUiTestScene()
        {
            // 1. 새로운 씬 생성
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            newScene.name = "Alchemist_UGUI_Rebuild_Spike";

            // 2. UI 전용 카메라 설정
            GameObject cameraObj = new GameObject("UI Camera");
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            cam.orthographic = true;
            cam.tag = "MainCamera";

            // 3. Event System 생성 (UI 상호작용 및 오버헤드 유발)
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();

            // 4. UI Manager (병목 시뮬레이터 부착 대상)
            GameObject uiManagerObj = new GameObject("Canvas_Bottleneck_Manager");
            var simulator = uiManagerObj.AddComponent<UiGridRebuildSimulator>();
            simulator.itemCount = 500; // 극심한 병목 유발
            simulator.updatesPerFrame = 20;

            // 5. 씬 뷰 동기화
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.AlignViewToObject(cameraObj.transform);
            }

            Debug.Log("[Alchemist] 🌶️ Spicy Level: UGUI Rebuild Test Scene Setup Complete! Open Profiler (CPU/UI tab) and press 'Play'.");
        }
    }
}
