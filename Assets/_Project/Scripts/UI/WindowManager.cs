using System.Collections.Generic;
using UnityEngine;

namespace RPGAuto.UI
{
    /// <summary>열고 닫을 수 있는 UI 창</summary>
    public interface IGameWindow
    {
        bool IsOpen { get; }
        void SetOpen(bool open);
    }

    /// <summary>
    /// 열린 창을 연 순서대로 기억한다. Esc(Android 뒤로 가기)로 가장 나중에 연 창부터 닫는다.
    /// </summary>
    public static class WindowManager
    {
        private static readonly List<IGameWindow> s_open = new();

        public static int OpenCount
        {
            get
            {
                Prune();
                return s_open.Count;
            }
        }

        /// <summary>각 창의 SetOpen에서 호출</summary>
        public static void NotifyChanged(IGameWindow window, bool open)
        {
            s_open.Remove(window);
            if (open) s_open.Add(window);
        }

        /// <summary>가장 나중에 연 창을 닫는다. 닫은 창이 없으면 false</summary>
        public static bool CloseTop()
        {
            Prune();
            if (s_open.Count == 0) return false;
            var top = s_open[^1];
            top.SetOpen(false); // SetOpen → NotifyChanged로 목록에서 빠진다
            s_open.Remove(top);
            return true;
        }

        public static void CloseAll()
        {
            Prune();
            for (int i = s_open.Count - 1; i >= 0; i--) s_open[i].SetOpen(false);
            s_open.Clear();
        }

        /// <summary>파괴된 창, 다른 경로로 닫힌 창 정리</summary>
        private static void Prune()
        {
            for (int i = s_open.Count - 1; i >= 0; i--)
            {
                var w = s_open[i];
                if (w is Object o && o == null || !w.IsOpen) s_open.RemoveAt(i);
            }
        }

        // 도메인 리로드를 끈 상태에서 플레이 모드를 다시 시작해도 목록이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_open.Clear();
    }
}
