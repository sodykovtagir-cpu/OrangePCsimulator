import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from unity_ui_emitter import (  # noqa: E402
    UIEmitter,
    ALIGN_MIDDLE_CENTER,
    ALIGN_MIDDLE_LEFT,
    FONT_BOLD,
    FONT_ITALIC,
)

# Количество заранее собранных строк в таблице сравнения и в истории
LEADERBOARD_ROWS = 8
HISTORY_ROWS = 5

FONT_GUID = "2644e223131232f41a7199fc4aef1e34"
CIRCLE_SPRITE_GUID = "09807ade07a9e244dabec7885cc0a17a"
APP_ICON_GUID = "96035d719d554591ac698214dd94ed9c"
THREEDMORK_SCRIPT_GUID = "48ab01ab22fd4bc78201ca8ad4b6d729"
WINDOWDRAG_GUID = "a0450bed449d406439621f6405243e67"
SLIDER_GUID = "67db9e8f0e2ae9c40bc1e2b64352a6b4"
RAWIMAGE_GUID = "1344c3c82d62a2a41a3576d8abb8e3ea"
TEXT_GUID = "5f7201a12d95ffc409449d95f23cf332"
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"
MAX_SPRITE_GUID = "8bee6c5e8f8f34343b72503754d62702"
MIN_SPRITE_GUID = "bb0f5908d2d446f085cf605d2613be08"

class PrefabBuilder:
    def __init__(self):
        self._cur_id = 5000000000000000
        self.chunks = []

    def next_id(self):
        self._cur_id += 1
        return str(self._cur_id)

    def add(self, chunk):
        self.chunks.append(chunk.strip())

b = PrefabBuilder()

# 1. Root IDs
id_root_go = b.next_id()
id_root_tr = b.next_id()
id_root_cr = b.next_id()
id_root_img = b.next_id()
id_root_app = b.next_id()

# ============================================================================
# 0. START PANEL - предсобранный (не процедурный) стартовый экран:
#    слева сравнительная таблица результатов других ПК, справа история
#    тестов на этой машине + сводка по установленному железу.
#    Скрипт ThreeDMork только подставляет текст в эти заранее собранные поля.
# ============================================================================
ui = UIEmitter(b)

C_PANEL = "0.12, 0.12, 0.16, 0.92"
C_PANEL_DARK = "0.09, 0.09, 0.12, 0.95"
C_HEADER = "0.19, 0.2, 0.26, 1"
C_ROW_A = "0.15, 0.15, 0.2, 0.9"
C_ROW_B = "0.175, 0.175, 0.225, 0.9"
C_ROW_SELF = "0.3, 0.2, 0.05, 0.95"
C_TEXT = "1, 1, 1, 1"
C_TEXT_DIM = "0.62, 0.65, 0.72, 1"
C_TEXT_SOFT = "0.85, 0.87, 0.92, 1"
C_TEXT_ORANGE = "1, 0.72, 0.2, 1"
C_TEXT_GREEN = "0.55, 0.9, 0.6, 1"
C_TEXT_HEADER = "0.72, 0.76, 0.85, 1"

# Идентификаторы кнопки "в меню" резервируются заранее: её GameObject создаётся
# ниже, в секции экрана результатов, но на неё ссылается и корневой скрипт.
MENU_BTN_IDS = tuple(b.next_id() for _ in range(5))
id_btn_menu = MENU_BTN_IDS[4]
id_btn_menu_tr = MENU_BTN_IDS[1]

id_start_panel_go, id_start_panel_tr = ui.stretch_panel("StartPanel", id_root_tr)

# --- Левая колонка: сравнение с другими ПК ---------------------------------
LB_X, LB_Y, LB_W, LB_H = 12, 10, 486, 380
_, id_lb_tr, _ = ui.background("LeaderboardPanel", id_start_panel_tr, LB_X, LB_Y, LB_W, LB_H, C_PANEL)

ui.text("Title", id_lb_tr, 10, 6, 466, 22, "COMPARISON WITH OTHER PCs",
        color=C_TEXT_ORANGE, size=15, style=FONT_BOLD, align=ALIGN_MIDDLE_LEFT)
ui.text("Subtitle", id_lb_tr, 10, 30, 466, 16, "Reference 3DMork results of the global leaderboard",
        color=C_TEXT_DIM, size=10, align=ALIGN_MIDDLE_LEFT)

ui.background("HeaderRow", id_lb_tr, 8, 50, 470, 22, C_HEADER)
LB_HEADER = [("#", 8, 30, ALIGN_MIDDLE_CENTER), ("PC", 38, 128, ALIGN_MIDDLE_LEFT),
             ("CPU", 166, 96, ALIGN_MIDDLE_LEFT), ("GPU", 262, 108, ALIGN_MIDDLE_LEFT),
             ("SCORE", 370, 60, ALIGN_MIDDLE_CENTER), ("FPS", 430, 48, ALIGN_MIDDLE_CENTER)]
for _label, _x, _w, _align in LB_HEADER:
    ui.text("Header_" + _label, id_lb_tr, _x, 50, _w, 22, _label,
            color=C_TEXT_HEADER, size=11, style=FONT_BOLD, align=_align)

lb_rank, lb_name, lb_cpu, lb_gpu, lb_score, lb_fps = [], [], [], [], [], []
for _i in range(LEADERBOARD_ROWS):
    _y = 74 + _i * 25
    ui.background("LbRow_%d" % _i, id_lb_tr, 8, _y, 470, 24, C_ROW_A if _i % 2 == 0 else C_ROW_B)
    lb_rank.append(ui.text("LbRank_%d" % _i, id_lb_tr, 8, _y, 30, 24, "#%d" % (_i + 1),
                           color=C_TEXT_DIM, size=11, align=ALIGN_MIDDLE_CENTER))
    lb_name.append(ui.text("LbName_%d" % _i, id_lb_tr, 38, _y, 128, 24, "--",
                           color=C_TEXT, size=11, align=ALIGN_MIDDLE_LEFT, best_fit=1))
    lb_cpu.append(ui.text("LbCpu_%d" % _i, id_lb_tr, 166, _y, 96, 24, "--",
                          color=C_TEXT_SOFT, size=10, align=ALIGN_MIDDLE_LEFT, best_fit=1))
    lb_gpu.append(ui.text("LbGpu_%d" % _i, id_lb_tr, 262, _y, 108, 24, "--",
                          color=C_TEXT_SOFT, size=10, align=ALIGN_MIDDLE_LEFT, best_fit=1))
    lb_score.append(ui.text("LbScore_%d" % _i, id_lb_tr, 370, _y, 60, 24, "--",
                            color=C_TEXT_ORANGE, size=11, style=FONT_BOLD, align=ALIGN_MIDDLE_CENTER))
    lb_fps.append(ui.text("LbFps_%d" % _i, id_lb_tr, 430, _y, 48, 24, "--",
                          color=C_TEXT_GREEN, size=10, align=ALIGN_MIDDLE_CENTER))

