using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Samples
{
    /// <summary>
    /// 포트폴리오 Case 3 (Spicy Level): UGUI Canvas Rebuild & Layout Overload
    /// 대형 RPG의 인벤토리나 HUD에서 500개 이상의 UI 요소가 매 프레임 업데이트될 때 발생하는
    /// 치명적인 Canvas.SendWillRenderCanvases 및 LayoutRebuilder 병목을 시뮬레이션합니다.
    /// 
    /// AI의 목표: 
    /// 1. Raycast Target 해제
    /// 2. Canvas 분리 (정적 UI vs 동적 UI)
    /// 3. LayoutGroup 의존도 낮추기 (직접 RectTransform 계산 또는 Virtualization)
    /// </summary>
    public class UiGridRebuildSimulator : MonoBehaviour
    {
        [Header("UI Spikes Settings")]
        public int itemCount = 500;
        public int updatesPerFrame = 20; // 매 프레임 20개의 텍스트를 강제 변경하여 Rebuild 유발
        
        // [Bottleneck 1] 무거운 UI 컴포넌트들
        private List<Text> itemTexts = new List<Text>();
        private List<Image> itemImages = new List<Image>();

        private Transform gridParent;

        void Start()
        {
            // Canvas와 Grid Layout 강제 생성 및 부착
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<GraphicRaycaster>(); // [Bottleneck 2] 쓸데없는 Raycaster

            GameObject gridObj = new GameObject("HeavyGrid");
            gridObj.transform.SetParent(this.transform);
            
            // [Bottleneck 3] 500개의 자식을 가진 GridLayoutGroup (리빌드 시 엄청난 오버헤드)
            GridLayoutGroup grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(100, 50);
            grid.spacing = new Vector2(5, 5);
            
            // [Bottleneck 4] ContentSizeFitter가 GridLayoutGroup과 결합되어 최악의 연쇄 리빌드 유발
            ContentSizeFitter fitter = gridObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            gridParent = gridObj.transform;

            for (int i = 0; i < itemCount; i++)
            {
                CreateHeavyUIItem(i);
            }
        }

        private void CreateHeavyUIItem(int index)
        {
            GameObject itemObj = new GameObject("Item_" + index);
            itemObj.transform.SetParent(gridParent, false);

            // 배경 이미지
            Image bg = itemObj.AddComponent<Image>();
            bg.color = new Color(Random.value, Random.value, Random.value, 0.5f);
            // [Bottleneck 5] 클릭하지 않는 배경에도 RaycastTarget이 켜져 있음 (유니티 디폴트)
            bg.raycastTarget = true; 
            itemImages.Add(bg);

            // 텍스트 자식 객체
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(itemObj.transform, false);
            
            Text txt = textObj.AddComponent<Text>();
            txt.text = "Item " + index;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            // [Bottleneck 5] 텍스트에도 RaycastTarget 켜짐
            txt.raycastTarget = true; 
            
            // [Bottleneck 6] Text의 Layout을 더럽히는(Dirty) 설정
            txt.horizontalOverflow = HorizontalWrapMode.Wrap; 
            
            itemTexts.Add(txt);
        }

        void Update()
        {
            // [Bottleneck 7] 매 프레임 무작위 텍스트와 크기를 변경하여
            // GridLayoutGroup과 ContentSizeFitter의 연쇄적인 SetLayoutDirty를 유발함.
            // 이는 Canvas 전체의 형상을 매 프레임 처음부터 다시 계산하게 만듦.
            for (int i = 0; i < updatesPerFrame; i++)
            {
                int randomIndex = Random.Range(0, itemCount);
                
                // 텍스트 변경 (Graphic & Layout Dirty 유발)
                itemTexts[randomIndex].text = "Update " + Time.frameCount;
                
                // 크기 미세 변경 (Layout Dirty 강제 유발)
                float jitter = Random.Range(-2f, 2f);
                itemImages[randomIndex].rectTransform.sizeDelta = new Vector2(100 + jitter, 50 + jitter);
            }
        }
    }
}
