using System;
using System.Collections;
using UnityEngine;
using Rive;
using Rive.Components;

namespace Starfall.UI.Rive
{
    internal class SafeRiveLogger : global::Rive.IDebugLogger
    {
        private readonly global::Rive.IDebugLogger _inner;
        public SafeRiveLogger(global::Rive.IDebugLogger inner) { _inner = inner; }
        public void Log(string message) => _inner?.Log(message);
        public void LogWarning(string message) => _inner?.LogWarning(message);
        public void LogError(string message)
        {
            // Suppress benign missing property probes so console stays clean
            if (!string.IsNullOrEmpty(message) && message.Contains("Property not found"))
                return;
            _inner?.LogError(message);
        }
        public void LogException(System.Exception exception) => _inner?.LogException(exception);
    }

    /// <summary>
    /// One Rive graphic living on a UGUI canvas: RivePanel + RiveCanvasRenderer + RiveWidget,
    /// built at runtime from a .riv asset in Resources/Rive. Inputs are resolved once and cached.
    /// Every call is exception-safe so a missing native plugin or renamed input can never break gameplay;
    /// callers check <see cref="Usable"/> and fall back to plain UGUI.
    /// </summary>
    public class RiveHudElement
    {
        static RiveHudElement()
        {
            EnsureSafeLogger();
        }

        public static void EnsureSafeLogger()
        {
            if (global::Rive.Utils.DebugLogger.Instance != null && !(global::Rive.Utils.DebugLogger.Instance is SafeRiveLogger))
            {
                global::Rive.Utils.DebugLogger.Instance = new SafeRiveLogger(global::Rive.Utils.DebugLogger.Instance);
            }
        }

        public GameObject Root { get; private set; }
        public RectTransform Rect { get; private set; }
        public CanvasGroup Group { get; private set; }

        private RiveWidget widget;
        private Asset asset;
        private string artboardName;
        private string stateMachineName;
        private bool failed;

        // Cached input handles (resolved lazily after the widget finishes loading).
        private string cachedNumberName;
        private SMINumber smNumber;
        private ViewModelInstanceNumberProperty vmNumber;
        private bool numberResolved;

        /// <summary>False if the asset was missing or Rive failed to load it.</summary>
        public bool Usable
        {
            get
            {
                if (failed || widget == null || asset == null) return false;
                try { return widget.Status != WidgetStatus.Error; }
                catch { return false; }
            }
        }

        public static RiveHudElement Create(Transform canvas, string name, string resourcePath, string artboard = null, string stateMachine = null, Fit fit = Fit.Contain)
        {
            return Create(canvas, name, Resources.Load<Asset>(resourcePath), resourcePath, artboard, stateMachine, fit);
        }

        /// <summary>
        /// Loads a .riv from Resources without blocking the main thread (large files such as the
        /// 4.8 MB boss bar otherwise cause a visible hitch when a level starts).
        /// </summary>
        public static IEnumerator LoadAssetAsync(string resourcePath, Action<Asset> onLoaded)
        {
            var req = Resources.LoadAsync<Asset>(resourcePath);
            while (!req.isDone) yield return null;
            onLoaded?.Invoke(req.asset as Asset);
        }

        public static RiveHudElement Create(Transform canvas, string name, Asset loadedAsset, string sourceLabel, string artboard = null, string stateMachine = null, Fit fit = Fit.Contain)
        {
            var el = new RiveHudElement { artboardName = artboard, stateMachineName = stateMachine };
            try
            {
                el.asset = loadedAsset;
                if (el.asset == null)
                {
                    Debug.LogWarning($"[Rive] '{sourceLabel}' not found in Resources (or not imported as a Rive asset). Using UGUI fallback. Run STARFALL > Rive > Import & Verify Rive Assets.");
                    el.failed = true;
                    return el;
                }

                el.Root = new GameObject("Rive_" + name, typeof(RectTransform), typeof(CanvasGroup));
                el.Root.transform.SetParent(canvas, false);
                el.Rect = el.Root.GetComponent<RectTransform>();
                el.Group = el.Root.GetComponent<CanvasGroup>();
                el.Group.blocksRaycasts = false;
                el.Group.interactable = false;

                el.Root.AddComponent<RivePanel>();
                el.Root.AddComponent<RiveCanvasRenderer>();

                var widgetGo = new GameObject("Widget", typeof(RectTransform));
                widgetGo.transform.SetParent(el.Root.transform, false);
                var wr = widgetGo.GetComponent<RectTransform>();
                wr.anchorMin = Vector2.zero;
                wr.anchorMax = Vector2.one;
                wr.offsetMin = Vector2.zero;
                wr.offsetMax = Vector2.zero;

                el.widget = widgetGo.AddComponent<RiveWidget>();
                el.widget.Fit = fit;
                el.Reload();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rive] Could not create '{name}': {e.Message}. Using UGUI fallback.");
                el.failed = true;
                if (el.Root != null) UnityEngine.Object.Destroy(el.Root);
                el.Root = null;
            }
            return el;
        }

