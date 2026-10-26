# Neural hourglass application icon

Generated using the built-in ImageGen tool from the user's visual reference. No lettering, digits or time indicator. The PNG retains a transparent background.

- `neural-hourglass.png`: original generated artwork.
- `app.ico`: 32-bit RGBA Windows icon with 16, 24, 32, 48, 64, 128 and 256 pixel frames.
- The ICO is embedded in the launcher EXE and used by the WPF windows and tray. It is ready for reuse by a future installer; this change does not add an installer.

Regenerate the format conversion (no image generation required):

```powershell
.\tools\Convert-Icon.ps1
```

## Generation prompt

Use case: ui-mockup / app icon. Generate ONE high quality Windows application icon for a Codex quota widget. The reference is a STYLE AND SUBJECT reference, not an edit target. Subject: a centered upright futuristic hourglass made of thick luminous cyan glass, with substantial dark gunmetal silver top and bottom caps. Inside the two glass bulbs: a small number of clearly defined bright cyan neural-network nodes and connecting filaments, flowing through the narrow center like digital sand. Capture the reference's elegant cool-blue glass and metallic material style, but SIMPLIFY drastically to produce a recognizable silhouette at 16px, 32px and 48px. Broad clean edges, strong contrast, a symmetric compact silhouette, restrained highlights, professional premium Windows desktop app icon. Slight 3D dimensionality with very slight top surface visible; icon remains straight upright, no dramatic perspective. Single isolated object fills about 84 percent of a square canvas, ample alpha margin around edges. No floor or scene, no cast shadow outside silhouette, no background tile. GENUINELY TRANSPARENT background. Absolutely NO TEXT, no digits, no binary glyphs, no time indicator, no clock face, no progress bars, no side handles or side panels, no logo lettering, no watermark. No decorative elements outside the hourglass. Color palette cyan / icy blue / dark silver with bright rim highlights. Crisp edges suitable for conversion into multi-resolution Windows ICO. This is the actual production launcher, installer and tray icon artwork.
