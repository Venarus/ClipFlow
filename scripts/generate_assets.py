import os
import math
from PIL import Image, ImageDraw, ImageFont, ImageFilter

os.makedirs("docs/assets", exist_ok=True)
os.makedirs("docs/screenshots", exist_ok=True)

# Helper function to get a clean font
def get_font(size, bold=False):
    font_names = [
        "C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf",
        "C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf",
    ]
    for path in font_names:
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size)
            except Exception:
                pass
    return ImageFont.load_default()

def get_code_font(size):
    font_names = [
        "C:/Windows/Fonts/consola.ttf",
        "C:/Windows/Fonts/cascadiacode.ttf",
        "C:/Windows/Fonts/lucon.ttf"
    ]
    for path in font_names:
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size)
            except Exception:
                pass
    return get_font(size)

# ==============================================================================
# 1. GENERATE LOGO PNG (512x512)
# ==============================================================================
def generate_logo():
    print("Generating docs/assets/logo.png...")
    size = 512
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Ambient radial glow
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow)
    gdraw.ellipse([80, 80, size-80, size-80], fill=(59, 130, 246, 60))
    glow = glow.filter(ImageFilter.GaussianBlur(40))
    img.alpha_composite(glow)

    # Clipboard Body
    body_box = [110, 90, 402, 460]
    draw.rounded_rectangle(body_box, radius=32, fill=(15, 23, 42, 245), outline=(59, 130, 246, 160), width=3)
    
    # Inner subtle glass shine
    draw.rounded_rectangle([114, 94, 398, 220], radius=28, fill=(255, 255, 255, 8))

    # Top Clip
    draw.rounded_rectangle([186, 60, 326, 115], radius=16, fill=(37, 99, 235, 255), outline=(147, 197, 253, 200), width=2)
    draw.rounded_rectangle([226, 48, 286, 74], radius=8, fill=(30, 41, 59, 255), outline=(96, 165, 250, 255), width=2)

    # Fluid Flow Waves
    # Wave 1 (Neon Cyan-Blue)
    points_1 = []
    for x in range(150, 362):
        t = (x - 150) / 212.0
        y = 185 + 16 * math.sin(t * math.pi * 2.2)
        points_1.append((x, y))
    for i in range(len(points_1)-1):
        draw.line([points_1[i], points_1[i+1]], fill=(56, 189, 248, 240), width=6)

    # Wave 2 (Purple Flow)
    points_2 = []
    for x in range(150, 362):
        t = (x - 150) / 212.0
        y = 238 + 14 * math.sin(t * math.pi * 2.0 + 1.2)
        points_2.append((x, y))
    for i in range(len(points_2)-1):
        draw.line([points_2[i], points_2[i+1]], fill=(139, 92, 246, 230), width=5)

    # Smart feature pill cards in logo
    # 1. Blue pill
    draw.rounded_rectangle([150, 290, 210, 318], radius=6, fill=(59, 130, 246, 60), outline=(59, 130, 246, 180), width=1)
    draw.ellipse([158, 299, 168, 309], fill=(56, 189, 248, 255))
    draw.line([174, 304, 200, 304], fill=(147, 197, 253, 220), width=3)

    # 2. Purple code pill
    draw.rounded_rectangle([220, 290, 290, 318], radius=6, fill=(139, 92, 246, 60), outline=(139, 92, 246, 180), width=1)
    font_code = get_font(12, bold=True)
    draw.text((230, 296), "</>", font=font_code, fill=(196, 181, 253, 255))
    draw.line([256, 304, 280, 304], fill=(221, 214, 254, 220), width=3)

    # 3. Emerald pill
    draw.rounded_rectangle([300, 290, 360, 318], radius=6, fill=(16, 185, 129, 60), outline=(16, 185, 129, 180), width=1)
    draw.ellipse([308, 299, 318, 309], fill=(52, 211, 153, 255))
    draw.line([324, 304, 350, 304], fill=(167, 243, 208, 220), width=3)

    # Wave 3 (Amber/Orange)
    points_3 = []
    for x in range(150, 362):
        t = (x - 150) / 212.0
        y = 360 + 12 * math.sin(t * math.pi * 1.8 + 2.5)
        points_3.append((x, y))
    for i in range(len(points_3)-1):
        draw.line([points_3[i], points_3[i+1]], fill=(59, 130, 246, 200), width=4)

    # Bottom indicators
    draw.ellipse([156, 408, 166, 418], fill=(34, 197, 94, 255))
    draw.line([176, 413, 260, 413], fill=(100, 116, 139, 200), width=3)
    draw.line([300, 413, 360, 413], fill=(71, 85, 105, 200), width=3)

    img.save("docs/assets/logo.png", "PNG")

