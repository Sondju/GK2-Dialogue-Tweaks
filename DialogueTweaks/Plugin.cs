using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DialogueTweaks
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MainPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.sondju.gk2.DialogueTweaks";
        public const string PluginName = "Dialogue Tweaks";
        public const string PluginVersion = "1.5.2";

        public static MainPlugin Instance { get; private set; }
        public static ManualLogSource Log;

        public ConfigEntry<bool> ModEnabled { get; private set; }
        public ConfigEntry<bool> DisplayHotkeys { get; private set; }
        public ConfigEntry<KeyboardShortcut> FirstDialogueHotkey { get; private set; }
        public ConfigEntry<KeyboardShortcut> TradeHotkey { get; private set; }
        public ConfigEntry<KeyboardShortcut> LeaveHotkey { get; private set; }
        public ConfigEntry<bool> JustifyText { get; private set; }
        public ConfigEntry<bool> ColorizeHotkeys { get; private set; }

        // Константы пастельных цветов для удобного управления палитрой [L1]
        public const string ColorExit = "#78292C";   // Выход
        public const string ColorTrade = "#FFFF00"; // Торговля
        public const string ColorChat = "#6BA36B";    // Общение
        //private const string ColorPastelGreen = "#6BA36B"; // Цифры 1-9

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ModEnabled = Config.Bind(
                "1. General",
                "ModEnabled",
                true,
                "Enable/Disable mod."
            );

            FirstDialogueHotkey = Config.Bind(
                "2. Controls",
                "FirstDialogueHotkey",
                new KeyboardShortcut(KeyCode.E),
                "First hotkey for skip."
            );
            TradeHotkey = Config.Bind(
                "2. Controls",
                "TradeHotkey",
                new KeyboardShortcut(KeyCode.T),
                "Hotkey for open trade window in answers menu."
            );
            LeaveHotkey = Config.Bind(
                "2. Controls",
                "LeaveHotkey",
                new KeyboardShortcut(KeyCode.Escape),
                "Hotkey for leave answers menu."
            );

            DisplayHotkeys = Config.Bind(
                "3. Visual",
                "DisplayHotkeys",
                true,
                "Show visual hotkey badges on dialogue options."
            );
            JustifyText = Config.Bind(
                "3. Visual",
                "JustifyText",
                false,
                "Use justified text alignment (stretches text to fit the block width, matching block heights but may cause wide spacing)."
            );
            ColorizeHotkeys = Config.Bind(
                "3. Visual",
                "ColorizeHotkeys",
                false,
                "Enable custom pastel colors for hotkey badges. If disabled, badges will use the default font color."
            );

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

            Log.LogInfo($"[{PluginName}] Version {PluginVersion} (Trade Hotkey & Soft Colors) successfully initialized!");
        }
    }

    // =========================================================================
    // --- ПАТЧ 1: ЛОГИКА УПРАВЛЕНИЯ (КЛИКИ, ЦИФРЫ, ESCAPE И ТОРГОВЛЯ НА T) ---
    // =========================================================================
    [HarmonyPatch(typeof(UIMultiAnswer), "Update")]
    public class UIMultiAnswer_Update_HotkeyPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIMultiAnswer __instance)
        {
            if (MainPlugin.Instance == null || !MainPlugin.Instance.ModEnabled.Value || !__instance.gameObject.activeInHierarchy) return;

            KeyCode mainKey = MainPlugin.Instance.FirstDialogueHotkey.Value.MainKey;
            bool isFirstDialogueHotkeyPressed = Input.GetKeyDown(mainKey);

            KeyCode leaveKey = MainPlugin.Instance.LeaveHotkey.Value.MainKey;
            bool isLeavePressed = Input.GetKeyDown(leaveKey);

            // Считываем клавишу торговли из конфига
            KeyCode tKey = MainPlugin.Instance.TradeHotkey.Value.MainKey;
            bool isTradePressed = (tKey != KeyCode.None) && Input.GetKeyDown(tKey);

            try
            {
                var visualDataField = AccessTools.Field(typeof(UIMultiAnswer), "visualData");
                var optionsField = AccessTools.Field(typeof(UIMultiAnswer), "answerOptions"); // Достаем список UI-виджетов кнопок [L1]

                if (visualDataField != null && optionsField != null)
                {
                    var activeAnswers = visualDataField.GetValue(__instance) as List<AnswerVisualData>;
                    var optionsList = optionsField.GetValue(__instance) as System.Collections.IEnumerable;

                    if (activeAnswers != null && activeAnswers.Count > 0 && optionsList != null)
                    {
                        // Перегоняем виджеты в удобный список для сверки флага доступности [L1]
                        var optionsArray = new List<Component>();
                        foreach (var opt in optionsList) { if (opt != null && opt is Component comp) optionsArray.Add(comp); }

                        // ХАК №1: Выход по нажатию Escape [L1]
                        if (isLeavePressed)
                        {
                            foreach (var answer in activeAnswers)
                            {
                                if (answer != null && answer.id == "common_leave")
                                {
                                    Input.ResetInputAxes();
                                    __instance.OnAnswerSelect(answer.id);
                                    return;
                                }
                            }
                        }

                        // ХАК №2: УЛЬТИМАТИВНЫЙ ВХОД В ТОРГОВЛЮ (Только если торговля ДОСТУПНА!) [L1]
                        if (isTradePressed)
                        {
                            for (int i = 0; i < activeAnswers.Count; i++)
                            {
                                if (activeAnswers[i] != null && activeAnswers[i].id == "common_trade" && i < optionsArray.Count)
                                {
                                    // Проверяем приватное поле available у виджета кнопки через рефлексию! [L1]
                                    var availField = AccessTools.Field(optionsArray[i].GetType(), "available");
                                    bool isAvailable = (availField == null) || (bool)availField.GetValue(optionsArray[i]);

                                    if (isAvailable)
                                    {
                                        Input.ResetInputAxes();
                                        __instance.OnAnswerSelect(activeAnswers[i].id);
                                        return;
                                    }
                                }
                            }
                        }

                        // ХАК №3: Одиночный выбор/интро (на Е или Пробел) [L1]
                        if (activeAnswers.Count == 1)
                        {
                            if (isFirstDialogueHotkeyPressed && optionsArray.Count > 0)
                            {
                                var availField = AccessTools.Field(optionsArray[0].GetType(), "available");
                                bool isAvailable = (availField == null) || (bool)availField.GetValue(optionsArray[0]);

                                // Прожимаем Е только если Хранителю РАЗРЕШЕНО это сделать! [L1]
                                if (isAvailable && !string.IsNullOrEmpty(activeAnswers[0].id))
                                {
                                    Input.ResetInputAxes();
                                    __instance.OnAnswerSelect(activeAnswers[0].id);
                                    return;
                                }
                            }
                        }
                        // ХАК №4: Стандартный цифровой многовыбор 1-9 [L1]
                        else if (activeAnswers.Count > 1)
                        {
                            int maxAnswers = Mathf.Min(activeAnswers.Count, 9);
                            for (int i = 0; i < maxAnswers; i++)
                            {
                                KeyCode targetNumKey = KeyCode.Alpha1 + i;

                                if (isFirstDialogueHotkeyPressed) && (i < optionsArray.Count)
                                {
                                    var availField = AccessTools.Field(optionsArray[0].GetType(), "available");
                                    bool isAvailable = (availField == null) || (bool)availField.GetValue(optionsArray[0]);

                                    // Цифра сработает строго в том случае, если у нас есть нужные ресурсы! [L1]
                                    if (isAvailable && !string.IsNullOrEmpty(activeAnswers[0].id))
                                    {
                                        Input.ResetInputAxes();
                                        __instance.OnAnswerSelect(activeAnswers[i].id);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainPlugin.Log.LogError("[DialogueTweaks] Error in UIMultiAnswer Update patch: " + ex.Message);
            }
        }
    }

    // =========================================================================
    // --- ПАТЧ 2: ПОЛНЫЙ ПЕРЕХВАТ И ЗАМЕНА МЕТОДА ОТРИСОВКИ (Текстовый Пастельный Дизайн) --- [L1]
    // =========================================================================
    [HarmonyPatch(typeof(UIMultiAnswerOption), "Show")]
    public class UIMultiAnswerOption_Show_VisualPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(UIMultiAnswerOption __instance, AnswerVisualData visualData, UIMultiAnswer multiAnswer)
        {
            if (MainPlugin.Instance == null || !MainPlugin.Instance.ModEnabled.Value) return true;
            if (__instance == null || visualData == null || multiAnswer == null) return true;

            try
            {
                // 1. Заполняем внутренние поля виджета, которые игра ожидает
                var visualDataField = AccessTools.Field(typeof(UIMultiAnswerOption), "visualData");
                if (visualDataField != null) visualDataField.SetValue(__instance, visualData);

                var multiAnswerField = AccessTools.Field(typeof(UIMultiAnswerOption), "multiAnswer");
                if (multiAnswerField != null) multiAnswerField.SetValue(__instance, multiAnswer);

                // Деактивируем углы (нативный цикл)
                var cornersField = AccessTools.Field(typeof(UIMultiAnswerOption), "corners");
                if (cornersField?.GetValue(__instance) is System.Collections.IEnumerable cornersList)
                {
                    foreach (var corner in cornersList)
                    {
                        if (corner is Component comp) comp.gameObject.SetActive(false);
                    }
                }

                // 2. Достаем текстовое поле
                TextMeshProUGUI textLabel = null;
                var labelField = AccessTools.Field(typeof(UIMultiAnswerOption), "label");
                if (labelField != null)
                {
                    textLabel = labelField.GetValue(__instance) as TextMeshProUGUI;
                    if (textLabel != null)
                    {
                        textLabel.richText = true;

                        if (MainPlugin.Instance.JustifyText.Value)
                        {
                            textLabel.alignment = TextAlignmentOptions.Justified;
                        }
                    }
                }

                // 3. Получаем оригинальный перевод строки через TextStyle игры [L1]
                var styleField = AccessTools.Field(typeof(UIMultiAnswerOption), "hightlightedTextStyle");
                var style = styleField?.GetValue(__instance);
                string originalTranslatedText = visualData.id;

                if (style != null)
                {
                    var translateMethod = AccessTools.Method(style.GetType(), "TranslateAndColorizeTags", new Type[] { typeof(string) });
                    if (translateMethod != null)
                    {
                        originalTranslatedText = translateMethod.Invoke(style, new object[] { visualData.id }) as string;
                    }
                }

                // 4. Формируем красивый текстовый префикс (Мягкие пастельные скобки) [L1]
                string prefix = "";
                if (MainPlugin.Instance.DisplayHotkeys.Value && !string.IsNullOrEmpty(originalTranslatedText))
                {
                    string mainKeyName = MainPlugin.Instance.FirstDialogueHotkey.Value.MainKey.ToString();
                    string tradeKeyName = MainPlugin.Instance.TradeHotkey.Value.MainKey.ToString();

                    KeyCode lKey = MainPlugin.Instance.LeaveHotkey.Value.MainKey;
                    string leaveKeyName = (lKey == KeyCode.Escape) ? "Esc" : lKey.ToString();

                    var parentVisualDataField = AccessTools.Field(typeof(UIMultiAnswer), "visualData");
                    var activeAnswers = parentVisualDataField?.GetValue(multiAnswer) as List<AnswerVisualData>;

                    if (activeAnswers != null && activeAnswers.Count > 0 && visualData != null)
                    {
                        int index = activeAnswers.IndexOf(visualData);
                        int totalCount = activeAnswers.Count;

                        bool useColor = MainPlugin.Instance.ColorizeHotkeys.Value;

                        // Контекст 1: Одиночная плашка мыслей в интро (Мягкий теплый янтарный)
                        /*if (totalCount == 1)
                        {
                            prefix = useColor ? $"<color={ColorChat}>[{mainKeyName}]</color> " : $"[{mainKeyName}] ";
                        }
                        // Контекст 2: Пункт выхода из диалога (Приглушенный благородный бордово-красный)
                        else*/
                        if (visualData.id == "common_leave")
                        {
                            prefix = useColor ? $"<color={MainPlugin.ColorExit}>[{leaveKeyName}]</color> " : $"[{leaveKeyName}] ";
                        }
                        // Контекст 3: ПУНКТ ТОРГОВЛИ (Умная подсказка [T] вместо цифры! Красивый оливково-зеленый)
                        else if (visualData.id == "common_trade")
                        {
                            prefix = useColor ? $"<color={MainPlugin.ColorTrade}>[{tradeKeyName}]</color> " : $"[{tradeKeyName}] ";
                        }
                        // Контекст 4: Все остальные обычные пункты многовыбора (Мягкий пастельно-зеленый)
                        else if (index == 0)
                        {
                            //prefix = $"<b><color=#245c1c>[{index + 1}]</color> ";
                            prefix = useColor ? $"<color={MainPlugin.ColorChat}>[{mainKeyName}]</color> " : $"[{mainKeyName}] ";
                        }
                    }
                }

                // 5. Выводим текст на экран и активируем плашку! [L1]
                if (textLabel != null)
                {
                    textLabel.text = prefix + originalTranslatedText;
                }

                __instance.gameObject.SetActive(true);

                // Возвращаем false, блокируя ванильный метод Show игры! [L1]
                return false;
            }
            catch (Exception ex)
            {
                MainPlugin.Log.LogError("[DialogueTweaks] Error in Show Prefix Override: " + ex.Message);
                return true;
            }
        }
    }
}