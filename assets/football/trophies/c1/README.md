# UEFA Champions League Trophy (C1) - 2.5D Pixel Art Sprite Set

Bộ sprite cúp C1 phong cách **2.5D Pixel Art** (lấy cảm hứng từ phong cách Nintendo DS - Pokémon Gen 4/5), được xử lý nền trong suốt (transparent PNG) và căn chỉnh trục quay (rotation axis) đồng bộ để làm hiệu ứng xoay 360 độ mượt mà.

---

## 📁 Cấu trúc thư mục

```text
assets/football/trophies/c1/
├── c1_trophy_rotation_strip.png     # Spritesheet dạng dải ngang (2048 x 384, 8 frames x 256px)
├── c1_trophy_rotation_grid.png      # Spritesheet dạng lưới 2x4 (1024 x 768, 4 cols x 2 rows)
├── c1_trophy_rotation_preview.gif   # File GIF chạy thử animation xoay 360°
├── c1_trophy_01_front.png           # Frame 1: Chính diện (Front view - 0°)
├── c1_trophy_02_front_right.png     # Frame 2: Chếch trước bên phải (Front-Right - 45°)
├── c1_trophy_03_right.png           # Frame 3: Cạnh bên phải (Right profile - 90°)
├── c1_trophy_04_back_right.png      # Frame 4: Chếch sau bên phải (Back-Right - 135°)
├── c1_trophy_05_back.png            # Frame 5: Phía sau (Back view - 180°)
├── c1_trophy_06_back_left.png       # Frame 6: Chếch sau bên trái (Back-Left - 225°)
├── c1_trophy_07_left.png            # Frame 7: Cạnh bên trái (Left profile - 270°)
├── c1_trophy_08_front_left.png      # Frame 8: Chếch trước bên trái (Front-Left - 315°)
└── README.md                        # Hướng dẫn chi tiết
```

---

## 📐 Thông số kỹ thuật

| Thông số | Giá trị |
| :--- | :--- |
| **Kích thước mỗi Frame** | `256 x 384` px |
| **Định dạng** | PNG RGBA 32-bit (Transparent) |
| **Anchor Alignment** | Trục xoay nằm tại `X = 128`, đáy cúp tại `Y = 356` trên mọi frame (chống rung giật khi xoay) |
| **Horizontal Strip** | `2048 x 384` px (chuẩn kích thước lũy thừa 2, tối ưu cho GPU) |
| **Grid** | `1024 x 768` px (4 cột x 2 hàng) |

---

## 🎮 Cách sử dụng trong Godot 4

### 1. Dùng với `AnimatedSprite2D` (Tạo Animation xoay cúp)
1. Tạo một Node `AnimatedSprite2D`.
2. Trong Inspector, tạo mới một **SpriteFrames**.
3. Mở bảng điều khiển SpriteFrames ở cạnh dưới màn hình Godot:
   * **Cách A (Kéo thả ảnh lẻ):** Kéo cả 8 file `c1_trophy_01_front.png` đến `c1_trophy_08_front_left.png` vào danh sách animation (đặt tên ví dụ: `rotate`).
   * **Cách B (Dùng Strip):** Nhấn icon **Add frames from a Sprite Sheet**, chọn file `c1_trophy_rotation_strip.png`, đặt:
     * `Horizontal`: **8**
     * `Vertical`: **1**
     * Chọn tất cả 8 frames và nhấn **Add Frames**.
4. Đặt tốc độ animation: **8 FPS** hoặc **10 FPS**, tích chọn **Loop**.

### 2. Dùng với `Sprite2D` thông thường
1. Gán Texture là `c1_trophy_rotation_strip.png`.
2. Trong mục **Animation** của `Sprite2D`:
   * `Hframes`: **8**
   * `Vframes`: **1**
   * Đổi `Frame` từ **0** đến **7** để chọn góc nhìn tương ứng.

### 3. Thiết lập hiển thị Pixel Art sắc nét
Trong phần **CanvasItem -> Texture -> Filter**, chọn **`Nearest`** (thay vì `Linear`) để giữ độ sắc nét pixel nguyên bản không bị nhòe mờ.
