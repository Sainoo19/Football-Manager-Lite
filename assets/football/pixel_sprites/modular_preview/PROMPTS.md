# ImageGen prompts — modular sprite preview v1

All images were generated with the built-in ImageGen tool. These are visual approval boards; the color variants are generated illustrations of the proposed recoloring approach, not images produced by a working shader.

## Kit separation board — initial generation

```text
Use case: stylized-concept
Asset type: transparent art-direction approval sheet demonstrating a reusable football sprite and separate kit color regions.
Input images: Image 1 is the previously approved football sprite style and character reference. Create a new approval board, do not reproduce the original sheet's layout or goalkeepers or balls.
Primary request: show ONE original adult football player in neutral clothes, his separate clothing-color mask, and two color schemes applied to exactly the same pose. This is a visual design mockup of a recolorable sprite system, not a technical diagram.
Style/medium: crisp chunky low-resolution pixel art, Nintendo DS era RPG overworld proportions, approximately 32x48-pixel character design enlarged using square pixel clusters. Compact athletic adult, short dark brown hair, warm medium skin, dark boots, large readable head, restrained three-tone shading. Maintain the approved reference style. No smooth gradients or 3D rendering.
Camera: elevated orthographic 2.5D overworld view, see top of head, fully visible body, contact shadow below shoes.
Composition: landscape canvas with exactly 4 equal columns and 2 equal rows, generous transparent gutters. All full-body sprites have identical scale, position inside their cell and feet baseline. No labels or drawn grid.
Top row, left to right:
1. BASE: front-facing idle player wearing neutral gray jersey, gray shorts, gray socks. Preserve skin, hair, eyes and black shoes in natural colors. Clothing has white-to-gray highlights and darker-gray folds so it can be recolored.
2. MASK: show ONLY the regions of the same clothing silhouette at exactly the same scale and matching position: shirt and sleeves filled flat pure red, shorts flat pure green, socks flat pure blue. All hair, face, skin, hands, shoes, holes and surroundings completely transparent. No whole-person outline, no shading, no shadow in this mask cell.
3. BLUE KIT: exactly the same player and pose as column 1, same hair, skin, face and shoes. Change only clothing colors: blue jersey, white shorts, blue socks. Keep the base clothing folds, outline and shading.
4. RED KIT: exactly the same player and pose as column 1, same hair, skin, face and shoes. Change only clothing colors: red jersey, dark navy shorts, white socks. Keep the base clothing folds, outline and shading.
Bottom row, left to right: repeat those same 4 columns for the BACK-FACING idle view of that identical player. The neutral base is a back view, the mask matches back-view clothing, and the blue and red variants use that exact back-view pose.
Constraints: show four columns in each row exactly as specified. Base and recolored variants must visibly have the same identity, silhouette, scale, hair and skin. Only the kit changes. Keep neutral base fully neutral in all clothes. True transparent background and gutters. No checkerboard, scenery, pitch, badges, logos, numbers, text, headings, arrows or watermark. This is a prototype board for user approval; make base-versus-mask-versus-variants immediately readable.
```

## Kit separation board — mask correction

```text
Use case: precise-object-edit
Asset type: correction to the clothing-mask column of a football sprite design approval board.
Input image 1 is the EDIT TARGET, a transparent 4-column by 2-row approval board.
Primary request: change ONLY the two mask sprites in column 2. Preserve every pixel and position of the six full human sprites in columns 1, 3 and 4, their faces, hair, skin, clothes, shoes, outlines and sizes. Preserve the exact canvas, transparent background, margins and 4x2 layout.
Mask corrections in BOTH the front-view and back-view mask:
- The shirt/sleeve fabric region must be flat pure RGB red (255,0,0), the shorts fabric flat pure RGB green (0,255,0), and the socks fabric flat pure RGB blue (0,0,255). No shading, texture or dark outline.
- Remove the FOOTBALL BOOTS from the mask. The current blue shapes wrongly continue onto large rounded boots. The blue sock regions must stop at the upper edge of the dark boots seen on the corresponding base character. All rounded toe/shoe shapes must become completely transparent. Retain only the calf/ankle sock fabric above shoes.
- White collar, white sleeve cuff trim, white sock trim, skin, face, hair, hands, legs and shoes are fixed-color parts of the base sprite. These must be completely TRANSPARENT in the mask column, not white pixels and not recolorable colored areas.
- No ground shadow in the mask.
The masks should visibly contain three isolated clothing zones (red jersey, green shorts, blue socks), with transparent gaps for natural skin, fixed white trim and dark boots.
Do not change the neutral, blue or red full-body sprites anywhere else in the board. Genuine transparency, no checkerboard, text, labels, grid or watermark.
```

## Neutral eight-direction board

```text
Use case: stylized-concept
Asset type: eight-direction neutral football sprite art-direction approval sheet.
Input images: Image 1 is a character and style reference. Use ONLY the neutral-gray player in the FIRST COLUMN as the reference character, including his dark brown short hair, warm skin, compact adult body proportions, gray uniform, dark boots, white collar/cuff accents, outlines and pixel shading. Do not reproduce the masks or colored kits from that board.
Primary request: the SAME neutral football player standing idle in all EIGHT DISTINCT compass-facing directions, so this one sprite design can later have its kit recolored in-game.
Style: crisp chunky authentic pixel art, low-resolution RPG overworld sprite design enlarged with square pixels, fixed orthographic camera slightly above and from the south. The camera remains fixed; only the character turns. See tops of hair and shoulders, compact body. Full body and both shoes visible when appropriate. Every person is identical in scale and identity.
Layout: EXACTLY four equal columns by two equal rows, isolated sprites centered in each cell, equal transparent gutters, generous padding, common ground foot baseline within each row. No labels, no grid.
Facing order, deliberately covering a full clockwise turn through eight DISTINCT directions:
TOP ROW left to right:
1. SOUTH / down toward the viewer: frontal face visible, both eyes, symmetric shoulders.
2. SOUTHWEST / down-left: front three-quarter face looking and body turning 45 degrees toward screen-left; BOTH face and front of jersey partly visible. Nose and boots point down-left.
3. WEST / left: full left side profile, one eye, nose points horizontally left; chest side-on, NOT a frontal view and NOT a back view.
4. NORTHWEST / up-left: back three-quarter view turned 45 degrees toward screen-left, mostly back of head and back of jersey visible; a small left side of face may appear. Nose and boots point up-left.
BOTTOM ROW left to right:
1. NORTH / up away from viewer: direct back view, back of head and jersey visible, NO eyes visible.
2. NORTHEAST / up-right: back three-quarter view turned 45 degrees toward screen-right, mostly back of head and back of jersey visible; a small right side of face may appear. Nose and boots point up-right.
3. EAST / right: full right side profile, one eye, nose points horizontally right; chest side-on, NOT a frontal view and NOT a back view.
4. SOUTHEAST / down-right: front three-quarter face looking and body turning 45 degrees toward screen-right; BOTH face and front of jersey partly visible. Nose and boots point down-right.
Clothes: neutral GRAY jersey, GRAY shorts and GRAY socks in all eight views, simple white collar and cuff trim, consistent light/dark gray folds suitable for palette recoloring. All skin and hair natural colors, shoes dark charcoal. No team colors.
Constraints: genuine transparent background, no checkerboard, no scenery, no field, no text, no team logos, no numbers or watermark. No running animation or extra poses. Preserve the same body proportions, lighting from upper-left, hair shape and kit design across the complete turn. In particular south-west and north-west must be distinct front versus back three-quarter views; east and west must be proper side views.
```