# Строка "этот компьютер"
ui.background("SelfRow", id_lb_tr, 8, 276, 470, 28, C_ROW_SELF)
lb_self_rank = ui.text("SelfRank", id_lb_tr, 8, 276, 30, 28, "*", color=C_TEXT_ORANGE, size=13,
                       style=FONT_BOLD, align=ALIGN_MIDDLE_CENTER)
lb_self_name = ui.text("SelfName", id_lb_tr, 38, 276, 128, 28, "This PC", color=C_TEXT, size=11,
                       style=FONT_BOLD, align=ALIGN_MIDDLE_LEFT, best_fit=1)
lb_self_spec = ui.text("SelfSpec", id_lb_tr, 166, 276, 204, 28, "--", color=C_TEXT_SOFT, size=10,
                       align=ALIGN_MIDDLE_LEFT, best_fit=1)
lb_self_score = ui.text("SelfScore", id_lb_tr, 370, 276, 60, 28, "--", color=C_TEXT_ORANGE, size=11,
                        style=FONT_BOLD, align=ALIGN_MIDDLE_CENTER)
lb_self_fps = ui.text("SelfFps", id_lb_tr, 430, 276, 48, 28, "--", color=C_TEXT_GREEN, size=10,
                     align=ALIGN_MIDDLE_CENTER)
lb_average = ui.text("Average", id_lb_tr, 8, 310, 470, 18, "Average of reference PCs: --",
                     color=C_TEXT_DIM, size=10, align=ALIGN_MIDDLE_LEFT)
ui.text("Hint", id_lb_tr, 8, 330, 470, 18, "Your PC is marked with * - the row updates after every run.",
        color=C_TEXT_DIM, size=10, style=FONT_ITALIC, align=ALIGN_MIDDLE_LEFT)

# --- Правая колонка: история тестов этого ПК --------------------------------
HI_X, HI_Y, HI_W, HI_H = 506, 10, 332, 380
_, id_hist_tr, _ = ui.background("HistoryPanel", id_start_panel_tr, HI_X, HI_Y, HI_W, HI_H, C_PANEL)

ui.text("Title", id_hist_tr, 10, 6, 312, 22, "TEST HISTORY - THIS PC",
        color=C_TEXT_ORANGE, size=15, style=FONT_BOLD, align=ALIGN_MIDDLE_LEFT)
ui.text("Subtitle", id_hist_tr, 10, 30, 312, 16, "Runs are stored locally on this computer",
        color=C_TEXT_DIM, size=10, align=ALIGN_MIDDLE_LEFT)

ui.background("HeaderRow", id_hist_tr, 8, 50, 316, 20, C_HEADER)
HI_HEADER = [("#", 8, 24, ALIGN_MIDDLE_CENTER), ("DATE", 32, 118, ALIGN_MIDDLE_LEFT),
             ("SCORE", 150, 78, ALIGN_MIDDLE_CENTER), ("FPS", 228, 88, ALIGN_MIDDLE_CENTER)]
for _label, _x, _w, _align in HI_HEADER:
    ui.text("Header_" + _label, id_hist_tr, _x, 50, _w, 20, _label,
            color=C_TEXT_HEADER, size=11, style=FONT_BOLD, align=_align)

hist_index, hist_date, hist_score, hist_fps = [], [], [], []
for _i in range(HISTORY_ROWS):
    _y = 72 + _i * 25
    ui.background("HistRow_%d" % _i, id_hist_tr, 8, _y, 316, 24, C_ROW_A if _i % 2 == 0 else C_ROW_B)
    hist_index.append(ui.text("HistIndex_%d" % _i, id_hist_tr, 8, _y, 24, 24, "--",
                              color=C_TEXT_DIM, size=11, align=ALIGN_MIDDLE_CENTER))
    hist_date.append(ui.text("HistDate_%d" % _i, id_hist_tr, 32, _y, 118, 24, "--",
                             color=C_TEXT_SOFT, size=10, align=ALIGN_MIDDLE_LEFT))
    hist_score.append(ui.text("HistScore_%d" % _i, id_hist_tr, 150, _y, 78, 24, "--",
                              color=C_TEXT_ORANGE, size=11, style=FONT_BOLD, align=ALIGN_MIDDLE_CENTER))
    hist_fps.append(ui.text("HistFps_%d" % _i, id_hist_tr, 228, _y, 88, 24, "--",
                            color=C_TEXT_GREEN, size=10, align=ALIGN_MIDDLE_CENTER))

hist_empty = ui.text("EmptyHint", id_hist_tr, 8, 72, 316, 125, "No benchmark runs on this PC yet.",
                     color=C_TEXT_DIM, size=11, align=ALIGN_MIDDLE_CENTER, active=1)
hist_best = ui.text("BestScore", id_hist_tr, 8, 200, 316, 20, "Best score: --",
                    color=C_TEXT_ORANGE, size=12, style=FONT_BOLD, align=ALIGN_MIDDLE_LEFT)

# Сводка по текущему железу
_, id_hw_tr, _ = ui.background("HardwarePanel", id_hist_tr, 8, 226, 316, 146, C_PANEL_DARK)
hw_title = ui.text("HwTitle", id_hw_tr, 8, 6, 300, 20, "THIS PC", color=C_TEXT, size=12,
                   style=FONT_BOLD, align=ALIGN_MIDDLE_LEFT)
