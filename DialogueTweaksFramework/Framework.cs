using BepInEx;
using BepInEx.Configuration;
using GK2.Framework;
using DialogueTweaks;
using UnityEngine;

namespace DialogueTweaksFramework
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(MainPlugin.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("ru.superman4eg.gk2.framework", BepInDependency.DependencyFlags.HardDependency)] // Твоя проверенная зависимость! [L1]
    public sealed class FrameworkBridgePlugin : BaseUnityPlugin
    {
        // Уникальный GUID моста, чтобы разорвать любые петли сортировки BepInEx [L1]
        public const string PluginGuid = "com.sondju.gk2.DialogueTweaks.framework";
        public const string PluginName = "Dialogue Tweaks";
        public const string PluginVersion = "1.5.2";

        private void Awake()
        {
            MainPlugin main = MainPlugin.Instance;
            if (main == null)
            {
                Logger.LogError("Main mod instance is unavailable.");
                return;
            }

            // Регистрируем мост во фреймворке, скармливая ему main.Config по официальному гайду [L1]
            FrameworkApi.RegisterMod(new FrameworkBridge(main), main.Config);
            Logger.LogInfo("Dialogue Tweaks - Framework Integration Bridge loaded successfully!");
        }

        private sealed class FrameworkBridge : Gk2ModBase
        {
            private readonly MainPlugin main;

            // Выставляем frameworkManagesEnabledState: false, чтобы тумблер в меню управлял оригинальным .cfg файлом [L1]
            private readonly Gk2ModMetadata metadata = new Gk2ModMetadata(
                PluginGuid, // Передаем GUID моста [L1]
                PluginName,
                "sondju",
                PluginVersion,
                "Optional GK2 Mod Framework integration for Dialogue Tweaks.",
                supportsRuntimeToggle: true,
                requiresKnownBuild: false,
                frameworkManagesEnabledState: false);

            internal FrameworkBridge(MainPlugin main) { this.main = main; }
            public override Gk2ModMetadata Metadata => metadata;
            public override System.Collections.Generic.IReadOnlyList<Gk2ModDependency> Dependencies => null;

            public override void OnRegister(Gk2ModContext context)
            {
                // Регистрируем ТУ ЖЕ САМУЮ секцию и ключ, чтобы фреймворк связал меню с конфигом автономного мода [L1]
                context.Settings.AddToggle(
                    "1. General",
                    "ModEnabled",
                    true,
                    "Enable Mod",
                    "Completely disables confirmation windows when buying inspirations.");
                
                context.Settings.AddKeybind(
                    "2. Controls",
                    "FirstDialogueHotkey",
                    new KeyboardShortcut(KeyCode.E),
                    "First Hotkey",
                    "First hotkey for skip.");

                context.Settings.AddKeybind(
                    "2. Controls",
                    "LeaveHotkey",
                    new KeyboardShortcut(KeyCode.Escape),
                    "Leave Hotkey",
                    "Hotkey for leave answers menu.");

                context.Settings.AddKeybind(
                    "2. Controls",
                    "TradeHotkey",
                    new KeyboardShortcut(KeyCode.T),
                    "Trade Hotkey",
                    "Hotkey for open trade window in answers menu.");

                context.Settings.AddToggle(
                    "3. Visual",
                    "DisplayHotkeys",
                    true,
                    "Display Hotkeys",
                    "Display hotkeys in dialogue answers.");

                context.Settings.AddToggle(
                    "3. Visual",
                    "JustifyText",
                    true,
                    "Justify Text",
                    "Use justified text alignment (stretches text to fit the block width, matching block heights but may cause wide spacing).");

                context.Settings.AddToggle(
                    "3. Visual",
                    "ColorizeHotkeys",
                    false,
                    "Colorize Hotkeys",
                    "Enable custom pastel colors for hotkey badges. If disabled, badges will use the default font color.");

                context.Settings.AddReadOnly(
                    "Status",
                    "Integration",
                    "Framework Integration",
                    "Shows whether the optional bridge is active.",
                    () => main != null ? "Active" : "Unavailable");
            }

            public override void OnEnable() { }
            public override void OnDisable() { }
            public override void OnGameStarted() { }
            public override void OnReturnedToMainMenu() { }
        }
    }
}
