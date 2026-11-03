using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using Common;
using HarmonyLib;
using UI;
using UI.MainScreen;
using UnityEngine;
using UnityEngine.UI;

namespace GraphicalTweaks
{
    [BepInPlugin(Id, "Sal's Graphical Tweaks", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "local.sevo.graphicaltweaks";
        internal static Plugin Instance;
        internal ShadowDistanceModel Model;
        private ConfigEntry<bool> enabledOverride;
        private ConfigEntry<float> distance;
        private ConfigEntry<int> decalPercent;
        private ConfigEntry<float> decalMultiplier;
        private ConfigEntry<int> sunResolutionChoice;
        private ConfigEntry<bool> optimiseEmptyPasses;
        private ConfigEntry<bool> optimiseAmbientOcclusion;
        private ConfigEntry<bool> skipOffscreenDecalProxyWork;
        private ConfigEntry<bool> glassSunlightOcclusion;
        private ConfigEntry<bool> seamlessGlass;
        private ConfigEntry<int> nearShadowChoice;
        private ShadowResolutionModel resolutionModel;
        private SunShadowResolutionController resolutionController;
        private NearShadowDetailModel nearShadowModel;
        private NearShadowDetailController nearShadowController;
        private GlassRenderingController glassController;
        private bool glassInstalled;
        private Harmony harmony;
        private bool synchronizing;
        private float nextCheck;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"Lifecycle Awake: enabled={enabled}, activeSelf={gameObject.activeSelf}, activeHierarchy={gameObject.activeInHierarchy}, object={gameObject.name}");
            Model = new ShadowDistanceModel(QualitySettings.shadowDistance);
            enabledOverride = Config.Bind("Shadows", "OverrideEnabled", false, "Use the custom shadow distance.");
            distance = Config.Bind("Shadows", "Distance", Model.Baseline, "Shadow range in world units.");
            bool restoreOverride = enabledOverride.Value;
            if (!Model.TrySet(distance.Value)) restoreOverride = false;
            if (!restoreOverride) Model.Reset();
            MenuOptions.Initialize(Model);
            resolutionModel = new ShadowResolutionModel();
            sunResolutionChoice = Config.Bind("Shadows", "SunResolutionMode", 0, "Sun map: 0=default, 1=512, 2=1024, 3=2048, 4=4096.");
            sunResolutionChoice.Value = ShadowResolutionModel.NormalizeSaved(sunResolutionChoice.Value);
            resolutionModel.TrySet(sunResolutionChoice.Value);
            resolutionController = new SunShadowResolutionController(resolutionModel);
            MenuOptions.SunResolution.Value = resolutionModel.Choice;
            MenuOptions.SunResolution.eventVariable.OnValueChanged += OnSunResolutionChanged;
            nearShadowModel = new NearShadowDetailModel();
            nearShadowChoice = Config.Bind("Shadows", "NearShadowDetailChoice", 0, "Sharp range: 0=default, 1=15, 2=30, 3=60, 4=100.");
            nearShadowChoice.Value = NearShadowDetailModel.NormalizeSaved(nearShadowChoice.Value);
            nearShadowModel.TrySet(nearShadowChoice.Value);
            nearShadowController = new NearShadowDetailController(nearShadowModel);
            MenuOptions.NearShadowDetail.Value = nearShadowModel.Choice;
            MenuOptions.NearShadowDetail.eventVariable.OnValueChanged += OnNearShadowDetailChanged;
            glassSunlightOcclusion = Config.Bind("Glass", "SunlightOcclusion", true, "Block direct glass glare using sunlight shadows.");
            seamlessGlass = Config.Bind("Glass", "SeamlessGlass", true, "Remove internal joins between touching borderless glass.");
            MenuOptions.GlassSunlightOcclusion.Value = glassSunlightOcclusion.Value;
            MenuOptions.SeamlessGlass.Value = seamlessGlass.Value;
            MenuOptions.GlassSunlightOcclusion.eventVariable.OnValueChanged += OnGlassSunlightOcclusionChanged;
            MenuOptions.SeamlessGlass.eventVariable.OnValueChanged += OnSeamlessGlassChanged;
            string bundlePath = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "graphicaltweaks-glass.bundle");
            glassController = new GlassRenderingController(Logger, bundlePath);
            optimiseEmptyPasses = Config.Bind("Optimizations", "SkipEmptyRenderPassDispatches", true, "Skip culling work for empty render passes.");
            RendererOptimization.Enabled = optimiseEmptyPasses.Value;
            MenuOptions.OptimizeRenderer.Value = optimiseEmptyPasses.Value;
            MenuOptions.OptimizeRenderer.eventVariable.OnValueChanged += OnRendererOptimizationChanged;
            optimiseAmbientOcclusion = Config.Bind("Optimizations", "RepairAmbientOcclusionLifecycle", true, "Prevent duplicate or stale AO passes.");
            MenuOptions.OptimizeAmbientOcclusion.Value = optimiseAmbientOcclusion.Value;
            MenuOptions.OptimizeAmbientOcclusion.eventVariable.OnValueChanged += OnAmbientOcclusionOptimizationChanged;
            skipOffscreenDecalProxyWork = Config.Bind("Optimizations", "SkipOffscreenDecalProxyWork", false, "Experimental camera-turn optimisation; off by default.");
            MenuOptions.OptimizeDecalCpu.Value = skipOffscreenDecalProxyWork.Value;
            MenuOptions.OptimizeDecalCpu.eventVariable.OnValueChanged += OnDecalProxyWorkFilteringChanged;
            ApplyDecalProxyWorkFiltering();
            MenuOptions.Slider.eventVariable.OnValueChanged += OnDistanceChanged;
            decalPercent = Config.Bind("Decals", "VisibilityPercent", 100, "Legacy percentage for older configurations.");
            decalMultiplier = Config.Bind("Decals", "VisibilityMultiplier", DecalRangeModel.NormalizeMultiplier(decalPercent.Value / 100f), "Decal visibility, 1.0–10.0x.");
            decalMultiplier.Value = DecalRangeModel.NormalizeMultiplier(decalMultiplier.Value);
            decalPercent.Value = Mathf.RoundToInt(decalMultiplier.Value * 100);
            MenuOptions.DecalSlider.Value = Mathf.RoundToInt(decalMultiplier.Value * 10);
            MenuOptions.DecalNumeric.Value = decalMultiplier.Value;
            MenuOptions.DecalSlider.eventVariable.OnValueChanged += OnDecalSliderChanged;
            MenuOptions.DecalNumeric.eventVariable.OnValueChanged += OnDecalNumericChanged;
            DecalVisibility.Request(decalPercent.Value);
            DecalProjection.Request(decalPercent.Value);
            harmony = new Harmony(Id);
            // Native ResourcesLoader.Awake uploads the shape/material/rotation
            // tables before plugin Start. Observe those successful uploads here;
            // asset loading and render interception still start in Start.
            glassController.InstallBootstrapObservers(harmony);
            Apply(false);
            Logger.LogInfo($"Graphical Tweaks loaded. baseline={Model.Baseline}, range=1..{Model.Maximum}, effective={Model.EffectiveDistance}, cascades={QualitySettings.shadowCascades}, resolution={QualitySettings.shadowResolution}, persistentDataPath={Application.persistentDataPath}");
        }

        private void Start()
        {
            try { DecalProjection.Install(harmony); }
            catch (Exception e) { Logger.LogError("Decal projection accessor unavailable: " + e); }
            harmony.PatchAll(typeof(Plugin).Assembly);
            try
            {
                AmbientOcclusionLifecycle.Install(harmony, message => Logger.LogInfo(message));
                ApplyAmbientOcclusionOptimization();
            }
            catch(Exception e) { Logger.LogWarning("Ambient occlusion optimisation unavailable; native rendering retained: "+e); }
            try
            {
                glassController.Install(harmony);
                glassInstalled = true;
                ApplyGlassOptions();
            }
            catch (Exception e) { Logger.LogError("Glass rendering unavailable; native rendering retained: " + e); }
            Logger.LogInfo("Menu patch installed in Start: " + Harmony.GetPatchInfo(AccessTools.Method(typeof(UI_GraphicOptions), "RebuildOptions")).Owners.Contains(Id));
        }

        private void OnEnable()
        {
            Logger.LogInfo("Lifecycle OnEnable");
            nearShadowController?.Apply();
            if (glassInstalled) ApplyGlassOptions();
            ApplyAmbientOcclusionOptimization();
            ApplyDecalProxyWorkFiltering();
        }
        private void OnDisable()
        {
            Logger.LogInfo("Lifecycle OnDisable");
            nearShadowController?.Restore();
            if (glassInstalled) glassController?.SetOptions(false, false);
            AmbientOcclusionLifecycle.SetEnabled(false);
            DecalVisibility.SetProxyWorkFiltering(false);
        }

        private void OnDistanceChanged()
        {
            if (synchronizing) return;
            if (Model.TrySet(MenuOptions.Slider.Value)) Apply(true);
            else Refresh();
        }

        internal void ResetShadow()
        {
            Model.Reset();
            Apply(true);
        }

        internal void LogDecal(string message) => Logger.LogInfo(message);
        internal int SunShadowResolutionChoice => resolutionModel.Choice;
        internal bool RendererOptimizationEnabled => RendererOptimization.Enabled;
        internal bool AmbientOcclusionOptimizationEnabled => optimiseAmbientOcclusion.Value;
        internal int NearShadowDetailChoice => nearShadowModel.Choice;
        internal bool GlassSunlightOcclusionEnabled => glassSunlightOcclusion.Value;
        internal bool SeamlessGlassEnabled => seamlessGlass.Value;
        private static Light CurrentSun => PositionManager.instance != null ? PositionManager.instance.sunLight : null;
        private void OnSunResolutionChanged() { if (!synchronizing) SetSunShadowResolutionChoice(MenuOptions.SunResolution.Value); }
        internal void SetSunShadowResolutionChoice(int choice)
        {
            if (!resolutionModel.TrySet(choice)) { RefreshQualityControls(); return; }
            sunResolutionChoice.Value = choice;
            resolutionController.Apply(CurrentSun);
            Config.Save();
            Logger.LogInfo($"Sun shadow resolution applied: choice={choice}, light={CurrentSun?.shadowResolution}, custom={CurrentSun?.shadowCustomResolution}, quality={QualitySettings.shadowResolution}");
            RefreshQualityControls();
        }
        private void OnRendererOptimizationChanged() { if (!synchronizing) SetRendererOptimization(MenuOptions.OptimizeRenderer.Value); }
        internal void SetRendererOptimization(bool enabled)
        {
            optimiseEmptyPasses.Value = enabled;
            RendererOptimization.Enabled = enabled;
            Config.Save();
            Logger.LogInfo("Renderer empty-pass optimisation: " + enabled);
            RefreshQualityControls();
        }
        private void OnNearShadowDetailChanged() { if (!synchronizing) SetNearShadowDetailChoice(MenuOptions.NearShadowDetail.Value); }
        private void OnAmbientOcclusionOptimizationChanged() { if (!synchronizing) SetAmbientOcclusionOptimization(MenuOptions.OptimizeAmbientOcclusion.Value); }
        internal void SetAmbientOcclusionOptimization(bool value)
        {
            optimiseAmbientOcclusion.Value = value;
            Config.Save();
            ApplyAmbientOcclusionOptimization();
            RefreshQualityControls();
        }
        private void ApplyAmbientOcclusionOptimization()
        {
            if (AmbientOcclusionLifecycle.Installed)
                AmbientOcclusionLifecycle.SetEnabled(isActiveAndEnabled && optimiseAmbientOcclusion != null && optimiseAmbientOcclusion.Value);
        }
        private void OnDecalProxyWorkFilteringChanged() { if (!synchronizing) SetDecalProxyWorkFiltering(MenuOptions.OptimizeDecalCpu.Value); }
        internal void SetDecalProxyWorkFiltering(bool value)
        {
            skipOffscreenDecalProxyWork.Value = value;
            Config.Save();
            ApplyDecalProxyWorkFiltering();
            RefreshQualityControls();
        }
        private void ApplyDecalProxyWorkFiltering()
        {
            DecalVisibility.SetProxyWorkFiltering(isActiveAndEnabled && skipOffscreenDecalProxyWork != null && skipOffscreenDecalProxyWork.Value);
        }
        internal void SetNearShadowDetailChoice(int choice)
        {
            if (!nearShadowModel.TrySet(choice)) { RefreshQualityControls(); return; }
            if (isActiveAndEnabled) nearShadowController.Apply();
            else nearShadowController.Restore();
            nearShadowChoice.Value = choice;
            Config.Save();
            Logger.LogInfo("Near shadow detail applied: " + nearShadowController.DescribeCurrent());
            RefreshQualityControls();
        }
        private void OnGlassSunlightOcclusionChanged() { if (!synchronizing) SetGlassSunlightOcclusion(MenuOptions.GlassSunlightOcclusion.Value); }
        private void OnSeamlessGlassChanged() { if (!synchronizing) SetSeamlessGlass(MenuOptions.SeamlessGlass.Value); }
        internal void SetGlassSunlightOcclusion(bool enabled)
        {
            glassSunlightOcclusion.Value = enabled;
            Config.Save();ApplyGlassOptions();RefreshQualityControls();
            Logger.LogInfo("Glass sunlight occlusion requested: " + enabled);
        }
        internal void SetSeamlessGlass(bool enabled)
        {
            seamlessGlass.Value = enabled;
            Config.Save();ApplyGlassOptions();RefreshQualityControls();
            Logger.LogInfo("Seamless glass requested: " + enabled);
        }
        private void ApplyGlassOptions()
        {
            if (!glassInstalled) return;
            if (!isActiveAndEnabled) { glassController.SetOptions(false, false); return; }
            glassController.SetOptions(glassSunlightOcclusion.Value, seamlessGlass.Value);
            glassController.Apply();
        }
        private void RefreshQualityControls()
        {
            synchronizing = true;
            try
            {
                MenuOptions.SunResolution.Value = resolutionModel.Choice;
                MenuOptions.OptimizeRenderer.Value = RendererOptimization.Enabled;
                MenuOptions.OptimizeAmbientOcclusion.Value = optimiseAmbientOcclusion.Value;
                MenuOptions.OptimizeDecalCpu.Value = skipOffscreenDecalProxyWork.Value;
                MenuOptions.NearShadowDetail.Value = nearShadowModel.Choice;
                MenuOptions.GlassSunlightOcclusion.Value = glassSunlightOcclusion.Value;
                MenuOptions.SeamlessGlass.Value = seamlessGlass.Value;
                // Native checkbox UI does not refresh its image in OnValueChanged.
                if (MenuOptions.OptimizeRenderer.uiInstance != null) MenuOptions.OptimizeRenderer.uiInstance.SetGenericOption(MenuOptions.OptimizeRenderer);
                if (MenuOptions.OptimizeAmbientOcclusion.uiInstance != null) MenuOptions.OptimizeAmbientOcclusion.uiInstance.SetGenericOption(MenuOptions.OptimizeAmbientOcclusion);
                if (MenuOptions.OptimizeDecalCpu.uiInstance != null) MenuOptions.OptimizeDecalCpu.uiInstance.SetGenericOption(MenuOptions.OptimizeDecalCpu);
                if (MenuOptions.GlassSunlightOcclusion.uiInstance != null) MenuOptions.GlassSunlightOcclusion.uiInstance.SetGenericOption(MenuOptions.GlassSunlightOcclusion);
                if (MenuOptions.SeamlessGlass.uiInstance != null) MenuOptions.SeamlessGlass.uiInstance.SetGenericOption(MenuOptions.SeamlessGlass);
            }
            finally { synchronizing = false; }
        }

        private void OnDecalSliderChanged() { if (!synchronizing) SetDecalMultiplier(MenuOptions.DecalSlider.Value / 10f); }
        private void OnDecalNumericChanged() { if (!synchronizing) SetDecalMultiplier(MenuOptions.DecalNumeric.Value); }
        internal void ResetDecals() => SetDecalMultiplier(1f);
        private void SetDecalMultiplier(float value)
        {
            var model = new DecalRangeModel();
            if (!model.TrySetMultiplier(value)) { RefreshDecals(); return; }
            int percent = model.Percent;
            decalMultiplier.Value = model.Multiplier;
            decalPercent.Value = percent;
            Config.Save();
            DecalVisibility.Request(percent);
            DecalProjection.Request(percent);
            Logger.LogInfo($"Decal visibility requested: {model.Multiplier:F1}x (queued for native upload phase)");
            RefreshDecals();
        }

        internal void CommitDecalInput(string text)
        {
            if (float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value) && !float.IsInfinity(value) && value >= 1 && value <= 10)
                SetDecalMultiplier(value);
            else Logger.LogWarning("Decal visibility rejected: enter a multiplier from 1.0 to 10.0.");
            RefreshDecals();
        }

        private void RefreshDecals()
        {
            synchronizing = true;
            try
            {
                MenuOptions.DecalSlider.Value = Mathf.RoundToInt(decalMultiplier.Value * 10);
                MenuOptions.DecalNumeric.Value = decalMultiplier.Value;
                if (MenuOptions.DecalNumeric.uiInstance != null) MenuOptions.DecalNumeric.uiInstance.SetGenericOption(MenuOptions.DecalNumeric);
            }
            finally { synchronizing = false; }
        }

        internal void CommitInput(string text)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && Model.TrySet(value)) Apply(true);
            else { Logger.LogWarning($"Shadow distance rejected: enter an integer from 1 to {Model.Maximum}."); Refresh(); }
        }

        private void Refresh()
        {
            synchronizing = true;
            try
            {
                MenuOptions.Slider.Value = Mathf.Clamp(Mathf.RoundToInt(Model.Enabled ? Model.SelectedDistance : Model.Baseline), 1, Model.Maximum);
                if (MenuOptions.Numeric.uiInstance != null) MenuOptions.Numeric.uiInstance.SetGenericOption(MenuOptions.Numeric);
            }
            finally { synchronizing = false; }
        }

        private void Apply(bool persist)
        {
            QualitySettings.shadowDistance = Model.EffectiveDistance;
            if (isActiveAndEnabled) nearShadowController?.Apply();
            else nearShadowController?.Restore();
            if (persist)
            {
                enabledOverride.Value = Model.Enabled;
                distance.Value = Model.SelectedDistance;
                Config.Save();
                Logger.LogInfo($"Shadow distance applied: override={Model.Enabled}, requested={Model.EffectiveDistance}, actual={QualitySettings.shadowDistance}");
            }
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            resolutionController?.Apply(CurrentSun);
            if (Model.Enabled && !Mathf.Approximately(QualitySettings.shadowDistance, Model.EffectiveDistance)) Apply(false);
            nearShadowController?.Apply();
            if (glassInstalled) glassController?.Apply();
        }

        private void OnDestroy()
        {
            Logger.LogInfo("Lifecycle OnDestroy: restoring baseline and removing patches");
            if (Model != null) QualitySettings.shadowDistance = Model.Baseline;
            try { nearShadowController?.Dispose(); }
            catch (Exception e) { Logger.LogError("Near shadow restoration failed: " + e); }
            try { glassController?.Dispose(); }
            catch (Exception e) { Logger.LogError("Glass teardown failed; continuing existing setting restoration: " + e); }
            resolutionController?.Restore();
            AmbientOcclusionLifecycle.Dispose();
            RendererOptimization.Enabled = false;
            MenuOptions.Slider.eventVariable.OnValueChanged -= OnDistanceChanged;
            MenuOptions.DecalSlider.eventVariable.OnValueChanged -= OnDecalSliderChanged;
            MenuOptions.DecalNumeric.eventVariable.OnValueChanged -= OnDecalNumericChanged;
            MenuOptions.SunResolution.eventVariable.OnValueChanged -= OnSunResolutionChanged;
            MenuOptions.OptimizeRenderer.eventVariable.OnValueChanged -= OnRendererOptimizationChanged;
            MenuOptions.OptimizeAmbientOcclusion.eventVariable.OnValueChanged -= OnAmbientOcclusionOptimizationChanged;
            MenuOptions.OptimizeDecalCpu.eventVariable.OnValueChanged -= OnDecalProxyWorkFilteringChanged;
            MenuOptions.NearShadowDetail.eventVariable.OnValueChanged -= OnNearShadowDetailChanged;
            MenuOptions.GlassSunlightOcclusion.eventVariable.OnValueChanged -= OnGlassSunlightOcclusionChanged;
            MenuOptions.SeamlessGlass.eventVariable.OnValueChanged -= OnSeamlessGlassChanged;
            DecalVisibility.Shutdown();
            DecalProjection.Shutdown();
            harmony?.UnpatchSelf();
            Instance = null;
        }
    }

    internal static class MenuOptions
    {
        public static readonly GenericOption<int> Slider = new GenericOption<int> { eventVariable = new IntEventVariable(), label = "Shadow distance", tooltip = "Longer range can reduce nearby detail and FPS.", useSlider = true, data1 = 1, useDefaultValue = true };
        public static readonly GenericOption<int> Numeric = new GenericOption<int> { label = "Shadow distance (number)", tooltip = "", useDefaultValue = true };
        public static readonly GenericOption<int> SunResolution = new GenericOption<int> { eventVariable = new IntEventVariable(), label = "Sun shadow resolution", tooltip = "Lower values can improve FPS; shadows lose detail.", useDropdown = true, data1 = 0, data2 = 4, dropdownList = new List<string> { "Game default", "512 px", "1024 px", "2048 px", "4096 px" }, useDefaultValue = true, defaultValue = 0 };
        public static readonly GenericOption<bool> OptimizeRenderer = new GenericOption<bool> { eventVariable = new BoolEventVariable(), label = "Renderer optimisation", tooltip = "Skips culling work for empty passes.", isCheckbox = true, useDefaultValue = true, defaultValue = true };
        public static readonly GenericOption<bool> OptimizeAmbientOcclusion = new GenericOption<bool> { eventVariable = new BoolEventVariable(), label = "Optimize ambient occlusion", tooltip = "Prevents duplicate AO passes.", isCheckbox = true, useDefaultValue = true, defaultValue = true };
        public static readonly GenericOption<bool> OptimizeDecalCpu = new GenericOption<bool> { eventVariable = new BoolEventVariable(), label = "Decal CPU optimisation (experimental)", tooltip = "May improve FPS while turning. Can increase idle CPU use.", isCheckbox = true, useDefaultValue = true, defaultValue = false };
        public static readonly GenericOption<int> NearShadowDetail = new GenericOption<int> { eventVariable = new IntEventVariable(), label = "Sharp shadow range", tooltip = "Smaller values sharpen a smaller nearby area.", useDropdown = true, data1 = 0, data2 = 4, dropdownList = new List<string> { "Game default", "15 units", "30 units", "60 units", "100 units" }, useDefaultValue = true, defaultValue = 0 };
        public static readonly GenericOption<bool> GlassSunlightOcclusion = new GenericOption<bool> { eventVariable = new BoolEventVariable(), label = "Glass sunlight occlusion", tooltip = "Uses sunlight shadows to block glare.", isCheckbox = true, useDefaultValue = true, defaultValue = true };
        public static readonly GenericOption<bool> SeamlessGlass = new GenericOption<bool> { eventVariable = new BoolEventVariable(), label = "Seamless glass", tooltip = "Removes internal joins between touching borderless glass.", isCheckbox = true, useDefaultValue = true, defaultValue = true };
        public static readonly GenericOption<int> DecalSlider = new GenericOption<int> { eventVariable = new IntEventVariable(), label = "Decal visibility multiplier", tooltip = "1x is the game default. Higher values can reduce FPS.", useSlider = true, data1 = 10, data2 = 100, useDefaultValue = true, defaultValue = 10 };
        public static readonly GenericOption<float> DecalNumeric = new GenericOption<float> { eventVariable = new FloatEventVariable(), label = "Decal multiplier (number)", tooltip = "1.0–10.0x.", useDefaultValue = true, defaultValue = 1f };
        internal static void Initialize(ShadowDistanceModel model)
        {
            Slider.data2 = model.Maximum;
            Slider.defaultValue = Mathf.Clamp(Mathf.RoundToInt(model.Baseline), 1, model.Maximum);
            Numeric.defaultValue = Slider.defaultValue;
            Numeric.eventVariable = Slider.eventVariable;
        }
    }

    [HarmonyPatch(typeof(UI_GraphicOptions), "RebuildOptions")]
    internal static class MenuPatch
    {
        private static void Postfix(UI_GraphicOptions __instance, RectTransform ___optionParent)
        {
            Debug.Log("Graphical Tweaks: graphics menu rebuilt; plugin alive=" + (Plugin.Instance != null));
            if (Plugin.Instance == null) return;
            try
            {
                var templateData = (UITemplateData)AccessTools.Field(__instance.GetType(), "templateData").GetValue(__instance);
                float offset = ___optionParent.sizeDelta.y - 16f;
                Add(MenuOptions.Slider, ___optionParent, templateData, ref offset);
                Add(MenuOptions.Numeric, ___optionParent, templateData, ref offset);
                Add(MenuOptions.SunResolution, ___optionParent, templateData, ref offset);
                Add(MenuOptions.NearShadowDetail, ___optionParent, templateData, ref offset);
                Add(MenuOptions.OptimizeRenderer, ___optionParent, templateData, ref offset);
                Add(MenuOptions.OptimizeAmbientOcclusion, ___optionParent, templateData, ref offset);
                Add(MenuOptions.OptimizeDecalCpu, ___optionParent, templateData, ref offset);
                Add(MenuOptions.GlassSunlightOcclusion, ___optionParent, templateData, ref offset);
                Add(MenuOptions.SeamlessGlass, ___optionParent, templateData, ref offset);
                Add(MenuOptions.DecalSlider, ___optionParent, templateData, ref offset);
                Add(MenuOptions.DecalNumeric, ___optionParent, templateData, ref offset);
                GenericOptionCommon.FitRectToContent(offset, ___optionParent);
                Debug.Log("Graphical Tweaks: added 4 shadow, 3 optimisation, 2 glass and 2 multiplier controls");
            }
            catch (Exception e) { Debug.LogError("Graphical Tweaks menu injection failed: " + e); }
        }

        private static void Add<T>(GenericOption<T> option, Transform parent, UITemplateData templateData, ref float offset)
        {
            // Each SEVO rebuild destroys the old rows before calling SetupClassGenericOptions.
            option.translationId = "GraphicalTweaks." + option.label;
            var row = GenericOptionCommon.GetTemplateGo(option, templateData, parent);
            row.name = "GraphicalTweaks." + option.label;
            GenericOptionCommon.SetupGenericUI(row, ref offset, option, null);
            // SEVO assigns uiInstance only AFTER its first formatting callback.
            // Apply multiplier formatting once more after that assignment.
            if (ReferenceEquals(option, MenuOptions.DecalSlider)) MultiplierSliderFormatPatch.Format(MenuOptions.DecalSlider.uiInstance as UI_OptionSliderInt);
            if (ReferenceEquals(option, MenuOptions.DecalNumeric)) MultiplierNumberFormatPatch.Format(MenuOptions.DecalNumeric.uiInstance as UI_OptionFloat);
            if (ReferenceEquals(option, MenuOptions.Slider) || ReferenceEquals(option, MenuOptions.Numeric))
                BindReset(option.uiInstance, Plugin.Instance.ResetShadow);
            if (ReferenceEquals(option, MenuOptions.DecalSlider) || ReferenceEquals(option, MenuOptions.DecalNumeric))
                BindReset(option.uiInstance, Plugin.Instance.ResetDecals);
            if (ReferenceEquals(option, MenuOptions.SunResolution)) BindReset(option.uiInstance, () => Plugin.Instance.SetSunShadowResolutionChoice(0));
            if (ReferenceEquals(option, MenuOptions.OptimizeRenderer)) BindReset(option.uiInstance, () => Plugin.Instance.SetRendererOptimization(true));
            if (ReferenceEquals(option, MenuOptions.OptimizeAmbientOcclusion)) BindReset(option.uiInstance, () => Plugin.Instance.SetAmbientOcclusionOptimization(true));
            if (ReferenceEquals(option, MenuOptions.OptimizeDecalCpu)) BindReset(option.uiInstance, () => Plugin.Instance.SetDecalProxyWorkFiltering(false));
            if (ReferenceEquals(option, MenuOptions.NearShadowDetail)) BindReset(option.uiInstance, () => Plugin.Instance.SetNearShadowDetailChoice(0));
            if (ReferenceEquals(option, MenuOptions.GlassSunlightOcclusion)) BindReset(option.uiInstance, () => Plugin.Instance.SetGlassSunlightOcclusion(true));
            if (ReferenceEquals(option, MenuOptions.SeamlessGlass)) BindReset(option.uiInstance, () => Plugin.Instance.SetSeamlessGlass(true));
        }

        private static void BindReset(Component widget, Action resetAction)
        {
            var reset = (GameObject)AccessTools.Field(widget.GetType(), "resetGo").GetValue(widget);
            var button = reset.GetComponent<Button>() ?? reset.GetComponentInChildren<Button>(true) ?? reset.GetComponentInParent<Button>();
            if (button != null)
            {
                // Only newly cloned mod rows are rebound. Reset also works
                // when the displayed number has not changed.
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => resetAction());
                button.enabled = true;
                button.interactable = true;
                if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
            }
            else Debug.LogWarning("Graphical Tweaks: reset button missing from native template.");
        }
    }

    [HarmonyPatch(typeof(UI_OptionInt), nameof(UI_OptionInt.OnNewInput))]
    internal static class NumericPatch
    {
        private static bool Prefix(UI_OptionInt __instance)
        {
            if (Plugin.Instance == null) return true;
            bool shadow = __instance == MenuOptions.Numeric.uiInstance;
            if (!shadow) return true;
            var input = (InputField)AccessTools.Field(typeof(UI_OptionInt), "input").GetValue(__instance);
            Plugin.Instance.CommitInput(input.text);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_OptionFloat), nameof(UI_OptionFloat.OnNewInput))]
    internal static class MultiplierInputPatch
    {
        private static readonly System.Reflection.FieldInfo inputField = AccessTools.Field(typeof(UI_OptionFloat), "input");
        private static bool Prefix(UI_OptionFloat __instance)
        {
            if (Plugin.Instance == null || __instance != MenuOptions.DecalNumeric.uiInstance) return true;
            Plugin.Instance.CommitDecalInput(((InputField)inputField.GetValue(__instance)).text);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_OptionFloat), "OnValueChanged")]
    internal static class MultiplierNumberFormatPatch
    {
        private static readonly System.Reflection.FieldInfo inputField = AccessTools.Field(typeof(UI_OptionFloat), "input");
        private static void Postfix(UI_OptionFloat __instance)
            => Format(__instance);
        internal static void Format(UI_OptionFloat __instance)
        {
            if (__instance != null && __instance == MenuOptions.DecalNumeric.uiInstance)
                ((InputField)inputField.GetValue(__instance)).text = MenuOptions.DecalNumeric.Value.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }

    [HarmonyPatch]
    internal static class MultiplierSliderFormatPatch
    {
        private static readonly System.Reflection.FieldInfo textField = AccessTools.Field(typeof(UI_OptionSliderInt), "text");
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(UI_OptionSliderInt), "OnValueChanged");
            yield return AccessTools.Method(typeof(UI_OptionSliderInt), nameof(UI_OptionSliderInt.OnNewDataInput));
        }
        private static void Postfix(UI_OptionSliderInt __instance)
            => Format(__instance);
        internal static void Format(UI_OptionSliderInt __instance)
        {
            if (__instance != null && __instance == MenuOptions.DecalSlider.uiInstance)
                ((Text)textField.GetValue(__instance)).text = (MenuOptions.DecalSlider.Value / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "×";
        }
    }
}