hw_cpu = ui.text("HwCpu", id_hw_tr, 8, 30, 300, 22, "CPU: --", color=C_TEXT_SOFT, size=11,
                 align=ALIGN_MIDDLE_LEFT, best_fit=1)
hw_gpu = ui.text("HwGpu", id_hw_tr, 8, 56, 300, 22, "GPU: --", color=C_TEXT_SOFT, size=11,
                 align=ALIGN_MIDDLE_LEFT, best_fit=1)
hw_ram = ui.text("HwRam", id_hw_tr, 8, 82, 300, 22, "RAM: --", color=C_TEXT_SOFT, size=11,
                 align=ALIGN_MIDDLE_LEFT, best_fit=1)
hw_board = ui.text("HwBoard", id_hw_tr, 8, 108, 300, 22, "BOARD: --", color=C_TEXT_SOFT, size=11,
                   align=ALIGN_MIDDLE_LEFT, best_fit=1)

# --- Кнопки стартового экрана ----------------------------------------------
id_btn_start, _, _ = ui.button("ButtonStart", id_start_panel_tr, 305, 400, 240, 44, "START TEST",
                               "1, 0.5, 0, 1", label_size=20)
id_btn_start_close, _, _ = ui.button("ButtonStartClose", id_start_panel_tr, 688, 400, 150, 44, "CLOSE",
                                    "0.32, 0.32, 0.38, 1", label_size=18)
ui.text("FooterNote", id_start_panel_tr, 12, 404, 260, 36,
        "Camera flyby runs in a separate benchmark room. FPS depends on the PC you built.",
        color=C_TEXT_DIM, size=9, style=FONT_ITALIC, align=ALIGN_MIDDLE_LEFT)

# 2. Title Bar IDs
id_title_go = b.next_id()
id_title_tr = b.next_id()
id_title_cr = b.next_id()
id_title_img = b.next_id()
id_title_drag = b.next_id()

id_title_txt_go = b.next_id()
id_title_txt_tr = b.next_id()
id_title_txt_cr = b.next_id()
id_title_txt = b.next_id()

id_title_close_go = b.next_id()
id_title_close_tr = b.next_id()
id_title_close_cr = b.next_id()
id_title_close_img = b.next_id()
id_title_close_btn = b.next_id()
id_title_close_lbl_go = b.next_id()
id_title_close_lbl_tr = b.next_id()
id_title_close_lbl_cr = b.next_id()
id_title_close_lbl_txt = b.next_id()

id_title_max_go = b.next_id()
id_title_max_tr = b.next_id()
id_title_max_cr = b.next_id()
id_title_max_img = b.next_id()
id_title_max_btn = b.next_id()

id_title_min_go = b.next_id()
id_title_min_tr = b.next_id()
id_title_min_cr = b.next_id()
id_title_min_img = b.next_id()
id_title_min_btn = b.next_id()

# 3. Test Panel IDs
id_test_panel_go = b.next_id()
id_test_panel_tr = b.next_id()

id_viewport_go = b.next_id()
id_viewport_tr = b.next_id()
id_viewport_cr = b.next_id()
id_viewport_raw = b.next_id()

id_top_bar_go = b.next_id()
id_top_bar_tr = b.next_id()
id_top_bar_cr = b.next_id()
id_top_bar_img = b.next_id()

id_fps_text_go = b.next_id()
id_fps_text_tr = b.next_id()
id_fps_text_cr = b.next_id()
id_fps_text = b.next_id()

id_scene_text_go = b.next_id()
id_scene_text_tr = b.next_id()
id_scene_text_cr = b.next_id()
id_scene_text = b.next_id()

id_bottom_bar_go = b.next_id()
id_bottom_bar_tr = b.next_id()

id_progress_slider_go = b.next_id()
id_progress_slider_tr = b.next_id()
id_progress_slider = b.next_id()
id_progress_bg_go = b.next_id()
id_progress_bg_tr = b.next_id()
id_progress_bg_cr = b.next_id()
id_progress_bg_img = b.next_id()
id_progress_fill_area_go = b.next_id()
id_progress_fill_area_tr = b.next_id()
id_progress_fill_go = b.next_id()
id_progress_fill_tr = b.next_id()
id_progress_fill_cr = b.next_id()
id_progress_fill_img = b.next_id()

# 4. Results Panel IDs
id_res_panel_go = b.next_id()
id_res_panel_tr = b.next_id()
id_res_panel_cr = b.next_id()
id_res_panel_img = b.next_id()

id_res_hdr_go = b.next_id()
id_res_hdr_tr = b.next_id()
id_res_hdr_cr = b.next_id()
id_res_hdr_txt = b.next_id()

# Score Circle
id_circle_bg_go = b.next_id()
id_circle_bg_tr = b.next_id()
id_circle_bg_cr = b.next_id()
id_circle_bg_img = b.next_id()

id_circle_fill_go = b.next_id()
id_circle_fill_tr = b.next_id()
id_circle_fill_cr = b.next_id()
id_circle_fill_img = b.next_id()

id_circle_score_go = b.next_id()
id_circle_score_tr = b.next_id()
id_circle_score_cr = b.next_id()
id_circle_score_txt = b.next_id()

id_circle_lbl_go = b.next_id()
id_circle_lbl_tr = b.next_id()
id_circle_lbl_cr = b.next_id()
id_circle_lbl_txt = b.next_id()

# Categories (4 Sliders + Texts)
cat_ids = []
for i in range(4):
    cat_ids.append({
        'row_go': b.next_id(),
        'row_tr': b.next_id(),
        'lbl_go': b.next_id(),
        'lbl_tr': b.next_id(),
        'lbl_cr': b.next_id(),
        'lbl_txt': b.next_id(),
        'slider_go': b.next_id(),
        'slider_tr': b.next_id(),
        'slider': b.next_id(),
        'sbg_go': b.next_id(),
        'sbg_tr': b.next_id(),
        'sbg_cr': b.next_id(),
        'sbg_img': b.next_id(),
        'sarea_go': b.next_id(),
        'sarea_tr': b.next_id(),
        'sfill_go': b.next_id(),
        'sfill_tr': b.next_id(),
        'sfill_cr': b.next_id(),
        'sfill_img': b.next_id(),
        'val_go': b.next_id(),
        'val_tr': b.next_id(),
        'val_cr': b.next_id(),
        'val_txt': b.next_id(),
    })

