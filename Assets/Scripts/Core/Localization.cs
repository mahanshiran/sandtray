using System.Collections.Generic;
using UnityEngine;

namespace Sandplay.Core
{
    public enum Language { English, Chinese }

    public static class Localization
    {
        private static Language _current = Language.English;
        private static bool _initialized;
        public static Language Current => _current;

        public static event System.Action OnLanguageChanged;

        /// <summary>
        /// Auto-detect device language on first access. Call early or let it self-init.
        /// Respects a previously saved user preference stored in PlayerPrefs.
        /// </summary>
        public static void AutoDetect()
        {
            if (_initialized) return;
            _initialized = true;

            // Restore saved preference first
            int saved = UnityEngine.PlayerPrefs.GetInt("sandplay_lang", -1);
            if (saved == (int)Language.English || saved == (int)Language.Chinese)
            {
                _current = (Language)saved;
                return;
            }

            // No saved preference — fall back to system language
            var sysLang = UnityEngine.Application.systemLanguage;
            if (sysLang == UnityEngine.SystemLanguage.Chinese ||
                sysLang == UnityEngine.SystemLanguage.ChineseSimplified ||
                sysLang == UnityEngine.SystemLanguage.ChineseTraditional)
            {
                _current = Language.Chinese;
            }
            else
            {
                _current = Language.English;
            }
        }

        public static void SetLanguage(Language lang)
        {
            _initialized = true;
            if (_current == lang) return;
            _current = lang;
            UnityEngine.PlayerPrefs.SetInt("sandplay_lang", (int)lang);
            UnityEngine.PlayerPrefs.Save();
            OnLanguageChanged?.Invoke();
        }

        public static string Get(string key)
        {
            if (!_initialized) AutoDetect();
            var table = _current == Language.Chinese ? _zh : _en;
            if (table.TryGetValue(key, out string val)) return val;
            // Fallback to English
            if (_en.TryGetValue(key, out string fallback)) return fallback;
            return key;
        }

