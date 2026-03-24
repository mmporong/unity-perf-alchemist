using UnityEngine;
using System.Collections.Generic;

public class OptimizationTest : MonoBehaviour
{
    // [Bottleneck 1] 매 프레임 리스트 생성 (GC 할당)
    // [Bottleneck 2] Update 내에서 GetComponent 반복 호출
    // [Bottleneck 3] 비효율적인 거리 계산 (Magnitude 대신 sqrMagnitude 미사용)

    void Update()
    {
        List<GameObject> objects = new List<GameObject>(); // 매 프레임 할당
        GameObject[] allObjects = GameObject.FindGameObjectsWithTag("Player");

        foreach (var obj in allObjects)
        {
            // 매 프레임 GetComponent 호출
            Transform target = GetComponent<Transform>();
            
            float dist = Vector3.Distance(transform.position, obj.transform.position);
            
            if (dist < 10.0f)
            {
                objects.Add(obj);
                Debug.Log("Object nearby: " + obj.name);
            }
        }
    }
}