# ==============================================================================
# 2. GENERATE HERO BANNER (1280x420)
# ==============================================================================
def generate_banner():
    print("Generating docs/assets/banner.png...")
    w, h = 1280, 420
    img = Image.new("RGBA", (w, h), (11, 15, 25, 255))
    draw = ImageDraw.Draw(img)

    # Cyber Mesh & Glowing Orbs
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow)
    gdraw.ellipse([-100, -100, 450, 450], fill=(59, 130, 246, 45))
    gdraw.ellipse([850, 100, 1350, 500], fill=(139, 92, 246, 40))
    gdraw.ellipse([500, -80, 900, 280], fill=(14, 165, 233, 30))
    glow = glow.filter(ImageFilter.GaussianBlur(60))
    img.alpha_composite(glow)

    # Subtle grid dots
    for x in range(40, w, 40):
        for y in range(30, h, 40):
            draw.point((x, y), fill=(255, 255, 255, 18))

    # Paste Logo
    logo = Image.open("docs/assets/logo.png").resize((210, 210), Image.Resampling.LANCZOS)
    img.alpha_composite(logo, (80, 105))

    # Typography
    font_brand = get_font(52, bold=True)
    font_sub = get_font(21, bold=True)
    font_desc = get_font(15, bold=False)
    font_badge = get_font(12, bold=True)

    text_x = 330
    # App Name
    draw.text((text_x, 90), "ClipFlow", font=font_brand, fill=(255, 255, 255, 255))
    
    # Version Pill
    draw.rounded_rectangle([text_x + 225, 102, text_x + 295, 132], radius=6, fill=(59, 130, 246, 45), outline=(59, 130, 246, 180), width=1)
    draw.text((text_x + 242, 107), "v1.0.0", font=font_badge, fill=(96, 165, 250, 255))

    # Subtitle
    draw.text((text_x, 158), "Smart, Private & Fluid Windows 11 Clipboard Manager", font=font_sub, fill=(147, 197, 253, 255))

    # Description
    desc_line1 = "Instant clipboard intelligence with Mica/Acrylic glassmorphism, genuine Win32 event monitoring,"
    desc_line2 = "Linux-style fluid micro-animations, smart color/code/file detection, and zero-cloud local SQLite WAL."
    draw.text((text_x, 200), desc_line1, font=font_desc, fill=(148, 163, 184, 255))
    draw.text((text_x, 224), desc_line2, font=font_desc, fill=(148, 163, 184, 255))

    # Badges Row
    badges = [
        ("⚡ .NET 9 WPF", (59, 130, 246)),
        ("💎 Windows 11 Fluent", (14, 165, 233)),
        ("🐧 Linux Fluid Easing", (168, 85, 247)),
        ("🛡️ Zero Telemetry", (34, 197, 94)),
        ("🚀 SQLite WAL Mode", (249, 115, 22)),
        ("📦 Self-Contained", (236, 72, 153))
    ]

    bx = text_x
    by = 280
    for label, col in badges:
        r, g, b = col
        bbox = draw.textbbox((0, 0), label, font=font_badge)
        bw = bbox[2] - bbox[0] + 20
        draw.rounded_rectangle([bx, by, bx + bw, by + 28], radius=6, fill=(r, g, b, 35), outline=(r, g, b, 140), width=1)
        draw.text((bx + 10, by + 6), label, font=font_badge, fill=(r, g, b, 255))
        bx += bw + 12

    # Bottom Gradient Accent Line
    for x in range(w):
        t = x / float(w)
        r = int(59 * (1 - t) + 168 * t)
        g = int(130 * (1 - t) + 85 * t)
        b = int(246 * (1 - t) + 247 * t)
        draw.line([(x, h-4), (x, h)], fill=(r, g, b, 240))

    img.save("docs/assets/banner.png", "PNG")