# Results Buttons
id_btn_run_go = b.next_id()
id_btn_run_tr = b.next_id()
id_btn_run_cr = b.next_id()
id_btn_run_img = b.next_id()
id_btn_run = b.next_id()
id_btn_run_txt_go = b.next_id()
id_btn_run_txt_tr = b.next_id()
id_btn_run_txt_cr = b.next_id()
id_btn_run_txt = b.next_id()

id_btn_close_go = b.next_id()
id_btn_close_tr = b.next_id()
id_btn_close_cr = b.next_id()
id_btn_close_img = b.next_id()
id_btn_close = b.next_id()
id_btn_close_txt_go = b.next_id()
id_btn_close_txt_tr = b.next_id()
id_btn_close_txt_cr = b.next_id()
id_btn_close_txt = b.next_id()

# --- ROOT ---
b.add(f"""
--- !u!1 &{id_root_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_root_tr}}}
  - component: {{fileID: {id_root_cr}}}
  - component: {{fileID: {id_root_img}}}
  - component: {{fileID: {id_root_app}}}
  m_Layer: 5
  m_Name: 3DMork
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_root_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_root_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_start_panel_tr}}}
  - {{fileID: {id_title_tr}}}
  - {{fileID: {id_test_panel_tr}}}
  - {{fileID: {id_res_panel_tr}}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 850, y: 520}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_root_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_root_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_root_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_root_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.08, g: 0.08, b: 0.1, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!114 &{id_root_app}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_root_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {THREEDMORK_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  appName: 3DMork
  icon: {{fileID: 21300000, guid: {APP_ICON_GUID}, type: 2}}
  fileName: 
  fileIcon: {{fileID: 0}}
  size: 650
  MenuBar: []
  maximizeSprite: {{fileID: 21300000, guid: {MAX_SPRITE_GUID}, type: 2}}
  normalSprite: {{fileID: 0}}
  windowState: {{fileID: {id_title_max_img}}}
  startPanel: {{fileID: {id_start_panel_go}}}
  testPanel: {{fileID: {id_test_panel_go}}}
  resultsPanel: {{fileID: {id_res_panel_go}}}
  buttonStart: {{fileID: {id_btn_start}}}
  buttonStartClose: {{fileID: {id_btn_start_close}}}
  buttonMenu: {{fileID: {id_btn_menu}}}
  leaderboardRank:
{chr(10).join("  - {fileID: %s}" % t for t in lb_rank)}
  leaderboardName:
{chr(10).join("  - {fileID: %s}" % t for t in lb_name)}
  leaderboardCpu:
{chr(10).join("  - {fileID: %s}" % t for t in lb_cpu)}
  leaderboardGpu:
{chr(10).join("  - {fileID: %s}" % t for t in lb_gpu)}
  leaderboardScore:
{chr(10).join("  - {fileID: %s}" % t for t in lb_score)}
  leaderboardFps:
{chr(10).join("  - {fileID: %s}" % t for t in lb_fps)}
  leaderboardSelfRank: {{fileID: {lb_self_rank}}}
  leaderboardSelfName: {{fileID: {lb_self_name}}}
  leaderboardSelfSpec: {{fileID: {lb_self_spec}}}
  leaderboardSelfScore: {{fileID: {lb_self_score}}}
  leaderboardSelfFps: {{fileID: {lb_self_fps}}}
  leaderboardAverage: {{fileID: {lb_average}}}
  historyIndex:
{chr(10).join("  - {fileID: %s}" % t for t in hist_index)}
  historyDate:
{chr(10).join("  - {fileID: %s}" % t for t in hist_date)}
  historyScore:
{chr(10).join("  - {fileID: %s}" % t for t in hist_score)}
  historyFps:
{chr(10).join("  - {fileID: %s}" % t for t in hist_fps)}
  historyBest: {{fileID: {hist_best}}}
  historyEmpty: {{fileID: {hist_empty}}}
  hardwareTitle: {{fileID: {hw_title}}}
  hardwareCpu: {{fileID: {hw_cpu}}}
  hardwareGpu: {{fileID: {hw_gpu}}}
  hardwareRam: {{fileID: {hw_ram}}}
  hardwareBoard: {{fileID: {hw_board}}}
  viewportImage: {{fileID: {id_viewport_raw}}}
  fpsText: {{fileID: {id_fps_text}}}
  sceneInfoText: {{fileID: {id_scene_text}}}
  testProgressBar: {{fileID: {id_progress_slider}}}
  textTotalScore: {{fileID: {id_circle_score_txt}}}
  markCircle: {{fileID: {id_circle_fill_img}}}
  marks:
  - {{fileID: {cat_ids[0]['slider']}}}
  - {{fileID: {cat_ids[1]['slider']}}}
  - {{fileID: {cat_ids[2]['slider']}}}
  - {{fileID: {cat_ids[3]['slider']}}}
  text_marks:
  - {{fileID: {cat_ids[0]['val_txt']}}}
  - {{fileID: {cat_ids[1]['val_txt']}}}
  - {{fileID: {cat_ids[2]['val_txt']}}}
  - {{fileID: {cat_ids[3]['val_txt']}}}
  buttonClose: {{fileID: {id_btn_close}}}
  buttonRun: {{fileID: {id_btn_run}}}
  stagePrefab: {{fileID: 6000000000000001, guid: d6f6d07dffc84a1ba33c9f9032a7a0e1, type: 3}}
  stageSceneName: 
  stageSpawnPosition: {{x: 0, y: -2500, z: 0}}
  flybyPhases: []
  waypointsRoot: {{fileID: 0}}
  rotationSmoothing: 5.5
  cameraFov: 65
""")

