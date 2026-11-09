using UnityEngine;

namespace AnimeOpenWorld
{
    /// <summary>
    /// 水面をプレイヤーの真下に追従させ、どこまで行っても海があるように見せる。
    /// </summary>
    public class WaterFollower : MonoBehaviour
    {
        public Transform target;
        [Tooltip("指定すると seaLevel を水面の高さとして使う")]
        public WorldStreamer world;
        public float height = 0f;
        [Tooltip("この幅単位で移動させて、波模様のチラつきを防ぐ")]
        public float snap = 8f;

        void LateUpdate()
        {
            if (target == null) return;
            float y = world != null ? world.seaLevel : height;
            Vector3 p = target.position;
            if (snap > 0f)
            {
                p.x = Mathf.Round(p.x / snap) * snap;
                p.z = Mathf.Round(p.z / snap) * snap;
            }
            transform.position = new Vector3(p.x, y, p.z);
        }
    }
}
