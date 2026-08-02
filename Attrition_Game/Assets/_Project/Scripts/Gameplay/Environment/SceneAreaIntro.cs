using System.Collections;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>Hiện tên khu một lần sau khi scene gameplay load xong, bất kể player vào từ cửa nào.</summary>
    public class SceneAreaIntro : MonoBehaviour
    {
        [SerializeField] private string areaName = "";
        [SerializeField, Min(0f)] private float delay = 1.3f;

        private IEnumerator Start()
        {
            if (string.IsNullOrWhiteSpace(areaName)) yield break;
            yield return new WaitForSecondsRealtime(delay);
            while (SceneFader.IsTransitioning) yield return null;
            AreaNameBanner.Show(areaName);
        }
    }
}