# --- TITLE BAR ---
b.add(f"""
--- !u!1 &{id_title_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_title_tr}}}
  - component: {{fileID: {id_title_cr}}}
  - component: {{fileID: {id_title_img}}}
  - component: {{fileID: {id_title_drag}}}
  m_Layer: 5
  m_Name: Title
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_title_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_title_txt_tr}}}
  - {{fileID: {id_title_min_tr}}}
  - {{fileID: {id_title_max_tr}}}
  - {{fileID: {id_title_close_tr}}}
  m_Father: {{fileID: {id_root_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -18}}
  m_SizeDelta: {{x: 0, y: 36}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_title_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_title_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.12, g: 0.12, b: 0.15, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!114 &{id_title_drag}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {WINDOWDRAG_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
""")

# Title text
b.add(f"""
--- !u!1 &{id_title_txt_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_title_txt_tr}}}
  - component: {{fileID: {id_title_txt_cr}}}
  - component: {{fileID: {id_title_txt}}}
  m_Layer: 5
  m_Name: Text
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_title_txt_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_txt_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_title_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 14, y: 0}}
  m_SizeDelta: {{x: -120, y: 0}}
  m_Pivot: {{x: 0, y: 0.5}}
--- !u!222 &{id_title_txt_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_txt_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_title_txt}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_txt_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.65, b: 0.15, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 16
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 3
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: 3DMork - Realtime 3D Benchmark
""")

# Title Buttons
b.add(f"""
--- !u!1 &{id_title_min_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_title_min_tr}}}
  - component: {{fileID: {id_title_min_cr}}}
  - component: {{fileID: {id_title_min_img}}}
  - component: {{fileID: {id_title_min_btn}}}
  m_Layer: 5
  m_Name: Minimize
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_title_min_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_min_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_title_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 1, y: 0.5}}
  m_AnchorMax: {{x: 1, y: 0.5}}
  m_AnchoredPosition: {{x: -76, y: 0}}
  m_SizeDelta: {{x: 28, y: 28}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_title_min_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_min_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_title_min_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_min_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 0.8}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 21300000, guid: {MIN_SPRITE_GUID}, type: 2}}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
--- !u!114 &{id_title_min_btn}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_min_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {BUTTON_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.9, g: 0.9, b: 0.9, a: 1}}
    m_PressedColor: {{r: 0.7, g: 0.7, b: 0.7, a: 1}}
    m_SelectedColor: {{r: 0.9, g: 0.9, b: 0.9, a: 1}}
    m_DisabledColor: {{r: 0.5, g: 0.5, b: 0.5, a: 0.5}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {id_title_min_img}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: {id_root_app}}}
        m_TargetAssemblyTypeName: 
        m_MethodName: Minimize
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument: 
          m_BoolArgument: 0
        m_CallState: 2
--- !u!1 &{id_title_max_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_title_max_tr}}}
  - component: {{fileID: {id_title_max_cr}}}
  - component: {{fileID: {id_title_max_img}}}
  - component: {{fileID: {id_title_max_btn}}}
  m_Layer: 5
  m_Name: Maximize
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_title_max_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_max_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_title_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 1, y: 0.5}}
  m_AnchorMax: {{x: 1, y: 0.5}}
  m_AnchoredPosition: {{x: -44, y: 0}}
  m_SizeDelta: {{x: 28, y: 28}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_title_max_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_max_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_title_max_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_max_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 0.8}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 21300000, guid: {MAX_SPRITE_GUID}, type: 2}}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
--- !u!114 &{id_title_max_btn}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_max_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {BUTTON_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.9, g: 0.9, b: 0.9, a: 1}}
    m_PressedColor: {{r: 0.7, g: 0.7, b: 0.7, a: 1}}
    m_SelectedColor: {{r: 0.9, g: 0.9, b: 0.9, a: 1}}
    m_DisabledColor: {{r: 0.5, g: 0.5, b: 0.5, a: 0.5}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {id_title_max_img}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: {id_root_app}}}
        m_TargetAssemblyTypeName: 
        m_MethodName: Maximize
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument: 
          m_BoolArgument: 0
        m_CallState: 2
--- !u!1 &{id_title_close_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_title_close_tr}}}
  - component: {{fileID: {id_title_close_cr}}}
  - component: {{fileID: {id_title_close_img}}}
  - component: {{fileID: {id_title_close_btn}}}
  m_Layer: 5
  m_Name: Close
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_title_close_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_close_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_title_close_lbl_tr}}}
  m_Father: {{fileID: {id_title_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 1, y: 0.5}}
  m_AnchorMax: {{x: 1, y: 0.5}}
  m_AnchoredPosition: {{x: -14, y: 0}}
  m_SizeDelta: {{x: 28, y: 28}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_title_close_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_close_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_title_close_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_close_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.8, g: 0.2, b: 0.2, a: 0.8}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!114 &{id_title_close_btn}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_close_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {BUTTON_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 0.8, g: 0.2, b: 0.2, a: 1}}
    m_HighlightedColor: {{r: 1, g: 0.25, b: 0.25, a: 1}}
    m_PressedColor: {{r: 0.6, g: 0.15, b: 0.15, a: 1}}
    m_SelectedColor: {{r: 0.8, g: 0.2, b: 0.2, a: 1}}
    m_DisabledColor: {{r: 0.5, g: 0.5, b: 0.5, a: 0.5}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {id_title_close_img}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: {id_root_app}}}
        m_TargetAssemblyTypeName: 
        m_MethodName: Close
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument: 
          m_BoolArgument: 0
        m_CallState: 2
--- !u!1 &{id_title_close_lbl_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_title_close_lbl_tr}}}
  - component: {{fileID: {id_title_close_lbl_cr}}}
  - component: {{fileID: {id_title_close_lbl_txt}}}
  m_Layer: 5
  m_Name: Text
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_title_close_lbl_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_close_lbl_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_title_close_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_title_close_lbl_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_close_lbl_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_title_close_lbl_txt}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_title_close_lbl_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 16
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 4
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: X
""")

