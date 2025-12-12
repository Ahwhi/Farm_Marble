using UnityEngine;

[ExecuteAlways]
public class CameraController : MonoBehaviour {
    public Transform boardRoot;   // 보드(필드 부모) 넣어주기
    public float margin = 2.0f;   // 보드 경계 여유
    public float tiltAngle = 50f; // 위에서 내려다보는 각도
    public float rotateAngle = 45f; // 보드를 대각으로 보는 각도
    public float heightFactor = 1.2f; // 카메라 높이 보정

    private Camera cam;

    void Start() {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        UpdateCamera();
    }

    void LateUpdate() {
        // 에디터에서도 바로 반영되도록
        UpdateCamera();
    }

    void UpdateCamera() {
        if (boardRoot == null || cam == null) return;

        // 보드 전체 Bounds 계산
        var renderers = boardRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);

        Vector3 center = bounds.center;
        float size = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f + margin;

        // 정사영 카메라 세팅
        cam.orthographic = true;
        cam.orthographicSize = size;

        // 카메라 방향 = 회전 각도로 제어
        Quaternion rot = Quaternion.Euler(tiltAngle, rotateAngle, 0f);
        Vector3 dir = rot * Vector3.forward; // 카메라가 바라보는 방향

        // 거리 계산 → 높이 보정해서 위에서 내려다보도록
        float dist = (bounds.size.magnitude) * heightFactor;
        cam.transform.position = center - dir * dist;
        cam.transform.rotation = rot;
        cam.transform.LookAt(center, Vector3.up);
    }
}
