/**
 * Stáhne Fluent UI System Icons (Microsoft, MIT licence) z GitHubu
 * a konvertuje SVG → PNG 16×16 přes sharp.
 *
 * Prerekvizity: npm install sharp
 */

import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import sharp from "sharp";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT_DIR = path.resolve(__dirname, "..", "..", "EDControls", "dotnet", "EDIcons", "png");
const BASE_URL = "https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets";

const ICONS = {
  "new.png":              "Document Add/SVG/ic_fluent_document_add_16_regular.svg",
  "open.png":             "Folder Open/SVG/ic_fluent_folder_open_16_regular.svg",
  "save.png":             "Save/SVG/ic_fluent_save_16_regular.svg",
  "print.png":            "Print/SVG/ic_fluent_print_16_regular.svg",
  "undo.png":             "Arrow Undo/SVG/ic_fluent_arrow_undo_16_regular.svg",
  "redo.png":             "Arrow Redo/SVG/ic_fluent_arrow_redo_16_regular.svg",
  "add_section.png":      "Board Split/SVG/ic_fluent_board_split_16_regular.svg",
  "add_element.png":      "Add Square/SVG/ic_fluent_add_square_16_regular.svg",
  "text_element.png":     "Text T/SVG/ic_fluent_text_t_16_regular.svg",
  "rect_element.png":     "Square/SVG/ic_fluent_square_16_regular.svg",
  "line_element.png":     "Line Horizontal 1/SVG/ic_fluent_line_horizontal_1_16_regular.svg",
  "image_element.png":    "Image/SVG/ic_fluent_image_16_regular.svg",
  "table_element.png":    "Table Simple/SVG/ic_fluent_table_simple_16_regular.svg",
  "qr_code.png":          "Scan Dash/SVG/ic_fluent_scan_dash_16_regular.svg",
  "barcode.png":          "Barcode Scanner/SVG/ic_fluent_barcode_scanner_16_regular.svg",
  "stop_list.png":        "Vehicle Bus/SVG/ic_fluent_vehicle_bus_16_regular.svg",
  "departure_grid.png":   "Clock/SVG/ic_fluent_clock_16_regular.svg",
  "notes_legend.png":     "Notepad/SVG/ic_fluent_notepad_16_regular.svg",
  "z_forward.png":        "Arrow Sort Up/SVG/ic_fluent_arrow_sort_up_16_regular.svg",
  "z_backward.png":       "Arrow Sort Down/SVG/ic_fluent_arrow_sort_down_16_regular.svg",
  "z_front.png":          "Arrow Step In/SVG/ic_fluent_arrow_step_in_16_regular.svg",
  "z_back.png":           "Arrow Step Out/SVG/ic_fluent_arrow_step_out_16_regular.svg",
  "align_left.png":       "Align Left/SVG/ic_fluent_align_left_16_regular.svg",
  "align_right.png":      "Align Right/SVG/ic_fluent_align_right_16_regular.svg",
  "align_center_h.png":   "Align Center Horizontal/SVG/ic_fluent_align_center_horizontal_16_regular.svg",
  "align_top.png":        "Align Top/SVG/ic_fluent_align_top_16_regular.svg",
  "align_bottom.png":     "Align Bottom/SVG/ic_fluent_align_bottom_16_regular.svg",
  "align_center_v.png":   "Align Center Vertical/SVG/ic_fluent_align_center_vertical_16_regular.svg",
  "distribute_h.png":     "Align Stretch Horizontal/SVG/ic_fluent_align_stretch_horizontal_16_regular.svg",
  "distribute_v.png":     "Align Stretch Vertical/SVG/ic_fluent_align_stretch_vertical_16_regular.svg",
  "grid.png":             "Grid/SVG/ic_fluent_grid_16_regular.svg",
  "snap.png":             "Target/SVG/ic_fluent_target_16_regular.svg",
  "eye.png":              "Eye/SVG/ic_fluent_eye_16_regular.svg",
};

fs.mkdirSync(OUT_DIR, { recursive: true });

let ok = 0, fail = 0;

for (const [filename, svgPath] of Object.entries(ICONS)) {
  const url = `${BASE_URL}/${svgPath}`;
  const outPath = path.join(OUT_DIR, filename);

  try {
    const resp = await fetch(url);
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
    const svgBuffer = Buffer.from(await resp.arrayBuffer());

    await sharp(svgBuffer, { density: 150 })
      .resize(16, 16)
      .png()
      .toFile(outPath);

    console.log(`  OK  ${filename}`);
    ok++;
  } catch (e) {
    console.log(`  FAIL ${filename}: ${e.message}`);
    fail++;
  }
}

console.log(`\nHotovo: ${ok} OK, ${fail} FAIL`);