# --- TEST PANEL (3D Viewport + Overlays) ---
b.add(f"""
--- !u!1 &{id_test_panel_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_test_panel_tr}}}
  m_Layer: 5
  m_Name: TestPanel
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 0
--- !u!224 &{id_test_panel_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_test_panel_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_viewport_tr}}}
  - {{fileID: {id_top_bar_tr}}}
  - {{fileID: {id_bottom_bar_tr}}}
  m_Father: {{fileID: {id_root_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -18}}
  m_SizeDelta: {{x: 0, y: -36}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!1 &{id_viewport_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_viewport_tr}}}
  - component: {{fileID: {id_viewport_cr}}}
  - component: {{fileID: {id_viewport_raw}}}
  m_Layer: 5
  m_Name: Viewport
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_viewport_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_viewport_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_test_panel_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_viewport_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_viewport_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_viewport_raw}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_viewport_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {RAWIMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Texture: {{fileID: 0}}
  m_UVRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
""")

# Top Bar (FPS text + Scene text)
b.add(f"""
--- !u!1 &{id_top_bar_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_top_bar_tr}}}
  - component: {{fileID: {id_top_bar_cr}}}
  - component: {{fileID: {id_top_bar_img}}}
  m_Layer: 5
  m_Name: TopOverlay
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_top_bar_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_top_bar_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_fps_text_tr}}}
  - {{fileID: {id_scene_text_tr}}}
  m_Father: {{fileID: {id_test_panel_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -24}}
  m_SizeDelta: {{x: 0, y: 48}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_top_bar_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_top_bar_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_top_bar_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_top_bar_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0, g: 0, b: 0, a: 0.75}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!1 &{id_fps_text_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_fps_text_tr}}}
  - component: {{fileID: {id_fps_text_cr}}}
  - component: {{fileID: {id_fps_text}}}
  m_Layer: 5
  m_Name: FpsText
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_fps_text_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_fps_text_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_top_bar_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 1}}
  m_AnchoredPosition: {{x: 100, y: 0}}
  m_SizeDelta: {{x: 170, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_fps_text_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_fps_text_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_fps_text}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_fps_text_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.25, g: 1, b: 0.25, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 26
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 3
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: 'FPS: --'
--- !u!1 &{id_scene_text_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_scene_text_tr}}}
  - component: {{fileID: {id_scene_text_cr}}}
  - component: {{fileID: {id_scene_text}}}
  m_Layer: 5
  m_Name: SceneInfoText
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_scene_text_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_scene_text_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_top_bar_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.3, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: -20, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 1, y: 0.5}}
--- !u!222 &{id_scene_text_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_scene_text_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_scene_text}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_scene_text_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 0.95}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 18
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 5
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: 3DMork 3D Benchmark
""")

# Bottom bar (Progress slider)
b.add(f"""
--- !u!1 &{id_bottom_bar_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_bottom_bar_tr}}}
  m_Layer: 5
  m_Name: BottomOverlay
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_bottom_bar_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_bottom_bar_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_progress_slider_tr}}}
  m_Father: {{fileID: {id_test_panel_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 18}}
  m_SizeDelta: {{x: -30, y: 22}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!1 &{id_progress_slider_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_progress_slider_tr}}}
  - component: {{fileID: {id_progress_slider}}}
  m_Layer: 5
  m_Name: ProgressBar
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_progress_slider_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_progress_slider_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_progress_bg_tr}}}
  - {{fileID: {id_progress_fill_area_tr}}}
  m_Father: {{fileID: {id_bottom_bar_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!114 &{id_progress_slider}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_progress_slider_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SLIDER_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 0
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_PressedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_SelectedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_DisabledColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 0
  m_TargetGraphic: {{fileID: 0}}
  m_FillRect: {{fileID: {id_progress_fill_tr}}}
  m_HandleRect: {{fileID: 0}}
  m_Direction: 0
  m_MinValue: 0
  m_MaxValue: 1
  m_WholeNumbers: 0
  m_Value: 0
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{id_progress_bg_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_progress_bg_tr}}}
  - component: {{fileID: {id_progress_bg_cr}}}
  - component: {{fileID: {id_progress_bg_img}}}
  m_Layer: 5
  m_Name: Background
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_progress_bg_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_progress_bg_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_progress_slider_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_progress_bg_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_progress_bg_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_progress_bg_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_progress_bg_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0, g: 0, b: 0, a: 0.6}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!1 &{id_progress_fill_area_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_progress_fill_area_tr}}}
  m_Layer: 5
  m_Name: Fill Area
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_progress_fill_area_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_progress_fill_area_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_progress_fill_tr}}}
  m_Father: {{fileID: {id_progress_slider_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!1 &{id_progress_fill_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_progress_fill_tr}}}
  - component: {{fileID: {id_progress_fill_cr}}}
  - component: {{fileID: {id_progress_fill_img}}}
  m_Layer: 5
  m_Name: Fill
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_progress_fill_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_progress_fill_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_progress_fill_area_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_progress_fill_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_progress_fill_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_progress_fill_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_progress_fill_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.5, b: 0, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
""")

# Кнопка "в меню" на экране результатов (возврат к стартовому экрану)
id_btn_menu, _, id_btn_menu_tr = ui.button("ButtonMenu", id_res_panel_tr, -140, 40, 160, 38,
                                           "TO MENU", "0.32, 0.32, 0.38, 1", label_size=16,
                                           anchor="bottomcenter", track=False, ids=MENU_BTN_IDS)