        public static string Get(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        // ─── English ───────────────────────────────────────
        private static readonly Dictionary<string, string> _en = new Dictionary<string, string>
        {
            // Toolbar
            ["toolbar.sand"] = "Sand",
            ["tool.raise"] = "Raise",
            ["tool.dig"] = "Dig",
            ["tool.smooth"] = "Smooth",
            ["tool.flatten"] = "Flat",
            ["tool.walk"] = "Walk",
            ["tool.settings"] = "Settings",
            ["tool.ai_analysis"] = "AI\nReflection",
            ["tool.manual_analysis"] = "Manual\nReport",
            ["tool.exit"] = "Exit",
            ["tool.sand_material"] = "Texture",

            // Sand material presets
            ["mat.sand"] = "Sand",
            ["mat.rock"] = "Rock",
            ["mat.grass"] = "Grass",
            ["mat.snow"] = "Snow",
            ["mat.mud"] = "Mud",

            // Manual report panel
            ["manual.title"] = "Manual Report",
            ["manual.placeholder"] = "Type your observations, notes, or session summary here\u2026",
            ["manual.save"] = "Save Report",
            ["manual.saved"] = "Report saved!",
            ["manual.empty"] = "Please write something before saving.",

            // Subscription / paywall
            ["sub.pro_badge"] = "PRO",
            ["sub.subscribed_badge"] = "PRO+",
            ["sub.vip_feature"] = "Unlock this feature",
            ["sub.upgrade"] = "Upgrade to PRO",
            ["sub.paywall_title"] = "Sandtray Pro",
            ["sub.paywall_subtitle"] = "Unlimited AI-assisted reflections, session hosting, and PDF reports",
            ["sub.paywall_free_note"] = "Free includes 3 of each per month",
            ["sub.feature_ai"] = "Unlimited AI Reflections",
            ["sub.feature_host"] = "Unlimited Session Hosting",
            ["sub.feature_reports"] = "Unlimited PDF Reports",
            ["sub.feature_replays"] = "Full Replay History",
            ["sub.feature_support"] = "Priority Support",
            ["sub.loading"] = "Loading plans…",
            ["sub.subscribe"] = "Subscribe",
            ["sub.restore"] = "Restore Purchases",
            ["sub.restoring"] = "Restoring…",
            ["sub.restored_yes"] = "Subscription restored!",
            ["sub.restored_no"] = "No active subscription found.",
            ["sub.purchasing"] = "Processing…",
            ["sub.purchase_ok"] = "Welcome to Sandtray Pro!",
            ["sub.purchase_err"] = "Purchase failed: {0}",
            ["sub.terms"] = "Subscriptions auto-renew unless cancelled 24h before renewal.",
            ["sub.privacy"] = "Privacy Policy",
            ["sub.terms_link"] = "Terms of Use",
            ["sub.best_value"] = "Best value",
            ["sub.save_percent"] = "Save {0}%",
            ["sub.locked_ai"] = "AI-assisted reflection is a Pro feature. Subscribe to unlock.",
            ["sub.locked_host"] = "Sign in to host a session. Free accounts get 3 hosted sessions.",
            ["sub.free_ai_limit"] = "You have used your 3 free AI reflections this month. Upgrade to VIP for unlimited reflections.",
            ["sub.free_pdf_limit"] = "You have used your 3 free PDF exports this month. Upgrade to VIP for unlimited exports.",
            ["sub.free_host_limit"] = "You have used your 3 free hosted sessions. Upgrade to VIP for unlimited hosting.",
            ["sub.locked_replay"] = "Free accounts can play the latest replay. Upgrade to VIP to unlock all older replays.",
            ["sub.period_month"] = "/ month",
            ["sub.period_year"] = "/ year",
            ["sub.period_week"] = "/ week",
            ["sub.period_once"] = "once",
            ["sub.no_plans"] = "No plans available.",
            ["sub.mobile_only"] = "Subscriptions are available on iOS and Android.\nPlease subscribe from the mobile app.",
            ["sub.ok"] = "OK",

            // Brush
            ["brush.radius"] = "Radius",
            ["brush.strength"] = "Strength",
            ["brush.radius_val"] = "R:{0:F1}",
            ["brush.strength_val"] = "S:{0:F1}",

            // Catalog
            ["catalog.title"] = "Object Catalog",
            ["button.catalog"] = "Catalog",

            // Status
            ["status.ready"] = "Ready",
            ["status.hint"] = "Ready - Right-click to orbit, Scroll to zoom, Middle-click to pan",
            ["status.selected"] = "Selected: {0}",
            ["status.saved"] = "Saved: {0}",
            ["status.loaded"] = "Loaded: {0}",
            ["status.object"] = "Object",

            // Action panel fallbacks
            ["action.vertical"] = "\u2195",
            ["action.rotate"] = "\u21BB",
            ["action.resize"] = "\u2922",
            ["action.delete"] = "\u2716",

            // Analysis
            ["analysis.title"] = "AI-Assisted Reflection",
            ["analysis.ask"] = "Create AI Reflection",
            ["analysis.loading"] = "Preparing reflection... Please wait...",
            ["analysis.login_required"] = "Create a free account or sign in to use AI-assisted reflection.",
            ["analysis.disclaimer"] = "AI-assisted reflection — not a diagnosis or clinical conclusion. Observations and optional hypotheses are separated below; only the creator can confirm personal meaning.",
            ["analysis.error.capture"] = "Failed to capture session data.",
            ["analysis.error.manager"] = "AIAnalysisManager not found in scene.",
            ["analysis.error"] = "Error: {0}",
            ["analysis.save"] = "Save",
            ["analysis.saved"] = "Saved",
            ["analysis.export_pdf"] = "Export PDF",
            ["analysis.save_not_logged_in"] = "Not logged in",
            ["analysis.saving"] = "Saving to cloud...",
            ["analysis.save_done"] = "Saved to cloud",
            ["analysis.save_fail"] = "Save failed: {0}",
            ["analysis.pdf_login_required"] = "Login required for PDF",
            ["analysis.wait_save"] = "Please wait...",
            ["analysis.pdf_generating"] = "Generating PDF...",
            ["analysis.pdf_saved"] = "PDF saved: {0}",
            ["analysis.pdf_fail"] = "PDF error: {0}",

            // Walk mode
            ["walk.exit"] = "Exit Walk",

            // Main menu
            ["menu.title"] = "Sandtray",
            ["menu.subtitle"] = "THERAPEUTIC WORKSPACE",
            ["menu.welcome"] = "Welcome back, {0}",
            ["menu.welcome_guest"] = "Welcome",
            ["menu.welcome_sub"] = "Create, explore and reflect in your sandtray world.",
            ["menu.continue_playing"] = "Load Boards",
            ["menu.view_all"] = "View All  >",
            ["menu.nav_home"] = "Home",
            ["menu.nav_boards"] = "My Boards",
            ["menu.my_boards_sub"] = "{0} boards",
            ["menu.my_boards_open"] = "Open",
            ["menu.nav_multiplayer"] = "Multiplayer",
            ["menu.nav_replays"] = "Replays",
            ["menu.nav_ai"] = "AI Reflection",
            ["menu.nav_objects"] = "Objects",
            ["menu.nav_settings"] = "Settings",
            ["menu.cta_new_desc"] = "Create a new sandtray world.",
            ["menu.cta_host_desc"] = "Create and invite others to join.",
            ["menu.cta_join_desc"] = "Join a friend's ongoing session.",
            ["menu.tile_replays"] = "Replays",
            ["menu.tile_replays_desc"] = "Watch and reflect on your past sessions.",
            ["menu.tile_ai"] = "AI-Assisted Reflection",
            ["menu.tile_ai_desc"] = "Explore observations, possibilities, and open questions.",
            ["menu.tile_objects"] = "Objects Library",
            ["menu.tile_objects_desc"] = "Unlock all objects and build your world.",
            ["menu.tile_multiplayer"] = "Multiplayer",
            ["menu.tile_multiplayer_desc"] = "Create or join real-time sessions.",
            ["menu.new_board"] = "New Board",
            ["menu.host_online"] = "Host Online",
            ["menu.join_session"] = "Join Session",
            ["menu.saved_boards"] = "Saved Boards",
            ["menu.empty"] = "No saved boards yet.\nTap \"+ New Board\" to create one.",
            ["menu.account"] = "Account",
            ["menu.replays"] = "Replays",
            ["replays.title"] = "Session Replays",
            ["replays.empty"] = "No recorded sessions yet.",
            ["replays.duration"] = "{0}s, {1} events",
            ["replays.play"] = "Play",
            ["replays.vip_locked"] = "VIP",
            ["replays.upgrade"] = "Upgrade",
            ["replays.delete"] = "Delete",
            ["replays.close"] = "Close",
            ["replays.confirm_delete"] = "Delete this replay?",
            ["replays.read_only"] = "READ-ONLY REPLAY",
            ["replays.stop"] = "Stop",
            ["replays.quit"] = "Quit",
            ["replays.pause"] = "Pause",
            ["replays.resume"] = "Play",
            ["replays.finished"] = "Replay finished",
            ["replays.replay_again"] = "Replay again",
            ["replays.back_list"] = "Back to list",
            ["replays.free_note"] = "Free: only the latest replay is playable. Upgrade for full history.",
            ["replays.storage_note"] = "Replays are stored on this device. Delete old ones to free space.",
            ["replays.latest_badge"] = "Latest",
            ["replays.reset_view"] = "Reset view",
            ["replays.export_video"] = "Export",
            ["replays.exporting"] = "Exporting video… {0}%",
            ["replays.export_done"] = "Video ready to share",
            ["replays.export_fail"] = "Export failed: {0}",
            ["replays.export_too_long"] = "Replay is too long to export (max 5 min).",

            // Login / Account
            ["login.title"] = "Sign In",
            ["login.email"] = "Email",
            ["login.password"] = "Password",
            ["login.sign_in"] = "Sign In",
            ["login.register"] = "Create Account",
            ["login.signing_in"] = "Signing in...",
            ["login.registering"] = "Creating account...",
            ["login.success"] = "Welcome, {0}",
            ["login.error"] = "Error: {0}",
            ["login.logged_in_as"] = "Signed in as {0}",
            ["login.sign_out"] = "Sign Out",
            ["login.name"] = "Full Name",
            ["login.user_type"] = "Account type",
            ["login.type_normal"] = "Normal User",
            ["login.type_psychologist"] = "Psychologist",

            // Board list
            ["board.open"] = "Open",
            ["board.rename"] = "Rename",
            ["board.reports"] = "Reports",
            ["board.delete"] = "Delete",
            ["board.loading"] = "Opening board…",
            ["board.loading_new"] = "Preparing board…",
            ["board.loading_catalog"] = "Loading catalog…",
            ["board.loading_prepare"] = "Setting up sandtray…",
            ["board.loading_restore"] = "Restoring board…",
            ["board.loading_objects"] = "Placing objects…",
            ["board.loading_ready"] = "Almost ready…",

            // Reports panel
            ["reports.title"] = "Reports: {0}",
            ["reports.empty"] = "No AI reflection reports yet.\nOpen this board and create an AI-assisted reflection.",
            ["reports.view"] = "View",
            ["reports.delete"] = "Delete",
            ["reports.pdf"] = "\uD83D\uDCC4 PDF",
            ["reports.detail_title"] = "Report \u2014 {0}",
            ["reports.pdf_login"] = "Login required for PDF",
            ["reports.pdf_uploading"] = "Uploading\u2026",
            ["reports.pdf_downloading"] = "Generating PDF\u2026",
            ["reports.pdf_saved"] = "PDF saved!",
            ["reports.pdf_error"] = "PDF error: {0}",

            // Dialogs
            ["dialog.new_board"] = "New Board",
            ["dialog.rename"] = "Rename Board",
            ["dialog.ok"] = "OK",
            ["dialog.cancel"] = "Cancel",

            // Size dialog
            ["size.title"] = "Choose Board Size",
            ["size.standard"] = "Standard (Square)",
            ["size.standard_desc"] = "10 × 10",
            ["size.medium"] = "Medium (Wide)",
            ["size.medium_desc"] = "10 × 13",
            ["size.custom"] = "Custom",
            ["size.width"] = "W:",
            ["size.depth"] = "H:",

            // Host panel
            ["host.title"] = "Host Online Session",
            ["host.info"] = "Choose a hosting mode, then open a board.\nThe room will be created after you enter the board.",
            ["host.cloud"] = "\u2601  Cloud Session  (room code \u2014 works anywhere)",
            ["host.lan"] = "\u26A1  LAN Session  (same Wi-Fi \u2014 IP: {0})",
            ["host.cancel"] = "Cancel",

            // Join panel
            ["join.title"] = "Join Session",
            ["join.role_label"] = "Join as:",
            ["join.observer"] = "Observer",
            ["join.therapist"] = "Therapist",
            ["join.patient"] = "Patient",
            ["host.therapist_mode"] = "Therapist Mode",
            ["host.therapist_mode_desc"] = "You guide; the first joiner plays as Patient",
            ["net.patient_active"] = "Patient connected",
            ["therapist.patient_acting"] = "Patient is sculpting…",
            ["therapist.patient_painting"] = "Patient is painting…",
            ["therapist.patient_placing"] = "Patient placed an object",
            ["join.cloud_label"] = "Join via Room Code",
            ["join.placeholder_code"] = "ABC123",
            ["join.scan_qr"] = "Scan QR",
            ["join.join_room"] = "Join Room",
            ["join.invalid_code"] = "Enter the room code (4-6 letters)",
            ["join.joining"] = "Joining...",
            ["join.room_not_found"] = "Room not found or connection failed",
            ["join.or"] = "or",
            ["join.lan_label"] = "\u26A1  Join via IP Address  (same Wi-Fi)",
            ["join.placeholder_ip"] = "192.168.1.",
            ["join.connect"] = "Connect",
            ["join.missing_ip"] = "Please enter an IP address",
            ["join.connecting"] = "Connecting...",
            ["join.connection_failed"] = "Connection failed",
            ["join.cancel"] = "Cancel",

            // Network
            ["net.room"] = "Room: {0}",
            ["net.connecting"] = "Connecting...",
            ["net.spectating"] = "SPECTATING",
            ["net.viewers"] = "{0} viewer{1}",
            ["net.viewers_s"] = "s",

            // Reconnect
            ["reconnect.return"] = "Return to Menu",
            ["reconnect.message"] = "Connection lost\nReconnecting... ({0}/10)",

            // Settings
            ["settings.title"] = "Settings",
            ["settings.close"] = "\u00D7",
            ["settings.sand_color"] = "Sand Color",
            ["settings.box_outer"] = "Box Outer",
            ["settings.box_inner"] = "Box Inner",
            ["settings.floor"] = "Floor",
            ["settings.allow_objects_in_air"] = "Allow objects in air",
            ["settings.restore"] = "Restore Defaults",
            ["settings.pick"] = "Pick",
            ["settings.language"] = "Language",

            // Color picker
            ["colorpicker.title"] = "Pick Color",
            ["colorpicker.done"] = "Done",

            // QR scanner
            ["qr.title"] = "Scan QR Code",
            ["qr.instruction"] = "Point camera at a room QR code",
            ["qr.cancel"] = "Cancel",
            ["qr.requesting"] = "Requesting camera permission...",
            ["qr.waiting"] = "Waiting for camera...",
            ["qr.scanning"] = "Scanning...",
            ["qr.found"] = "Found: {0}",
            ["qr.tap_close"] = "Tap anywhere to close",

            // HUD
            ["hud.radius"] = "Radius: {0:F1}",
            ["hud.strength"] = "Strength: {0:F1}",
            ["hud.select"] = "Select",
            ["hud.raise_sand"] = "Raise Sand",
            ["hud.dig_sand"] = "Dig Sand",
            ["hud.smooth_sand"] = "Smooth Sand",
            ["hud.flatten_sand"] = "Flatten Sand",
            ["hud.place_object"] = "Place Object",
            ["hud.move_object"] = "Move Object",
            ["hud.rotate_object"] = "Rotate Object",
            ["hud.scale_object"] = "Scale Object",
            ["hud.none"] = "None",
            ["hud.saved"] = "Session saved!",
            ["hud.loaded"] = "Session loaded!",
            ["hud.screenshot"] = "Screenshot saved!",
            ["hud.analysis"] = "Analysis requested...",

            // Agora voice / video
            ["agora.mic_on"] = "Mic On",
            ["agora.mic_off"] = "Mute",
            ["agora.cam_on"] = "Cam On",
            ["agora.cam_off"] = "Cam Off",
            ["agora.show"] = "Show",
            ["agora.hide"] = "Hide",
            ["agora.leave"] = "End Call",
            ["agora.connecting"] = "Connecting audio…",
            ["agora.comm_title"] = "Communication",
            ["agora.you"] = "You",
            ["cat.Animals"] = "Animals",
            ["cat.People"] = "People",
            ["cat.Buildings"] = "Buildings",
            ["cat.Nature"] = "Nature",
            ["cat.Vehicles"] = "Vehicles",
            ["cat.Abstract"] = "Abstract",
        };

        // ─── Chinese (Simplified) ──────────────────────────
        private static readonly Dictionary<string, string> _zh = new Dictionary<string, string>
        {
            // Toolbar
            ["toolbar.sand"] = "沙子",
            ["tool.raise"] = "堆高",
            ["tool.dig"] = "挖掘",
            ["tool.smooth"] = "平滑",
            ["tool.flatten"] = "压平",
            ["tool.walk"] = "行走",
            ["tool.settings"] = "设置",
            ["tool.ai_analysis"] = "AI\n反思",
            ["tool.manual_analysis"] = "手动\n报告",
            ["tool.exit"] = "退出",
            ["tool.sand_material"] = "材质",

            // Sand material presets
            ["mat.sand"] = "沙子",
            ["mat.rock"] = "岩石",
            ["mat.grass"] = "草地",
            ["mat.snow"] = "雪地",
            ["mat.mud"] = "泥土",

            // Manual report panel
            ["manual.title"] = "手动报告",
            ["manual.placeholder"] = "在此输入您的观察、笔记或会话总结…",
            ["manual.save"] = "保存报告",
            ["manual.saved"] = "报告已保存！",
            ["manual.empty"] = "请先写一些内容就能保存。",

            // Subscription / paywall
            ["sub.pro_badge"] = "PRO",
            ["sub.subscribed_badge"] = "PRO+",
            ["sub.vip_feature"] = "解锁此功能",
            ["sub.upgrade"] = "升级到专业版",
            ["sub.paywall_title"] = "Sandtray 专业版",
            ["sub.paywall_subtitle"] = "无限 AI 辅助反思、会话托管与 PDF 报告",
            ["sub.paywall_free_note"] = "免费版每月各含3次",
            ["sub.feature_ai"] = "无限 AI 辅助反思",
            ["sub.feature_host"] = "无限会话托管",
            ["sub.feature_reports"] = "无限 PDF 报告",
            ["sub.feature_replays"] = "完整回放历史",
            ["sub.feature_support"] = "优先支持",
            ["sub.loading"] = "正在加载方案…",
            ["sub.subscribe"] = "订阅",
            ["sub.restore"] = "恢复购买",
            ["sub.restoring"] = "恢复中…",
            ["sub.restored_yes"] = "订阅已恢复！",
            ["sub.restored_no"] = "未找到有效订阅。",
            ["sub.purchasing"] = "处理中…",
            ["sub.purchase_ok"] = "欢迎使用 Sandtray 专业版！",
            ["sub.purchase_err"] = "购买失败：{0}",
            ["sub.terms"] = "订阅将自动续费，除非在续期前24小时取消。",
            ["sub.privacy"] = "隐私政策",
            ["sub.terms_link"] = "使用条款",
            ["sub.best_value"] = "最超值",
            ["sub.save_percent"] = "省 {0}%",
            ["sub.locked_ai"] = "AI 辅助反思是专业版功能。订阅后解锁。",
            ["sub.locked_host"] = "请先登录再托管会话。免费账户可托管3次。",
            ["sub.free_ai_limit"] = "您本月的3次免费AI辅助反思已用完。升级VIP即可无限使用。",
            ["sub.free_pdf_limit"] = "您本月的3次免费PDF导出已用完。升级VIP即可无限导出。",
            ["sub.free_host_limit"] = "您的3次免费托管会话已用完。升级VIP即可无限托管。",
            ["sub.locked_replay"] = "免费账户可播放最新回放。升级VIP即可解锁所有较早的回放。",
            ["sub.period_month"] = "/ 月",
            ["sub.period_year"] = "/ 年",
            ["sub.period_week"] = "/ 周",
            ["sub.period_once"] = "一次性",
            ["sub.no_plans"] = "暂无可用方案。",
            ["sub.mobile_only"] = "订阅仅支持 iOS 与 Android。\n请在手机 App 中完成订阅。",
            ["sub.ok"] = "好的",

            // Brush
            ["brush.radius"] = "半径",
            ["brush.strength"] = "力度",
            ["brush.radius_val"] = "半径:{0:F1}",
            ["brush.strength_val"] = "力度:{0:F1}",

            // Catalog
            ["catalog.title"] = "物品目录",
            ["button.catalog"] = "目录",

            // Status
            ["status.ready"] = "就绪",
            ["status.hint"] = "就绪 - 右键旋转，滚轮缩放，中键平移",
            ["status.selected"] = "已选择：{0}",
            ["status.saved"] = "已保存：{0}",
            ["status.loaded"] = "已加载：{0}",
            ["status.object"] = "物体",

            // Action panel fallbacks
            ["action.vertical"] = "\u2195",
            ["action.rotate"] = "\u21BB",
            ["action.resize"] = "\u2922",
            ["action.delete"] = "\u2716",

            // Analysis
            ["analysis.title"] = "AI 辅助反思",
            ["analysis.ask"] = "生成 AI 反思",
            ["analysis.loading"] = "正在准备反思…请稍候…",
            ["analysis.login_required"] = "创建免费账户或登录后即可使用AI辅助反思。",
            ["analysis.disclaimer"] = "AI辅助反思并非诊断或临床结论。下方将客观观察与可选假设分开呈现；个人意义只能由创作者本人确认。",
            ["analysis.error.capture"] = "无法捕获会话数据。",
            ["analysis.error.manager"] = "场景中未找到 AIAnalysisManager。",
            ["analysis.error"] = "错误：{0}",
            ["analysis.save"] = "保存",
            ["analysis.saved"] = "已保存",
            ["analysis.export_pdf"] = "导出PDF",
            ["analysis.save_not_logged_in"] = "未登录，无法保存",
            ["analysis.saving"] = "正在保存到云端…",
            ["analysis.save_done"] = "已保存到云端",
            ["analysis.save_fail"] = "保存失败：{0}",
            ["analysis.pdf_login_required"] = "导出PDF需要登录",
            ["analysis.wait_save"] = "请等待保存完成…",
            ["analysis.pdf_generating"] = "正在生成PDF…",
            ["analysis.pdf_saved"] = "PDF已保存：{0}",
            ["analysis.pdf_fail"] = "PDF生成失败：{0}",

            // Walk mode
            ["walk.exit"] = "退出行走",

            // Main menu
            ["menu.title"] = "沙盘游戏",
            ["menu.subtitle"] = "治疗工作空间",
            ["menu.welcome"] = "欢迎回来，{0}",
            ["menu.welcome_guest"] = "欢迎",
            ["menu.welcome_sub"] = "在沙盘世界中创作、探索与反思。",
            ["menu.continue_playing"] = "加载沙盘",
            ["menu.view_all"] = "查看全部  >",
            ["menu.nav_home"] = "首页",
            ["menu.nav_boards"] = "我的沙盘",
            ["menu.my_boards_sub"] = "{0} 个沙盘",
            ["menu.my_boards_open"] = "打开",
            ["menu.nav_multiplayer"] = "多人协作",
            ["menu.nav_replays"] = "回放",
            ["menu.nav_ai"] = "AI 辅助反思",
            ["menu.nav_objects"] = "物件",
            ["menu.nav_settings"] = "设置",
            ["menu.cta_new_desc"] = "创建一个新的沙盘世界。",
            ["menu.cta_host_desc"] = "创建并邀请他人加入。",
            ["menu.cta_join_desc"] = "加入好友正在进行的会话。",
            ["menu.tile_replays"] = "回放",
            ["menu.tile_replays_desc"] = "回看并反思过往会话。",
            ["menu.tile_ai"] = "AI 辅助反思",
            ["menu.tile_ai_desc"] = "探索客观观察、可选可能性与开放式问题。",
            ["menu.tile_objects"] = "物件库",
            ["menu.tile_objects_desc"] = "解锁全部物件，构建你的世界。",
            ["menu.tile_multiplayer"] = "多人协作",
            ["menu.tile_multiplayer_desc"] = "创建或加入实时会话。",
            ["menu.new_board"] = "新沙盘",
            ["menu.host_online"] = "在线主持",
            ["menu.join_session"] = "加入会话",
            ["menu.saved_boards"] = "已保存的沙盘",
            ["menu.empty"] = "还没有保存的沙盘。\n点击\"+ 新沙盘\"来创建。",
            ["menu.account"] = "账户",
            ["menu.replays"] = "回放",
            ["replays.title"] = "会话回放",
            ["replays.empty"] = "还没有录制的会话。",
            ["replays.duration"] = "{0}秒，{1}条事件",
            ["replays.play"] = "播放",
            ["replays.vip_locked"] = "VIP",
            ["replays.upgrade"] = "升级",
            ["replays.delete"] = "删除",
            ["replays.close"] = "关闭",
            ["replays.confirm_delete"] = "确认删除此回放？",
            ["replays.read_only"] = "只读回放",
            ["replays.stop"] = "停止",
            ["replays.quit"] = "退出",
            ["replays.pause"] = "暂停",
            ["replays.resume"] = "播放",
            ["replays.finished"] = "回放结束",
            ["replays.replay_again"] = "再看一遍",
            ["replays.back_list"] = "返回列表",
            ["replays.free_note"] = "免费版仅可播放最新回放。升级即可解锁全部历史。",
            ["replays.storage_note"] = "回放保存在本机。可删除旧回放以释放空间。",
            ["replays.latest_badge"] = "最新",
            ["replays.reset_view"] = "重置视角",
            ["replays.export_video"] = "导出",
            ["replays.exporting"] = "正在导出视频… {0}%",
            ["replays.export_done"] = "视频已可分享",
            ["replays.export_fail"] = "导出失败：{0}",
            ["replays.export_too_long"] = "回放过长，无法导出（最长5分钟）。",

            // Login / Account
            ["login.title"] = "登录",
            ["login.email"] = "邮箱",
            ["login.password"] = "密码",
            ["login.sign_in"] = "登录",
            ["login.register"] = "创建账户",
            ["login.signing_in"] = "登录中...",
            ["login.registering"] = "创建中...",
            ["login.success"] = "欢迎，{0}",
            ["login.error"] = "错误：{0}",
            ["login.logged_in_as"] = "已登录：{0}",
            ["login.sign_out"] = "退出登录",
            ["login.name"] = "姓名",
            ["login.user_type"] = "账户类型",
            ["login.type_normal"] = "普通用户",
            ["login.type_psychologist"] = "心理咨询师",

            // Board list
            ["board.open"] = "打开",
            ["board.rename"] = "重命名",
            ["board.reports"] = "报告",
            ["board.delete"] = "删除",
            ["board.loading"] = "正在打开沙盘…",
            ["board.loading_new"] = "正在准备沙盘…",
            ["board.loading_catalog"] = "加载图鉴…",
            ["board.loading_prepare"] = "布置沙盘…",
            ["board.loading_restore"] = "恢复沙盘…",
            ["board.loading_objects"] = "放置物件…",
            ["board.loading_ready"] = "即将完成…",

            // Reports panel
            ["reports.title"] = "报告：{0}",
            ["reports.empty"] = "暂无AI反思报告。\n打开此沙盘并生成一份AI辅助反思。",
            ["reports.view"] = "查看",
            ["reports.delete"] = "删除",
            ["reports.pdf"] = "\uD83D\uDCC4 PDF",
            ["reports.detail_title"] = "报告 — {0}",
            ["reports.pdf_login"] = "请登录以导出PDF",
            ["reports.pdf_uploading"] = "上传中…",
            ["reports.pdf_downloading"] = "生成PDF中…",
            ["reports.pdf_saved"] = "PDF已保存!",
            ["reports.pdf_error"] = "PDF错误：{0}",

            // Dialogs
            ["dialog.new_board"] = "新沙盘",
            ["dialog.rename"] = "重命名沙盘",
            ["dialog.ok"] = "确定",
            ["dialog.cancel"] = "取消",

            // Size dialog
            ["size.title"] = "选择沙盘大小",
            ["size.standard"] = "标准（正方形）",
            ["size.standard_desc"] = "10 × 10",
            ["size.medium"] = "中等（宽型）",
            ["size.medium_desc"] = "10 × 13",
            ["size.custom"] = "自定义",
            ["size.width"] = "宽：",
            ["size.depth"] = "深：",

            // Host panel
            ["host.title"] = "在线主持会话",
            ["host.info"] = "选择主持模式，然后打开一个沙盘。\n进入沙盘后将创建房间。",
            ["host.cloud"] = "\u2601  云端会话（房间码 - 随处可用）",
            ["host.lan"] = "\u26A1  局域网会话（同一Wi-Fi - IP：{0}）",
            ["host.cancel"] = "取消",

            // Join panel
            ["join.title"] = "加入会话",
            ["join.role_label"] = "加入身份：",
            ["join.observer"] = "观察者",
            ["join.therapist"] = "治疗师",
            ["join.patient"] = "来访者",
            ["host.therapist_mode"] = "治疗师模式",
            ["host.therapist_mode_desc"] = "由您引导，首位加入者作为来访者操作",
            ["net.patient_active"] = "来访者已连接",
            ["therapist.patient_acting"] = "来访者正在操作…",
            ["therapist.patient_painting"] = "来访者正在上色…",
            ["therapist.patient_placing"] = "来访者放置了一个物件",
            ["join.cloud_label"] = "通过房间码加入",
            ["join.placeholder_code"] = "ABC123",
            ["join.scan_qr"] = "扫码",
            ["join.join_room"] = "加入房间",
            ["join.invalid_code"] = "请输入房间码（4-6个字母）",
            ["join.joining"] = "加入中…",
            ["join.room_not_found"] = "未找到房间或连接失败",
            ["join.or"] = "或",
            ["join.lan_label"] = "\u26A1  通过IP地址加入（同一Wi-Fi）",
            ["join.placeholder_ip"] = "192.168.1.",
            ["join.connect"] = "连接",
            ["join.missing_ip"] = "请输入IP地址",
            ["join.connecting"] = "连接中…",
            ["join.connection_failed"] = "连接失败",
            ["join.cancel"] = "取消",

            // Network
            ["net.room"] = "房间：{0}",
            ["net.connecting"] = "连接中…",
            ["net.spectating"] = "观看中",
            ["net.viewers"] = "{0} 位观众",
            ["net.viewers_s"] = "",

            // Reconnect
            ["reconnect.return"] = "返回菜单",
            ["reconnect.message"] = "连接中断\n正在重连… ({0}/10)",

            // Settings
            ["settings.title"] = "设置",
            ["settings.close"] = "\u00D7",
            ["settings.sand_color"] = "沙子颜色",
            ["settings.box_outer"] = "盒子外层",
            ["settings.box_inner"] = "盒子内层",
            ["settings.floor"] = "地板",
            ["settings.allow_objects_in_air"] = "允许物体悬空",
            ["settings.restore"] = "恢复默认",
            ["settings.pick"] = "选择",
            ["settings.language"] = "语言",

            // Color picker
            ["colorpicker.title"] = "选择颜色",
            ["colorpicker.done"] = "完成",

            // QR scanner
            ["qr.title"] = "扫描二维码",
            ["qr.instruction"] = "将摄像头对准房间二维码",
            ["qr.cancel"] = "取消",
            ["qr.requesting"] = "正在请求摄像头权限…",
            ["qr.waiting"] = "等待摄像头…",
            ["qr.scanning"] = "扫描中…",
            ["qr.found"] = "已找到：{0}",
            ["qr.tap_close"] = "点击任意位置关闭",

            // HUD
            ["hud.radius"] = "半径：{0:F1}",
            ["hud.strength"] = "力度：{0:F1}",
            ["hud.select"] = "选择",
            ["hud.raise_sand"] = "堆沙",
            ["hud.dig_sand"] = "挖沙",
            ["hud.smooth_sand"] = "平滑沙面",
            ["hud.flatten_sand"] = "压平沙面",
            ["hud.place_object"] = "放置物体",
            ["hud.move_object"] = "移动物体",
            ["hud.rotate_object"] = "旋转物体",
            ["hud.scale_object"] = "缩放物体",
            ["hud.none"] = "无",
            ["hud.saved"] = "会话已保存！",
            ["hud.loaded"] = "会话已加载！",
            ["hud.screenshot"] = "截图已保存！",
            ["hud.analysis"] = "已请求分析…",

            // Agora voice / video
            ["agora.mic_on"] = "开麦",
            ["agora.mic_off"] = "静音",
            ["agora.cam_on"] = "开摄像头",
            ["agora.cam_off"] = "关摄像头",
            ["agora.show"] = "显示",
            ["agora.hide"] = "隐藏",
            ["agora.leave"] = "挂断",
            ["agora.connecting"] = "连接音频中…",
            ["agora.comm_title"] = "语音视频",
            ["agora.you"] = "我",

            // Object categories
            ["cat.Animals"] = "动物",
            ["cat.People"] = "人物",
            ["cat.Buildings"] = "建筑",
            ["cat.Nature"] = "自然",
            ["cat.Vehicles"] = "交通工具",
            ["cat.Abstract"] = "抽象",
        };
    }
}
