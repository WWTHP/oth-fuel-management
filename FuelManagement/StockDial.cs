using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FuelManagement
{
    /// <summary>
    /// The fuel dial built from the game's own tilt dial, so it matches the stock HUD exactly.
    ///
    /// The right-hand <c>ContextUIInclineIndicator</c> (parts seen in the 0.2.0 log:
    /// Mask/{BlurredBackground, RotatingPart/{Terrain, Sky, GroundLine, Car}, Border, Lines}) is
    /// cloned next to it. The clone's indicator component is removed before it ever wakes (the
    /// clone is made under an inactive holder), so the game never moves it; the horizon stays
    /// level and the car icon is hidden. Added on top: a needle (E left → F right over the top),
    /// a "FUEL" caption where the car was, and the km/house readout in the grey band, in the
    /// game's own TextMeshPro font. The clone shows and hides with the stock dial.
    /// If anything is missing it gives up and the IMGUI dial (<see cref="FuelDial"/>) is used.
    /// </summary>
    internal static class StockDial
    {
        private const string Stage = "dial";
        private static readonly Color Ink = new(0.08f, 0.08f, 0.09f, 0.95f);
        private static readonly Color Amber = new(1f, 0.72f, 0.3f, 1f);
        private static readonly Color Red = new(1f, 0.3f, 0.24f, 1f);

        public static bool Ready => _root != null && !_root.WasCollected && !_failed;

        private static GameObject _root;
        private static RectTransform _rootRt, _needle;
        private static Image _needleImg;
        private static TextMeshProUGUI _caption, _km, _info, _status;
        private static string _lastStatus;
        private static ContextUIInclineIndicator _source, _left;
        private static CanvasGroup _srcGroup, _group;
        private static bool _failed, _layoutDriven;
        private static float _retryAt;
        private static string _lastKm, _lastInfo, _lastCaption;

        /// <summary>Call every frame. <paramref name="show"/>: the dial gauge is wanted right now.</summary>
        public static void Update(bool show)
        {
            if (_failed) return;
            if (!show)
            {
                if (Ready && _root.activeSelf) _root.SetActive(false);
                return;
            }
            if (!Ready || _source == null || _source.WasCollected)
            {
                if (Time.unscaledTime < _retryAt) return;
                _retryAt = Time.unscaledTime + 2f;
                Build();
                if (!Ready) return;
            }

            bool visible = _source.gameObject.activeInHierarchy;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;
            if (_srcGroup != null && _group != null) _group.alpha = _srcGroup.alpha;
            Position();
            Refresh();
        }

        // ------------------------------------------------------------- build

        private static void Build()
        {
            Destroy();
            var found = HudAnchor.Indicators;
            if (found.Count == 0) return; // stock dials hidden or not loaded yet; retry later
            if (!CrashGuard.Enter(Stage)) { Fail("disabled after a crash"); return; }
            GameObject holder = null;
            try
            {
                _source = found[0];
                _left = found[0];
                foreach (var i in found)
                {
                    if (i.transform.position.x > _source.transform.position.x) _source = i;
                    if (i.transform.position.x < _left.transform.position.x) _left = i;
                }

                // Clone under an inactive holder: nothing on the clone wakes up before we've
                // removed the game's indicator logic from it.
                holder = new GameObject("FuelDialHolder");
                holder.SetActive(false);
                _root = Object.Instantiate(_source.gameObject, holder.transform, false);
                _root.name = "FuelIndicator";
                foreach (var c in _root.GetComponentsInChildren<ContextUIInclineIndicator>(true)) Object.DestroyImmediate(c);

                var mask = _root.transform.Find("Mask");
                var rotating = mask != null ? mask.Find("RotatingPart") : null;
                if (mask == null || rotating == null) throw new System.InvalidOperationException("dial has no Mask/RotatingPart");
                rotating.localRotation = Quaternion.identity;
                var car = rotating.Find("Car");
                var carRt = car != null ? car.GetComponent<RectTransform>() : null;
                if (car != null) car.gameObject.SetActive(false);

                var font = FindFont(_source);
                var maskRt = mask.GetComponent<RectTransform>();
                float w = maskRt.rect.width > 0f ? maskRt.rect.width : 140f;

                // Pivot at the rotating part's centre (the circle's centre / horizon).
                var pivot = NewRect("FuelPivot", mask);
                var rotRt = rotating.GetComponent<RectTransform>();
                pivot.anchorMin = rotRt.anchorMin;
                pivot.anchorMax = rotRt.anchorMax;
                pivot.pivot = new Vector2(0.5f, 0.5f);
                pivot.anchoredPosition = rotRt.anchoredPosition;
                pivot.sizeDelta = Vector2.zero;

                _needle = NewRect("FuelNeedle", pivot);
                _needle.anchorMin = _needle.anchorMax = new Vector2(0.5f, 0.5f);
                _needle.pivot = new Vector2(0.5f, 0f);
                _needle.anchoredPosition = Vector2.zero;
                _needle.sizeDelta = new Vector2(Mathf.Max(2f, w * 0.022f), w * 0.42f);
                _needleImg = _needle.gameObject.AddComponent<Image>();
                _needleImg.raycastTarget = false;

                var captionPos = carRt != null ? carRt.anchoredPosition : new Vector2(0f, w * 0.2f);
                _caption = NewText("FuelCaption", pivot, font, w * 0.1f, Color.white, captionPos, w);
                _km = NewText("FuelKm", pivot, font, w * 0.12f, Ink, new Vector2(0f, -w * 0.15f), w);
                _info = NewText("FuelInfo", pivot, font, w * 0.085f, Ink, new Vector2(0f, -w * 0.27f), w);
                // Siphon messages can be long: above the dial, outside the mask.
                float h = maskRt.rect.height > 0f ? maskRt.rect.height : 120f;
                _status = NewText("FuelStatus", _root.transform, font, w * 0.1f, Color.white, new Vector2(0f, h * 0.5f + w * 0.1f), w * 3f);
                _caption.fontStyle = FontStyles.Bold;
                _km.fontStyle = FontStyles.Bold;

                // Next to the stock dial, then live.
                var parent = _source.transform.parent;
                _root.transform.SetParent(parent, false);
                _root.transform.SetSiblingIndex(_source.transform.GetSiblingIndex() + 1);
                _rootRt = _root.GetComponent<RectTransform>();
                _layoutDriven = parent != null && parent.GetComponent<LayoutGroup>() != null;
                _srcGroup = _source.GetComponent<CanvasGroup>();
                _group = _root.GetComponent<CanvasGroup>();
                _lastKm = _lastInfo = _lastCaption = _lastStatus = null;
                Position();
                Refresh();

                var comps = new List<string>();
                foreach (var mb in _root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null) comps.Add(mb.GetIl2CppType().Name);
                Mod.Log.Msg($"Fuel dial cloned from {_source.gameObject.name} (font: {(font != null ? font.name : "TMP default")}, " +
                            $"layout group: {_layoutDriven}). Components: {string.Join(", ", comps)}.");
            }
            catch (System.Exception ex)
            {
                Destroy();
                Fail(ex.Message);
            }
            finally
            {
                if (holder != null) Object.Destroy(holder);
                CrashGuard.Exit(Stage);
            }
        }

        private static void Fail(string why)
        {
            _failed = true;
            Mod.Log.Warning($"Couldn't build the fuel dial from the game's tilt dial ({why}); using the drawn dial.");
        }

        private static void Destroy()
        {
            if (_root != null && !_root.WasCollected) Object.Destroy(_root);
            _root = null;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color,
            Vector2 pos, float width)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width * 0.9f, size * 1.4f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>The game's HUD font: from the RPM display if possible, else any HUD text on the same canvas.</summary>
        private static TMP_FontAsset FindFont(Component near)
        {
            foreach (var rpm in Object.FindObjectsOfType<ContextUIEngineRPMDisplay>())
            {
                var t = rpm != null ? rpm.GetComponentInChildren<TMP_Text>(true) : null;
                if (t != null && t.font != null) return t.font;
            }
            var canvas = near.GetComponentInParent<Canvas>();
            if (canvas != null)
                foreach (var t in canvas.rootCanvas.GetComponentsInChildren<TMP_Text>(true))
                    if (t != null && t.font != null) return t.font;
            return null;
        }

        // ------------------------------------------------------------ update

        private static void Position()
        {
            if (_layoutDriven || _rootRt == null) return;
            var s = _source.transform.position;
            var step = _left != null && !_left.WasCollected && _left != _source
                ? s - _left.transform.position
                : new Vector3(_source.GetComponent<RectTransform>().rect.width * 1.1f * _source.transform.lossyScale.x, 0f, 0f);
            _root.transform.position = s + step;
            _rootRt.anchoredPosition += new Vector2(Settings.HudOffsetX.Value, Settings.HudOffsetY.Value);
        }

        private static void Refresh()
        {
            float level = Fuel.Tank.Level;
            _needle.localEulerAngles = new Vector3(0f, 0f, -HudLayout.NeedleAngle(level)); // UI z is counter-clockwise
            _needleImg.color = Limp.Active || level <= 0.1f ? Red : level <= 0.25f ? Amber : Color.white;

            string caption = Car.ExtraTanks > 0 ? $"FUEL +{Car.ExtraTanks}" : "FUEL";
            string km = $"{Fuel.Tank.RemainingM(Fuel.RangeM) / 1000f:0.0} km";
            string info = Limp.Active ? "<color=#b02010>LIMP</color>"
                : Fuel.Refuelling ? "refuelling"
                : Siphon.Receiving ? "siphoning"
                : Siphon.BeingDrained ? "being siphoned"
                : float.IsInfinity(Fuel.NearestHouseM) ? "house ?" : $"house {Fuel.NearestHouseM / 1000f:0.0} km";
            Set(_caption, caption, ref _lastCaption);
            Set(_km, km, ref _lastKm);
            Set(_info, info, ref _lastInfo);
            Set(_status, Siphon.Status, ref _lastStatus);
        }

        private static void Set(TextMeshProUGUI t, string s, ref string last)
        {
            if (s == last) return;
            last = s;
            t.text = s;
        }
    }
}