# --- RESULTS PANEL ---
b.add(f"""
--- !u!1 &{id_res_panel_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_res_panel_tr}}}
  - component: {{fileID: {id_res_panel_cr}}}
  - component: {{fileID: {id_res_panel_img}}}
  m_Layer: 5
  m_Name: ResultsPanel
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 0
--- !u!224 &{id_res_panel_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_res_panel_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_res_hdr_tr}}}
  - {{fileID: {id_circle_bg_tr}}}
  - {{fileID: {cat_ids[0]['row_tr']}}}
  - {{fileID: {cat_ids[1]['row_tr']}}}
  - {{fileID: {cat_ids[2]['row_tr']}}}
  - {{fileID: {cat_ids[3]['row_tr']}}}
  - {{fileID: {id_btn_run_tr}}}
  - {{fileID: {id_btn_close_tr}}}
  - {{fileID: {id_btn_menu_tr}}}
  m_Father: {{fileID: {id_root_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -18}}
  m_SizeDelta: {{x: 0, y: -36}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_res_panel_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_res_panel_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_res_panel_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_res_panel_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.08, g: 0.08, b: 0.1, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!1 &{id_res_hdr_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_res_hdr_tr}}}
  - component: {{fileID: {id_res_hdr_cr}}}
  - component: {{fileID: {id_res_hdr_txt}}}
  m_Layer: 5
  m_Name: Header
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_res_hdr_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_res_hdr_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_res_panel_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -30}}
  m_SizeDelta: {{x: 0, y: 40}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_res_hdr_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_res_hdr_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_res_hdr_txt}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_res_hdr_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.65, b: 0.1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 24
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 4
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: 3DMORK BENCHMARK RESULTS
""")

# Score Circle & Texts
b.add(f"""
--- !u!1 &{id_circle_bg_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_circle_bg_tr}}}
  - component: {{fileID: {id_circle_bg_cr}}}
  - component: {{fileID: {id_circle_bg_img}}}
  m_Layer: 5
  m_Name: ScoreCircle
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_circle_bg_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_bg_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_circle_fill_tr}}}
  - {{fileID: {id_circle_score_tr}}}
  - {{fileID: {id_circle_lbl_tr}}}
  m_Father: {{fileID: {id_res_panel_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: -240, y: 20}}
  m_SizeDelta: {{x: 230, y: 230}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_circle_bg_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_bg_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_circle_bg_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_bg_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.18, g: 0.18, b: 0.22, a: 0.8}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 21300000, guid: {CIRCLE_SPRITE_GUID}, type: 2}}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
--- !u!1 &{id_circle_fill_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_circle_fill_tr}}}
  - component: {{fileID: {id_circle_fill_cr}}}
  - component: {{fileID: {id_circle_fill_img}}}
  m_Layer: 5
  m_Name: FillCircle
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_circle_fill_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_fill_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_circle_bg_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_circle_fill_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_fill_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_circle_fill_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_fill_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.6, b: 0, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 21300000, guid: {CIRCLE_SPRITE_GUID}, type: 2}}
  m_Type: 3
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 0
  m_FillClockwise: 1
  m_FillOrigin: 2
--- !u!1 &{id_circle_score_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_circle_score_tr}}}
  - component: {{fileID: {id_circle_score_cr}}}
  - component: {{fileID: {id_circle_score_txt}}}
  m_Layer: 5
  m_Name: ScoreText
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_circle_score_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_score_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_circle_bg_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0.35}}
  m_AnchorMax: {{x: 1, y: 0.75}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_circle_score_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_score_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_circle_score_txt}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_score_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 44
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 50
    m_Alignment: 4
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: 0
--- !u!1 &{id_circle_lbl_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_circle_lbl_tr}}}
  - component: {{fileID: {id_circle_lbl_cr}}}
  - component: {{fileID: {id_circle_lbl_txt}}}
  m_Layer: 5
  m_Name: ScoreLabel
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_circle_lbl_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_lbl_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_circle_bg_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0.18}}
  m_AnchorMax: {{x: 1, y: 0.35}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_circle_lbl_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_lbl_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_circle_lbl_txt}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_circle_lbl_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.65, b: 0.1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 14
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 8
    m_MaxSize: 24
    m_Alignment: 4
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: 3DMORK SCORE
""")

# 4 Category Rows
cat_titles = [
    "Graphics Score",
    "Physics / CPU Score",
    "Memory Bandwidth",
    "Average Frame Rate"
]
cat_y_offsets = [95, 45, -5, -55]

for idx in range(4):
    c = cat_ids[idx]
    title = cat_titles[idx]
    y_off = cat_y_offsets[idx]

    b.add(f"""
--- !u!1 &{c['row_go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {c['row_tr']}}}
  m_Layer: 5
  m_Name: Category ({idx})
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{c['row_tr']}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['row_go']}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {c['lbl_tr']}}}
  - {{fileID: {c['slider_tr']}}}
  - {{fileID: {c['val_tr']}}}
  m_Father: {{fileID: {id_res_panel_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: 140, y: {y_off}}}
  m_SizeDelta: {{x: 440, y: 38}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!1 &{c['lbl_go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {c['lbl_tr']}}}
  - component: {{fileID: {c['lbl_cr']}}}
  - component: {{fileID: {c['lbl_txt']}}}
  m_Layer: 5
  m_Name: Label
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{c['lbl_tr']}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['lbl_go']}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {c['row_tr']}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0.5}}
  m_AnchorMax: {{x: 0, y: 0.5}}
  m_AnchoredPosition: {{x: 80, y: 0}}
  m_SizeDelta: {{x: 160, y: 32}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{c['lbl_cr']}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['lbl_go']}}}
  m_CullTransparentMesh: 1
--- !u!114 &{c['lbl_txt']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['lbl_go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.85, g: 0.85, b: 0.9, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 16
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 3
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: '{title}'
--- !u!1 &{c['slider_go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {c['slider_tr']}}}
  - component: {{fileID: {c['slider']}}}
  m_Layer: 5
  m_Name: Slider
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{c['slider_tr']}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['slider_go']}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {c['sbg_tr']}}}
  - {{fileID: {c['sarea_tr']}}}
  m_Father: {{fileID: {c['row_tr']}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: 55, y: 0}}
  m_SizeDelta: {{x: 180, y: 18}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!114 &{c['slider']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['slider_go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SLIDER_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 0
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_PressedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_SelectedColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_DisabledColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 0
  m_TargetGraphic: {{fileID: 0}}
  m_FillRect: {{fileID: {c['sfill_tr']}}}
  m_HandleRect: {{fileID: 0}}
  m_Direction: 0
  m_MinValue: 0
  m_MaxValue: 5000
  m_WholeNumbers: 0
  m_Value: 0
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{c['sbg_go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {c['sbg_tr']}}}
  - component: {{fileID: {c['sbg_cr']}}}
  - component: {{fileID: {c['sbg_img']}}}
  m_Layer: 5
  m_Name: Background
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{c['sbg_tr']}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['sbg_go']}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {c['slider_tr']}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{c['sbg_cr']}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['sbg_go']}}}
  m_CullTransparentMesh: 1
--- !u!114 &{c['sbg_img']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['sbg_go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.15, g: 0.15, b: 0.2, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!1 &{c['sarea_go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {c['sarea_tr']}}}
  m_Layer: 5
  m_Name: Fill Area
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{c['sarea_tr']}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['sarea_go']}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {c['sfill_tr']}}}
  m_Father: {{fileID: {c['slider_tr']}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!1 &{c['sfill_go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {c['sfill_tr']}}}
  - component: {{fileID: {c['sfill_cr']}}}
  - component: {{fileID: {c['sfill_img']}}}
  m_Layer: 5
  m_Name: Fill
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{c['sfill_tr']}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['sfill_go']}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {c['sarea_tr']}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{c['sfill_cr']}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['sfill_go']}}}
  m_CullTransparentMesh: 1
--- !u!114 &{c['sfill_img']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['sfill_go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.55, b: 0, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!1 &{c['val_go']}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {c['val_tr']}}}
  - component: {{fileID: {c['val_cr']}}}
  - component: {{fileID: {c['val_txt']}}}
  m_Layer: 5
  m_Name: Value
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{c['val_tr']}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['val_go']}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {c['row_tr']}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 1, y: 0.5}}
  m_AnchorMax: {{x: 1, y: 0.5}}
  m_AnchoredPosition: {{x: -45, y: 0}}
  m_SizeDelta: {{x: 90, y: 32}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{c['val_cr']}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['val_go']}}}
  m_CullTransparentMesh: 1
--- !u!114 &{c['val_txt']}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {c['val_go']}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.7, b: 0.15, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 18
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 5
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: 0
""")