        public RiveHudElement Place(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            if (Rect == null) return this;
            Rect.anchorMin = anchorMin;
            Rect.anchorMax = anchorMax;
            Rect.pivot = pivot;
            Rect.anchoredPosition = anchoredPos;
            Rect.sizeDelta = size;
            return this;
        }

        /// <summary>Reloads the graphic, restarting one-shot animations from the beginning.</summary>
        public void Reload()
        {
            if (failed || widget == null) return;
            try
            {
                if (!string.IsNullOrEmpty(artboardName) || !string.IsNullOrEmpty(stateMachineName))
                    widget.Load(asset, artboardName, stateMachineName);
                else
                    widget.Load(asset);
                numberResolved = false;
                smNumber = null;
                vmNumber = null;
                missingNumbers.Clear();
                missingTriggers.Clear();
                if (widget.Status == WidgetStatus.Error) failed = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rive] Load failed for '{Root?.name}': {e.Message}");
                failed = true;
            }
        }

        public void SetActive(bool active)
        {
            if (Root != null && Root.activeSelf != active) Root.SetActive(active);
        }

        public void SetAlpha(float a)
        {
            if (Group != null) Group.alpha = a;
        }

        /// <summary>Sets a numeric input: state-machine number first, then view-model number property.</summary>
        private readonly System.Collections.Generic.HashSet<string> missingNumbers = new System.Collections.Generic.HashSet<string>();

        public bool SetNumber(string inputName, float value)
        {
            // Skip properties we already know are absent — Rive logs a stack trace on every miss.
            if (!Usable || missingNumbers.Contains(inputName)) return false;
            try
            {
                if (!numberResolved || cachedNumberName != inputName)
                {
                    var sm = widget.StateMachine;
                    if (sm == null) return false; // not loaded yet; try again next call
                    cachedNumberName = inputName;
                    numberResolved = true;
                    smNumber = null;
                    vmNumber = null;

                    foreach (var input in sm.Inputs())
                    {
                        if (input != null && input.IsNumber && input.Name == inputName)
                        {
                            smNumber = sm.GetNumber(inputName);
                            break;
                        }
                    }
                    if (smNumber == null && sm.ViewModelInstance != null)
                    {
                        EnsureSafeLogger();
                        try
                        {
                            vmNumber = sm.ViewModelInstance.GetNumberProperty(inputName);
                        }
                        catch
                        {
                            vmNumber = null;
                        }
                    }

                    // Property not found in either path — remember so we don't retry every frame.
                    if (smNumber == null && vmNumber == null)
                    {
                        missingNumbers.Add(inputName);
                        return false;
                    }
                }

                if (smNumber != null) { smNumber.Value = value; return true; }
                if (vmNumber != null) { vmNumber.Value = value; return true; }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rive] SetNumber('{inputName}') failed: {e.Message}");
                failed = true;
            }
            return false;
        }

        /// <summary>Fires a trigger: state-machine trigger first, then view-model trigger property.</summary>
        private readonly System.Collections.Generic.HashSet<string> missingTriggers = new System.Collections.Generic.HashSet<string>();

        public bool Fire(string triggerName)
        {
            // Remember misses: Rive logs a warning (with a stack trace in the editor) on every failed lookup.
            if (!Usable || missingTriggers.Contains(triggerName)) return false;
            try
            {
                var sm = widget.StateMachine;
                if (sm == null) return false;
                foreach (var input in sm.Inputs())
                {
                    if (input != null && input.IsTrigger && input.Name == triggerName)
                    {
                        sm.GetTrigger(triggerName)?.Fire();
                        return true;
                    }
                }
                global::Rive.ViewModelInstanceTriggerProperty vmTrigger = null;
                if (sm.ViewModelInstance != null)
                {
                    EnsureSafeLogger();
                    try
                    {
                        vmTrigger = sm.ViewModelInstance.GetTriggerProperty(triggerName);
                    }
                    catch
                    {
                        vmTrigger = null;
                    }
                }
                if (vmTrigger != null) { vmTrigger.Trigger(); return true; }
                missingTriggers.Add(triggerName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Rive] Fire('{triggerName}') failed: {e.Message}");
            }
            return false;
        }
    }
}