# ==============================================================================
# 3. GENERATE REALISTIC APP MAIN SCREENSHOT (1200x780)
# ==============================================================================
def generate_app_screenshot():
    print("Generating docs/screenshots/app-main.png...")
    w, h = 1200, 780
    img = Image.new("RGBA", (w, h), (15, 23, 42, 255))
    draw = ImageDraw.Draw(img)

    # Ambient backdrop gradient (simulating Windows 11 desktop wallpaper blur)
    bg_glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    bg_draw = ImageDraw.Draw(bg_glow)
    bg_draw.ellipse([100, -200, 900, 400], fill=(30, 58, 138, 70))
    bg_draw.ellipse([600, 400, 1300, 900], fill=(76, 29, 149, 60))
    bg_glow = bg_glow.filter(ImageFilter.GaussianBlur(80))
    img.alpha_composite(bg_glow)

    # Window Container (Margin 20)
    wx1, wy1, wx2, wy2 = 24, 24, w - 24, h - 24
    draw.rounded_rectangle([wx1, wy1, wx2, wy2], radius=12, fill=(15, 23, 42, 235), outline=(255, 255, 255, 30), width=1)

    # Title Bar
    draw.rounded_rectangle([wx1, wy1, wx2, wy1 + 44], radius=12, fill=(255, 255, 255, 12))
    # Fill bottom corners of title bar
    draw.rectangle([wx1, wy1 + 24, wx2, wy1 + 44], fill=(255, 255, 255, 12))
    draw.line([wx1, wy1 + 44, wx2, wy1 + 44], fill=(255, 255, 255, 20), width=1)

    font_title = get_font(13, bold=True)
    font_ui = get_font(13, bold=False)
    font_bold = get_font(13, bold=True)
    font_small = get_font(11, bold=False)
    font_code = get_code_font(12)

    # Title bar brand
    logo_icon = Image.open("docs/assets/icon.png").resize((20, 20), Image.Resampling.LANCZOS)
    img.alpha_composite(logo_icon, (wx1 + 16, wy1 + 12))
    draw.text((wx1 + 44, wy1 + 13), "ClipFlow", font=font_title, fill=(255, 255, 255, 255))
    draw.rounded_rectangle([wx1 + 104, wy1 + 12, wx1 + 144, wy1 + 32], radius=4, fill=(59, 130, 246, 40))
    draw.text((wx1 + 112, wy1 + 14), "v1.0", font=get_font(10, bold=True), fill=(96, 165, 250, 255))

    # Center Monitoring Pill
    draw.rounded_rectangle([w//2 - 80, wy1 + 8, w//2 + 80, wy1 + 36], radius=6, fill=(255, 255, 255, 12), outline=(255, 255, 255, 20))
    draw.ellipse([w//2 - 65, wy1 + 18, w//2 - 57, wy1 + 26], fill=(34, 197, 94, 255))
    draw.text((w//2 - 48, wy1 + 13), "Monitoring Active", font=font_small, fill=(203, 213, 225, 255))

    # Right Window Controls
    draw.line([wx2 - 90, wy1 + 22, wx2 - 76, wy1 + 22], fill=(148, 163, 184, 255), width=2)
    draw.rectangle([wx2 - 60, wy1 + 16, wx2 - 46, wy1 + 30], outline=(148, 163, 184, 255), width=2)
    draw.line([wx2 - 30, wy1 + 16, wx2 - 18, wy1 + 28], fill=(148, 163, 184, 255), width=2)
    draw.line([wx2 - 30, wy1 + 28, wx2 - 18, wy1 + 16], fill=(148, 163, 184, 255), width=2)

    # 3 Column Layout
    side_w = 210
    prev_w = 360
    main_w = (wx2 - wx1) - side_w - prev_w

    sx1 = wx1
    sx2 = sx1 + side_w
    mx1 = sx2
    mx2 = mx1 + main_w
    px1 = mx2
    px2 = wx2

    draw.line([sx2, wy1 + 44, sx2, wy2], fill=(255, 255, 255, 20), width=1)
    draw.line([px1, wy1 + 44, px1, wy2], fill=(255, 255, 255, 20), width=1)

    # 1. SIDEBAR ITEMS
    draw.text((sx1 + 16, wy1 + 60), "HISTORY", font=get_font(11, bold=True), fill=(100, 116, 139, 255))
    nav_items = [
        ("All Items", True, (59, 130, 246)),
        ("Text & Code", False, (148, 163, 184)),
        ("Images", False, (148, 163, 184)),
        ("Links", False, (148, 163, 184)),
        ("Files", False, (148, 163, 184)),
    ]
    ny = wy1 + 86
    for label, is_active, col in nav_items:
        if is_active:
            draw.rounded_rectangle([sx1 + 8, ny, sx2 - 8, ny + 38], radius=7, fill=(59, 130, 246, 45))
            draw.rounded_rectangle([sx1 + 9, ny + 9, sx1 + 13, ny + 29], radius=2, fill=(59, 130, 246, 255))
            draw.text((sx1 + 28, ny + 9), label, font=font_bold, fill=(255, 255, 255, 255))
        else:
            draw.text((sx1 + 28, ny + 9), label, font=font_ui, fill=col)
        ny += 44

    draw.text((sx1 + 16, ny + 12), "ORGANIZATION", font=get_font(11, bold=True), fill=(100, 116, 139, 255))
    ny += 38
    org_items = [
        ("Favorites", False, (234, 179, 8)),
        ("Pinned", False, (59, 130, 246)),
        ("Recently Used", False, (148, 163, 184))
    ]
    for label, is_active, col in org_items:
        draw.text((sx1 + 28, ny + 9), label, font=font_ui, fill=col)
        ny += 44

    # Bottom Settings Card in Sidebar
    draw.rounded_rectangle([sx1 + 10, wy2 - 76, sx2 - 10, wy2 - 14], radius=8, fill=(255, 255, 255, 10), outline=(255, 255, 255, 18))
    draw.text((sx1 + 20, wy2 - 68), "Local Storage", font=font_small, fill=(148, 163, 184, 255))
    draw.text((sx1 + 20, wy2 - 48), "1,240 items stored", font=font_bold, fill=(241, 245, 249, 255))

    # 2. MAIN CONTENT (CLIPBOARD HISTORY)
    my = wy1 + 56
    # Search Bar
    draw.rounded_rectangle([mx1 + 16, my, mx2 - 90, my + 38], radius=8, fill=(30, 41, 59, 220), outline=(59, 130, 246, 120), width=1)
    draw.text((mx1 + 32, my + 9), "Search history (Win+Alt+F)...", font=font_ui, fill=(100, 116, 139, 255))
    
    # Action buttons
    draw.rounded_rectangle([mx2 - 80, my, mx2 - 48, my + 38], radius=6, fill=(255, 255, 255, 14))
    draw.rounded_rectangle([mx2 - 40, my, mx2 - 8, my + 38], radius=6, fill=(239, 68, 68, 30), outline=(239, 68, 68, 120))

    # Active Section Title Banner
    my += 52
    draw.text((mx1 + 18, my), "All Items", font=get_font(15, bold=True), fill=(255, 255, 255, 255))
    draw.rounded_rectangle([mx1 + 92, my - 2, mx1 + 162, my + 22], radius=10, fill=(59, 130, 246, 35))
    draw.text((mx1 + 102, my + 2), "12 items", font=get_font(11, bold=True), fill=(96, 165, 250, 255))

    my += 36

    # CARDS LIST (Simulating Linux-animated fluid card list)
    cards = [
        {
            "type": "color",
            "title": "#3B82F6",
            "snippet": "RGB(59, 130, 246) • Windows 11 Modern Blue",
            "source": "Figma",
            "time": "Just now",
            "badge": "Color",
            "badge_col": (236, 72, 153),
            "color_val": (59, 130, 246),
            "selected": True
        },
        {
            "type": "code",
            "title": "public async Task<bool> CopyToClipboardAsync()",
            "snippet": "C# Method implementation with Win32 format provider",
            "source": "VS Code",
            "time": "3m ago",
            "badge": "Code",
            "badge_col": (168, 85, 247),
            "selected": False
        },
        {
            "type": "url",
            "title": "github.com/dotnet/runtime",
            "snippet": "https://github.com/dotnet/runtime/releases/tag/v9.0.2",
            "source": "Chrome",
            "time": "12m ago",
            "badge": "Link",
            "badge_col": (14, 165, 233),
            "selected": False
        },
        {
            "type": "file",
            "title": "ClipFlow-Setup-v1.0.0.exe",
            "snippet": "C:\\Users\\senem\\Desktop\\Antigravity\\ClipFlow\\dist",
            "source": "Explorer",
            "time": "34m ago",
            "badge": "File",
            "badge_col": (249, 115, 22),
            "selected": False
        },
        {
            "type": "text",
            "title": "Clean, reliable Windows 11 productivity utility",
            "snippet": "ClipFlow is lightweight, private, and runs natively.",
            "source": "Notepad",
            "time": "1h ago",
            "badge": "Text",
            "badge_col": (59, 130, 246),
            "selected": False
        }
    ]

    for card in cards:
        cx1 = mx1 + 16
        cx2 = mx2 - 16
        ch = 72
        
        if card["selected"]:
            # Linux Fluid slide right + Accent border
            cx1 += 4
            draw.rounded_rectangle([cx1, my, cx2, my + ch], radius=8, fill=(59, 130, 246, 50), outline=(59, 130, 246, 255), width=1)
            # Left active bar
            draw.rounded_rectangle([cx1 - 2, my + 16, cx1 + 2, my + ch - 16], radius=2, fill=(59, 130, 246, 255))
        else:
            draw.rounded_rectangle([cx1, my, cx2, my + ch], radius=8, fill=(30, 41, 59, 110), outline=(255, 255, 255, 25), width=1)

        # 46x46 Thumbnail Preview Tile
        tx1, ty1 = cx1 + 12, my + 13
        draw.rounded_rectangle([tx1, ty1, tx1 + 46, ty1 + 46], radius=8, fill=(15, 23, 42, 220), outline=(255, 255, 255, 30), width=1)

        if card["type"] == "color":
            draw.rounded_rectangle([tx1 + 3, ty1 + 3, tx1 + 43, ty1 + 43], radius=6, fill=card["color_val"])
            draw.rounded_rectangle([tx1 + 6, ty1 + 28, tx1 + 40, ty1 + 41], radius=3, fill=(0, 0, 0, 160))
            draw.text((tx1 + 8, ty1 + 29), "HEX", font=get_font(9, bold=True), fill=(255, 255, 255, 255))
        elif card["type"] == "code":
            draw.rounded_rectangle([tx1 + 2, ty1 + 2, tx1 + 44, ty1 + 44], radius=7, fill=(30, 27, 75, 255))
            draw.text((tx1 + 14, ty1 + 8), "</>", font=get_font(12, bold=True), fill=(167, 139, 250, 255))
            draw.text((tx1 + 17, ty1 + 28), "C#", font=get_font(9, bold=True), fill=(221, 214, 254, 255))
        elif card["type"] == "url":
            draw.rounded_rectangle([tx1 + 2, ty1 + 2, tx1 + 44, ty1 + 44], radius=7, fill=(2, 132, 199, 255))
            draw.ellipse([tx1 + 14, ty1 + 10, tx1 + 32, ty1 + 28], outline=(255, 255, 255, 240), width=2)
            draw.line([tx1 + 14, ty1 + 19, tx1 + 32, ty1 + 19], fill=(255, 255, 255, 240), width=2)
            draw.text((tx1 + 12, ty1 + 31), "LINK", font=get_font(8, bold=True), fill=(224, 242, 254, 255))
        elif card["type"] == "file":
            draw.rounded_rectangle([tx1 + 2, ty1 + 2, tx1 + 44, ty1 + 44], radius=7, fill=(234, 88, 12, 255))
            draw.rounded_rectangle([tx1 + 10, ty1 + 28, tx1 + 36, ty1 + 41], radius=3, fill=(0, 0, 0, 160))
            draw.text((tx1 + 13, ty1 + 29), "EXE", font=get_font(8, bold=True), fill=(254, 215, 170, 255))
        elif card["type"] == "text":
            draw.rounded_rectangle([tx1 + 2, ty1 + 2, tx1 + 44, ty1 + 44], radius=7, fill=(30, 41, 59, 255))
            draw.rectangle([tx1 + 10, ty1 + 12, tx1 + 34, ty1 + 15], fill=(148, 163, 184, 255))
            draw.rectangle([tx1 + 10, ty1 + 19, tx1 + 38, ty1 + 21], fill=(100, 116, 139, 255))
            draw.rectangle([tx1 + 10, ty1 + 25, tx1 + 28, ty1 + 27], fill=(100, 116, 139, 255))
            draw.rectangle([tx1 + 10, ty1 + 31, tx1 + 20, ty1 + 33], fill=(71, 85, 105, 255))

        # Title & Snippet
        draw.text((tx1 + 58, my + 14), card["title"], font=font_bold, fill=(255, 255, 255, 255))
        draw.text((tx1 + 58, my + 34), card["snippet"], font=font_small, fill=(148, 163, 184, 255))

        # Subtitle metadata badges
        br, bg, bb = card["badge_col"]
        draw.rounded_rectangle([tx1 + 58, my + 52, tx1 + 100, my + 66], radius=3, fill=(br, bg, bb, 45))
        draw.text((tx1 + 64, my + 53), card["badge"], font=get_font(9, bold=True), fill=(br, bg, bb, 255))

        meta_str = f"{card['time']} • {card['source']}"
        draw.text((tx1 + 108, my + 52), meta_str, font=font_small, fill=(100, 116, 139, 255))

        # Right favorite/pin icons
        draw.text((cx2 - 54, my + 26), "★", font=get_font(15, bold=True), fill=(234, 179, 8, 255) if card["selected"] else (100, 116, 139, 140))
        draw.text((cx2 - 28, my + 26), "📌", font=get_font(13), fill=(59, 130, 246, 255) if card["selected"] else (100, 116, 139, 140))

        my += ch + 8

    # 3. PREVIEW PANEL (RIGHT SIDE)
    py = wy1 + 60
    draw.rounded_rectangle([px1 + 16, py, px1 + 76, py + 24], radius=4, fill=(236, 72, 153, 50))
    draw.text((px1 + 24, py + 4), "Color", font=get_font(11, bold=True), fill=(244, 114, 182, 255))
    draw.text((px1 + 16, py + 32), "Copied Just now", font=font_small, fill=(100, 116, 139, 255))

    # Large Color Swatch
    py += 62
    draw.rounded_rectangle([px1 + 16, py, px2 - 16, py + 110], radius=10, fill=(59, 130, 246, 255), outline=(255, 255, 255, 50), width=1)
    
    py += 124
    draw.text((px1 + 16, py), "HEX: #3B82F6", font=font_bold, fill=(255, 255, 255, 255))
    draw.text((px1 + 16, py + 22), "RGB: 59, 130, 246 (Alpha: 1.0)", font=font_ui, fill=(148, 163, 184, 255))

    # Details Box
    py += 60
    draw.rounded_rectangle([px1 + 16, py, px2 - 16, py + 90], radius=8, fill=(255, 255, 255, 10), outline=(255, 255, 255, 18))
    draw.text((px1 + 28, py + 12), "Size:           7 Bytes", font=font_small, fill=(148, 163, 184, 255))
    draw.text((px1 + 28, py + 34), "Used:          12 times", font=font_small, fill=(148, 163, 184, 255))
    draw.text((px1 + 28, py + 56), "Source:       Figma Desktop", font=font_small, fill=(148, 163, 184, 255))

    # Primary Action Copy Button
    py = wy2 - 64
    draw.rounded_rectangle([px1 + 16, py, px2 - 16, py + 42], radius=8, fill=(37, 99, 235, 255), outline=(96, 165, 250, 180), width=1)
    draw.text((px1 + 110, py + 11), "Copy to Clipboard", font=font_bold, fill=(255, 255, 255, 255))

    # 4. LINUX-STYLE SPRING TOAST NOTIFICATION
    draw.rounded_rectangle([w//2 - 140, wy2 - 58, w//2 + 140, wy2 - 18], radius=20, fill=(30, 41, 59, 250), outline=(59, 130, 246, 255), width=1)
    draw.text((w//2 - 110, wy2 - 45), "✓  Copied Color from Figma", font=get_font(12, bold=True), fill=(241, 245, 249, 255))

    img.save("docs/screenshots/app-main.png", "PNG")

# ==============================================================================
# 4. GENERATE SMART RECOGNITION INFOGRAPHIC (960x420)
# ==============================================================================
def generate_smart_recognition_graphic():
    print("Generating docs/screenshots/smart-recognition.png...")
    w, h = 960, 420
    img = Image.new("RGBA", (w, h), (15, 23, 42, 255))
    draw = ImageDraw.Draw(img)

    # Gradient header
    draw.text((40, 30), "Smart Content Recognition Engine", font=get_font(24, bold=True), fill=(255, 255, 255, 255))
    draw.text((40, 68), "ClipFlow analyzes copied content with zero-execution regex & token classification.", font=get_font(14), fill=(148, 163, 184, 255))

    types = [
        ("HEX & RGB Colors", "#EC4899", "Live palette swatch preview with RGB/RGBA conversion", "HEX: #3B82F6"),
        ("Code Snippets", "#8B5CF6", "Syntax language identification (C#, JS, Python, SQL, Rust)", "public void Run()"),
        ("URLs & Web Domains", "#0EA5E9", "Instant domain extraction with direct default browser open", "github.com"),
        ("Files & Drop Lists", "#F97316", "Windows shell icon extraction, size & extension categorization", "document.pdf"),
        ("Emails & Phones", "#14B8A6", "Clean contact format recognition and privacy masking", "user@domain.com"),
        ("Plain & Rich Text", "#3B82F6", "Mini line card representation with word count and UTF-8 size", "Hello World...")
    ]

    col_w = (w - 80 - 20) // 2
    row_h = 76
    y = 110
    
    for i, (title, color_hex, desc, example) in enumerate(types):
        col_idx = i % 2
        row_idx = i // 2
        
        x = 40 + col_idx * (col_w + 20)
        curr_y = y + row_idx * (row_h + 16)
        
        draw.rounded_rectangle([x, curr_y, x + col_w, curr_y + row_h], radius=8, fill=(30, 41, 59, 160), outline=(255, 255, 255, 20), width=1)
        
        # Color pill icon
        draw.rounded_rectangle([x + 12, curr_y + 16, x + 56, curr_y + 60], radius=6, fill=(15, 23, 42, 220), outline=(255, 255, 255, 30), width=1)
        draw.text((x + 22, curr_y + 26), "★", font=get_font(14, bold=True), fill=color_hex)
        
        draw.text((x + 68, curr_y + 14), title, font=get_font(13, bold=True), fill=(255, 255, 255, 255))
        draw.text((x + 68, curr_y + 36), desc, font=get_font(11), fill=(148, 163, 184, 255))
        draw.text((x + 68, curr_y + 54), f"Example: {example}", font=get_code_font(10), fill=(96, 165, 250, 255))

    img.save("docs/screenshots/smart-recognition.png", "PNG")

# ==============================================================================
# 5. GENERATE PRIVACY SHIELD INFOGRAPHIC (960x360)
# ==============================================================================
def generate_privacy_graphic():
    print("Generating docs/screenshots/privacy-shield.png...")
    w, h = 960, 360
    img = Image.new("RGBA", (w, h), (15, 23, 42, 255))
    draw = ImageDraw.Draw(img)

    draw.text((40, 28), "Privacy & Security Architecture", font=get_font(24, bold=True), fill=(255, 255, 255, 255))
    draw.text((40, 64), "Built from the ground up for strict privacy, compliance, and zero cloud leaks.", font=get_font(14), fill=(148, 163, 184, 255))

    pillars = [
        ("🛡️ 100% Local Storage", "Zero cloud synchronization, no remote servers, no telemetry or diagnostic pings.", (34, 197, 94)),
        ("🔑 Password Protection", "Auto-detects KeePass, 1Password, Bitwarden ignore markers and ignores credentials.", (59, 130, 246)),
        ("🚫 Sensitive Data Filter", "Regex scanners reject JWTs, API keys, private keys, and credit cards from ever being stored.", (168, 85, 247)),
        ("⚡ Ephemeral Image Cache", "Temporary image blobs are hashed with SHA-256 and subject to configurable auto-cleanup.", (249, 115, 22))
    ]

    card_w = (w - 80 - 36) // 4
    x = 40
    y = 105
    for title, desc, col in pillars:
        r, g, b = col
        draw.rounded_rectangle([x, y, x + card_w, y + 210], radius=10, fill=(30, 41, 59, 140), outline=(r, g, b, 120), width=1)
        draw.text((x + 14, y + 20), title, font=get_font(12, bold=True), fill=(255, 255, 255, 255))
        
        # Word wrap desc
        words = desc.split()
        lines = []
        cur = []
        for word in words:
            cur.append(word)
            if len(" ".join(cur)) > 22:
                lines.append(" ".join(cur[:-1]))
                cur = [word]
        if cur:
            lines.append(" ".join(cur))
        
        ly = y + 55
        for line in lines:
            draw.text((x + 14, ly), line, font=get_font(12), fill=(148, 163, 184, 255))
            ly += 20
            
        x += card_w + 12

    img.save("docs/screenshots/privacy-shield.png", "PNG")

# ==============================================================================
# 6. GENERATE ARCHITECTURE DIAGRAM (960x360)
# ==============================================================================
def generate_architecture_graphic():
    print("Generating docs/assets/architecture.png...")
    w, h = 960, 360
    img = Image.new("RGBA", (w, h), (15, 23, 42, 255))
    draw = ImageDraw.Draw(img)

    draw.text((40, 26), "ClipFlow System Architecture", font=get_font(22, bold=True), fill=(255, 255, 255, 255))
    draw.text((40, 58), "Decoupled Clean Architecture with event-driven Win32 hooks and SQLite WAL storage.", font=get_font(13), fill=(148, 163, 184, 255))

    layers = [
        ("ClipFlow.App (UI Layer)", [
            "WPF on .NET 9 (x64 Native)",
            "CommunityToolkit.Mvvm Pattern",
            "Windows 11 Acrylic & Mica DWM API",
            "Linux-style Fluid Hover & Physics Animations",
            "Virtualized Recycling Canvas (Zero lag)"
        ], (59, 130, 246)),
        ("ClipFlow.Infrastructure", [
            "Win32 AddClipboardFormatListener Hook",
            "Global Hotkey Subsystem (RegisterHotKey)",
            "SQLite WAL Mode with Memory Pragma",
            "Security & Exclusion List Filter Engine",
            "System Tray Shell_NotifyIcon Handler"
        ], (168, 85, 247)),
        ("ClipFlow.Core (Domain)", [
            "Domain Models (ClipboardItem, AppSettings)",
            "SmartContentRecognizer (Colors, URLs, Code)",
            "Contracts (IClipboardMonitor, IStorage)",
            "HashUtilities (SHA-256 Deduplication)",
            "FilterCriteria & Multi-select Logic"
        ], (34, 197, 94))
    ]

    lw = (w - 80 - 40) // 3
    x = 40
    y = 96
    for title, points, col in layers:
        r, g, b = col
        draw.rounded_rectangle([x, y, x + lw, y + 230], radius=10, fill=(30, 41, 59, 160), outline=(r, g, b, 140), width=2)
        draw.rounded_rectangle([x + 10, y + 10, x + lw - 10, y + 42], radius=6, fill=(r, g, b, 35))
        draw.text((x + 18, y + 16), title, font=get_font(12, bold=True), fill=(r, g, b, 255))

        py = y + 56
        for pt in points:
            draw.text((x + 16, py), "•", font=get_font(12, bold=True), fill=(r, g, b, 255))
            draw.text((x + 28, py), pt, font=get_font(11), fill=(203, 213, 225, 255))
            py += 32

        x += lw + 20

    img.save("docs/assets/architecture.png", "PNG")

if __name__ == "__main__":
    generate_logo()
    generate_banner()
    generate_app_screenshot()
    generate_smart_recognition_graphic()
    generate_privacy_graphic()
    generate_architecture_graphic()
    print("All professional visual assets generated successfully!")