# Buttons: Run Again & Close
b.add(f"""
--- !u!1 &{id_btn_run_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_btn_run_tr}}}
  - component: {{fileID: {id_btn_run_cr}}}
  - component: {{fileID: {id_btn_run_img}}}
  - component: {{fileID: {id_btn_run}}}
  m_Layer: 5
  m_Name: ButtonRun
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_btn_run_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_run_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_btn_run_txt_tr}}}
  m_Father: {{fileID: {id_res_panel_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0}}
  m_AnchorMax: {{x: 0.5, y: 0}}
  m_AnchoredPosition: {{x: 40, y: 40}}
  m_SizeDelta: {{x: 160, y: 38}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_btn_run_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_run_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_btn_run_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_run_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.5, b: 0, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!114 &{id_btn_run}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_run_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {BUTTON_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 0.5, b: 0, a: 1}}
    m_HighlightedColor: {{r: 1, g: 0.65, b: 0.15, a: 1}}
    m_PressedColor: {{r: 0.8, g: 0.4, b: 0, a: 1}}
    m_SelectedColor: {{r: 1, g: 0.5, b: 0, a: 1}}
    m_DisabledColor: {{r: 0.5, g: 0.5, b: 0.5, a: 0.5}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {id_btn_run_img}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{id_btn_run_txt_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_btn_run_txt_tr}}}
  - component: {{fileID: {id_btn_run_txt_cr}}}
  - component: {{fileID: {id_btn_run_txt}}}
  m_Layer: 5
  m_Name: Text
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_btn_run_txt_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_run_txt_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_btn_run_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_btn_run_txt_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_run_txt_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_btn_run_txt}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_run_txt_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 16
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 4
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: Run Again
--- !u!1 &{id_btn_close_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_btn_close_tr}}}
  - component: {{fileID: {id_btn_close_cr}}}
  - component: {{fileID: {id_btn_close_img}}}
  - component: {{fileID: {id_btn_close}}}
  m_Layer: 5
  m_Name: ButtonClose
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_btn_close_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_close_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {id_btn_close_txt_tr}}}
  m_Father: {{fileID: {id_res_panel_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0}}
  m_AnchorMax: {{x: 0.5, y: 0}}
  m_AnchoredPosition: {{x: 220, y: 40}}
  m_SizeDelta: {{x: 160, y: 38}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_btn_close_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_close_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_btn_close_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_close_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.25, g: 0.25, b: 0.3, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
--- !u!114 &{id_btn_close}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_close_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {BUTTON_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 0.25, g: 0.25, b: 0.3, a: 1}}
    m_HighlightedColor: {{r: 0.35, g: 0.35, b: 0.42, a: 1}}
    m_PressedColor: {{r: 0.2, g: 0.2, b: 0.25, a: 1}}
    m_SelectedColor: {{r: 0.25, g: 0.25, b: 0.3, a: 1}}
    m_DisabledColor: {{r: 0.5, g: 0.5, b: 0.5, a: 0.5}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {id_btn_close_img}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: {id_root_app}}}
        m_TargetAssemblyTypeName: 
        m_MethodName: Close
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument: 
          m_BoolArgument: 0
        m_CallState: 2
--- !u!1 &{id_btn_close_txt_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {id_btn_close_txt_tr}}}
  - component: {{fileID: {id_btn_close_txt_cr}}}
  - component: {{fileID: {id_btn_close_txt}}}
  m_Layer: 5
  m_Name: Text
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{id_btn_close_txt_tr}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_close_txt_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {id_btn_close_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{id_btn_close_txt_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_close_txt_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{id_btn_close_txt}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {id_btn_close_txt_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TEXT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {{fileID: 12800000, guid: {FONT_GUID}, type: 3}}
    m_FontSize: 16
    m_FontStyle: 1
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: 40
    m_Alignment: 4
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: Close
""")

ui.finalize()

full_yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + "\n".join(b.chunks) + "\n"
out_path = "Assets/Resources/apps/3DMork.prefab"
with open(out_path, "w", encoding="utf-8") as f:
    f.write(full_yaml)

print(f"Successfully wrote {out_path} ({len(full_yaml)} bytes, {len(b.chunks)} blocks)")
