# 그림 에셋

## 레시피 재료·실험 기록 수정 · 2026-10-03

내장 `image_gen`의 **edit** 모드로 기존 레시피 그림을 편집했습니다. 꿀떡·콩떡의 재료 메모에 남은 쑥 묶음만 제거하고, 남은 재료 세 가지를 중앙으로 옮겼습니다. 꿀떡·콩떡·궁떡의 실험 기록 종이에 그려진 기존 줄과 결론 표시를 지워 같은 질감의 빈 종이로 복원했습니다. 실험 기록 줄과 결론 타원은 Unity UI에서 각 문장 위치에 맞춰 그립니다.

최종 저장 파일과 전체 프롬프트·단계별 편집 기록:

- `Game/Assets/Shop/Resources/Shop/RecipeSpreadHoney.png` · [편집 기록](Game/Assets/Shop/Resources/Shop/RecipeSpreadHoney.generation.json)
- `Game/Assets/Shop/Resources/Shop/RecipeSpreadBean.png` · [편집 기록](Game/Assets/Shop/Resources/Shop/RecipeSpreadBean.generation.json)
- `Game/Assets/Shop/Resources/Shop/RecipeSpreadRoyal.png` · [편집 기록](Game/Assets/Shop/Resources/Shop/RecipeSpreadRoyal.generation.json)
- `Game/Assets/Shop/Resources/Shop/RecipeSpreadMugwort.png` · [편집 기록](Game/Assets/Shop/Resources/Shop/RecipeSpreadMugwort.generation.json)
- `Game/Assets/Shop/Resources/Shop/RecipeResearchButtonSource.png` · [보존한 버튼 원본 기록](Game/Assets/Shop/Resources/Shop/RecipeResearchButtonSource.generation.json)

프롬프트 구성은 `precise-object-edit`이며 세 요청을 순서대로 적용했습니다. (1) 재료 메모 왼쪽의 작은 쑥 묶음·그림자만 제거하고 주변 종이를 복원. (2) 왼쪽 아래 실험 기록 종이의 기존 수평선·빨간 타원·별표만 지워 종이 질감을 보존. (3) 꿀떡·콩떡의 남은 재료 사진 세 개를 크기·수직 위치·간격을 유지하며 왼쪽으로 옮겨 메모 중앙에 배치. 사진·책·장식·연구 정보 프레임은 보존하도록 지시했습니다. 각 파일의 JSON에 사용한 원문 프롬프트와 입력·출력 파일 경로를 기록했습니다.

본문 글씨는 동봉된 나눔손글씨 펜, 큰 제목은 나눔손글씨 붓을 사용합니다. 정확한 원본 폰트를 확인한 것은 아니며, 쑥떡 원본의 손글씨 분위기에 맞춘 선택입니다. 글꼴 출처와 원본 해시·OFL 라이선스는 [Fonts 기록](Game/Assets/Shop/Resources/Shop/Fonts/README.md)에 있습니다.

안내 문구를 가리지 않게 버튼을 아래로 옮기기 위해 네 배경의 기존 남색·금색 버튼만 내장 도구의 edit 모드로 지우고 같은 종이 질감을 복원했습니다. 원래 버튼 그림은 별도 원본에 보존하고 Unity에서 그 부분을 잘라 실제 버튼에 적용합니다. 버튼 위치와 책 아이콘·문구 위치는 Unity UI에서 조절합니다. 마지막 네 배경에 공통으로 사용한 프롬프트 원문은 다음과 같으며 파일별 입력·출력 경로는 위 JSON에 있습니다.

```text
Use case: precise-object-edit
Asset type: Unity game recipe-book background texture, opaque landscape, same dimensions as input.
Input image 1 is the edit target.
Primary request: Remove ONLY the empty navy blue and gold ornamental research-button plaque at the bottom of the RIGHT book page, approximately pixels x=995..1518, y=778..874 in the 1672x941 image. Fill that exact removed plaque area with seamless aged cream parchment matching its immediate surroundings.
Constraints: This is a surgical removal. Keep the right page's thin outer decorative border, all five study-info rows and their icons, both cake photos, every left-page ingredient illustration, all left scraps of paper, pressed plants, handwritten marks, tape, lighting, book binding, surrounding table, top navigation strip, and the image composition unchanged. Do NOT move or redesign any other element. Do NOT add any text, new button, icon, ingredients, decoration, or watermark. Preserve the original full image size, framing and color.
```

기본 배경, 떡 9종 아틀라스와 손님 초상은 내장 `image_gen` 도구로 새로 생성했습니다. 새 연구소 화면은 사용자 요청에 따라 제공한 원본 사진을 직접 사용하고, 같은 사진의 편집본을 함께 사용합니다. 첫 쑥떡 페이지 왼쪽 손글씨는 원본 그대로이며, 실시간 연구 정보와 다른 페이지 본문은 Unity Text로 표시합니다. Python 이미지 편집이나 별도 이미지 API는 사용하지 않았습니다.

## 배경

저장 파일: `Game/Assets/Shop/Resources/Shop/ShopBackground.png`

생성 프롬프트의 최종 구성:

```text
Use case: stylized-concept
Asset type: Unity game background, landscape 16:9.
Primary request: a cozy small-town Korean traditional rice-cake mill/shop in spring,
in a polished hand-painted visual-novel environment style.
Input image role: style reference only; do not reproduce its UI.
Scene: warm wooden shop frontage, tiled roof, soft late-afternoon sunlight,
spring branches near the edges, a quiet alley, a potted plant, a walnut counter
near the bottom, and subtle jars in the background.
Composition: keep the central 85% calm enough for real parchment UI panels.
Palette: chestnut, honey, sage, ivory.
Constraints: new original backdrop, target 1920x1080.
Avoid: text, sign lettering, numbers, UI, buttons, product trays, characters,
logos, watermark.
```

## 떡 9종

저장 파일: `Game/Assets/Shop/Resources/Shop/RiceCakeAtlas.png`

실제 생성 프롬프트와 생성 경로는 같은 폴더의 `RiceCakeAtlas.generation.json`에 보관했습니다. 투명 배경의 3 × 3 아틀라스로, 각 셀은 418 × 418 픽셀입니다. 원본 알파를 유지했습니다.

왼쪽 위부터 가래떡, 절편, 백설기, 인절미, 바람떡, 쑥떡쑥떡, 꿀떡꿀떡, 콩떡콩떡, 궁떡궁떡 순서입니다. Unity에서 셀을 Sprite로 나누어 표시합니다. 연구 전 회색 처리는 원본을 바꾸지 않고 `Grayscale.shader`로 적용합니다.

손님 추천 화면에서도 동일한 아틀라스를 사용합니다. 선택한 맛과 다른 떡은 같은 셰이더로 회색 처리하고, 맛 분류 안내와 추천 버튼은 실제 uGUI 요소로 표시합니다.

## 손님 초상

저장 파일: `Game/Assets/Shop/Resources/Shop/CustomerPortrait.png`

투명 배경의 1024 × 1536 픽셀 초상입니다. 오른쪽 손님 영역에 놓기 위해 약간 왼쪽을 바라보는 성인 여성 손님을 새로 생성했습니다. 어두운 머리와 안경, 차분한 회보라색 가디건, 아이보리 셔츠와 천 가방을 사용해 기존 나무 가게·파스텔 떡 그림의 색감에 맞췄습니다. 흰 사각 배경을 덧붙이지 않고 원본 알파를 유지합니다.

실제 전체 생성 프롬프트·도구·출력 경로는 같은 폴더의 `CustomerPortrait.generation.json`에 보관했습니다. 핵심 구성은 다음과 같습니다.

```text
Use case: stylized-concept
Asset type: transparent character portrait cutout for a cozy Korean rice-cake shop
Unity visual novel game.
One original friendly adult Korean woman customer, age about 25, shown from the
top of her head to mid-thigh, slightly turned toward the left as if speaking to
the shopkeeper. Shoulder-length dark chestnut hair, delicate oval dark glasses,
muted lavender-gray cardigan over an ivory shirt, simple dark skirt and sage
cloth tote bag. One hand gently holding the bag strap.
Style: refined softly painted anime visual-novel portrait, warm spring daylight,
subtle painterly shading, creamy highlights and gently textured cloth.
Composition: vertical 2:3 portrait, whole head and hair silhouette visible with
clear padding, isolated silhouette, facing slightly left for right-side UI.
Constraints: transparent alpha, exactly one original adult person, no white
rectangle, scenery, border, written text, labels, dialogue bubble, watermark,
logo or UI. Do not crop hair or hands or copy an existing character.
```

초상은 캐릭터 그림만 포함합니다. 말풍선, 요구사항 대사, 등장 효과, 추천 결과와 평판 변화 안내는 Unity에서 별도로 구성합니다.

## 어머니의 레시피 책

저장 파일: `Game/Assets/Shop/Resources/Shop/RecipeBookBackground.png`

내장 `image_gen` 도구의 새 이미지 생성 모드로 제작했습니다. 투명 배경의 낡은 가죽 표지와 두 장의 빈 양피지입니다. 사용자 연구소 참고 화면의 따뜻한 색감과 책 구성을 참고했고, 실제 UI의 문자·버튼은 그림에 포함하지 않았습니다. 원본 알파를 유지하며 Python 이미지 편집을 사용하지 않았습니다.

다음은 사용한 전체 프롬프트입니다. 도구·모드·원본 출력 경로는 같은 폴더의 `RecipeBookBackground.generation.json`에도 보관했습니다.

```text
Use case: stylized-concept
Asset type: transparent Unity game UI background for an open Korean family recipe book, landscape 16:9.
Primary request: an original old maternal recipe scrapbook opened to two completely blank parchment pages, seen almost directly from above. The two-page spread fills almost the whole canvas with only 2 percent transparent padding. The whole book silhouette is visible.
Style: refined warm softly painted semi-realistic illustration, matching a cozy traditional Korean rice-cake shop game; honey, ivory, warm chestnut, muted sage. Rich worn brown leather cover peeking around page edges, slightly irregular paper corners, convincing central binding/gutter shadow, curled paper edges and small age stains at the margins.
Composition: left blank page occupies 6–47% of width, right blank page 53–94%; usable writing regions from 8–92% of height. Both pages are flat, calm pale ivory parchment for live Unity text and rice-cake artwork to be added later. Keep all age marks, pressed tiny mugwort leaves, small dried spring flowers, and delicate stitched details near the outer margins only. A thin old red fabric bookmark may peek from below the central binding. No objects crossing the usable central page regions.
Constraints: genuinely transparent background; no desk, room, food pictures, portrait, labels, recipe content, lettering, numbers, stamps, buttons, UI controls, watermark or logo. Absolutely no written or printed text anywhere. Equal-sized facing pages, warm daylight softly defining depth, subtle paper grain, readable clean light center.
```

레시피 제목, 재료 메모, 실험 기록, 어머니의 편지, 연구비, 필요 조건과 버튼은 실제 Unity UI입니다. 네 장의 내용은 `Game/Assets/Shop/Scripts/UI/RecipeBookContent.cs`에서 수정할 수 있습니다. 책 넘김은 `RecipePageTurnGraphic.cs`의 종이 메쉬와 접힘 그림자로 구현했고, 이미지 생성으로 애니메이션 프레임을 만든 것은 아닙니다.

## 참고 사진을 그대로 맞춘 레시피 책 (2026-10-02 수정)

사용자가 제공한 첫 번째 연구소 사진을 직접 사용해도 된다고 요청했습니다. `Game/Assets/Shop/Resources/Shop/RecipeBookReference.png`는 제공 파일을 수정 없이 복사한 원본입니다. 첫 쑥떡 페이지의 왼쪽은 이 원본의 손글씨·떡 사진·재료 그림을 Unity Sprite 영역으로 잘라 그대로 표시합니다.

나머지 페이지와 오른쪽 실시간 연구 정보를 위해 내장 `image_gen` 도구의 **편집 모드**로 다음 네 파일을 만들었습니다. 생성된 PNG를 변경하지 않고 프로젝트에 복사했습니다. Python 이미지 편집과 별도 이미지 API는 사용하지 않았습니다.

- `Game/Assets/Shop/Resources/Shop/RecipeSpreadMugwort.png`
- `Game/Assets/Shop/Resources/Shop/RecipeSpreadHoney.png`
- `Game/Assets/Shop/Resources/Shop/RecipeSpreadBean.png`
- `Game/Assets/Shop/Resources/Shop/RecipeSpreadRoyal.png`

원본의 낡은 양피지, 사진·재료 메모의 위치, 접힌 책 표지, 분홍 편지, 반투명 정보 행, 남색·금색 버튼을 유지하고 글자를 지웠습니다. 각 떡에 맞게 사진과 재료만 바꿨습니다. 실시간 비용·선행 조건·해금 상태와 타 세 페이지의 본문은 궁서 OS 글꼴을 이용한 Unity UI로 표시합니다. 글꼴 파일은 프로젝트에 복사하지 않았습니다. 책 넘김은 기존 종이 메쉬 애니메이션입니다.

전체 프롬프트는 다음과 같으며 원본 출력 경로와 편집 대상은 각 `RecipeSpread*.generation.json`에도 보관했습니다.

### Mugwort

```text
Use case: precise-object-edit
Asset type: production Unity recipe-book UI artwork, edited from the supplied full reference.
Input image role: EDIT TARGET, keep the provided image's exact composition and visual identity.
Primary request: prepare this same richly aged Korean mother's scrapbook UI as a clean textless interactive game background. Do not redesign it. Keep the canvas 16:9 and keep the book, central gutter, curled brown leather binding, desk objects, golden sunlight, torn ingredient paper, experiment paper, salmon mother-letter note, red underline, taped tilted food photograph, right recipe illustration frame, five translucent rows, coin/document/chart/calendar/moon pictograms, and ornate navy-and-gold bottom button at their original normalized positions.
Change only: carefully remove ALL lettering and ALL numerals everywhere, including top navigation and status figures, left calligraphy and prose, ingredient labels, experimental notes, arrows' annotations, letter, right headings/descriptions/row labels/prices and the button caption. Reconstruct their underlying textured parchment or navy fill without covering them with flat new cards. Preserve all pictures, pressed leaves, tiny floral motifs, adhesive tape, pictograms, red underlines, decorative border corner flourishes, paper grain, edge damage, lighting, and navy/gold button outline.
Layout invariants: original full-image book spans about x100–1740 and y115–1010 on the 1818x1023 reference; preserve these proportions. Left tilted photograph at x615–958 y173–461; ingredients at x282–746 y492–606; small cutaway food drawing near x683–814 y640–756; right food illustration at x1340–1630 y248–473. The live Unity labels will be laid over the now-empty text areas, so those areas must remain clean and textured.
Constraints: same image and same arrangement, no new white or beige rectangular cards, no generic blank new book, no changed camera angle or zoom, no invented UI panels, no lettering whatsoever, no watermarks. Full opaque image, no transparent background. Food edit: retain all original mugwort rice cakes, the sliced cake with visible soft red-bean filling, its steaming wooden platter, mugwort sprigs, red beans and rice-flour bowl exactly. Do not change any food or ingredients. Only remove text.
```

### Honey

```text
Use case: precise-object-edit
Asset type: production Unity recipe-book UI artwork, edited from the supplied full reference.
Input image role: EDIT TARGET, keep the provided image's exact composition and visual identity.
Primary request: prepare this same richly aged Korean mother's scrapbook UI as a clean textless interactive game background. Do not redesign it. Keep the canvas 16:9 and keep the book, central gutter, curled brown leather binding, desk objects, golden sunlight, torn ingredient paper, experiment paper, salmon mother-letter note, red underline, taped tilted food photograph, right recipe illustration frame, five translucent rows, coin/document/chart/calendar/moon pictograms, and ornate navy-and-gold bottom button at their original normalized positions.
Change only: carefully remove ALL lettering and ALL numerals everywhere, including top navigation and status figures, left calligraphy and prose, ingredient labels, experimental notes, arrows' annotations, letter, right headings/descriptions/row labels/prices and the button caption. Reconstruct their underlying textured parchment or navy fill without covering them with flat new cards. Preserve all pictures, pressed leaves, tiny floral motifs, adhesive tape, pictograms, red underlines, decorative border corner flourishes, paper grain, edge damage, lighting, and navy/gold button outline.
Layout invariants: original full-image book spans about x100–1740 and y115–1010 on the 1818x1023 reference; preserve these proportions. Left tilted photograph at x615–958 y173–461; ingredients at x282–746 y492–606; small cutaway food drawing near x683–814 y640–756; right food illustration at x1340–1630 y248–473. The live Unity labels will be laid over the now-empty text areas, so those areas must remain clean and textured.
Constraints: same image and same arrangement, no new white or beige rectangular cards, no generic blank new book, no changed camera angle or zoom, no invented UI panels, no lettering whatsoever, no watermarks. Full opaque image, no transparent background. Food edit only in existing picture areas: replace both original mugwort platters and the small cutaway with a beautiful photograph of round white/pastel honey rice-cake balls on the same wooden platter, one cut open to show amber honey syrup. Replace ingredient pictures in the same positions with rice-flour bowl, a small wooden honey jar/dipper, and little sugar bowl. Keep the photograph frames, notes, plate placement, materials, all layout geometry and lighting unchanged. Do not add text.
```

### Bean

```text
Use case: precise-object-edit
Asset type: production Unity recipe-book UI artwork, edited from the supplied full reference.
Input image role: EDIT TARGET, keep the provided image's exact composition and visual identity.
Primary request: prepare this same richly aged Korean mother's scrapbook UI as a clean textless interactive game background. Do not redesign it. Keep the canvas 16:9 and keep the book, central gutter, curled brown leather binding, desk objects, golden sunlight, torn ingredient paper, experiment paper, salmon mother-letter note, red underline, taped tilted food photograph, right recipe illustration frame, five translucent rows, coin/document/chart/calendar/moon pictograms, and ornate navy-and-gold bottom button at their original normalized positions.
Change only: carefully remove ALL lettering and ALL numerals everywhere, including top navigation and status figures, left calligraphy and prose, ingredient labels, experimental notes, arrows' annotations, letter, right headings/descriptions/row labels/prices and the button caption. Reconstruct their underlying textured parchment or navy fill without covering them with flat new cards. Preserve all pictures, pressed leaves, tiny floral motifs, adhesive tape, pictograms, red underlines, decorative border corner flourishes, paper grain, edge damage, lighting, and navy/gold button outline.
Layout invariants: original full-image book spans about x100–1740 and y115–1010 on the 1818x1023 reference; preserve these proportions. Left tilted photograph at x615–958 y173–461; ingredients at x282–746 y492–606; small cutaway food drawing near x683–814 y640–756; right food illustration at x1340–1630 y248–473. The live Unity labels will be laid over the now-empty text areas, so those areas must remain clean and textured.
Constraints: same image and same arrangement, no new white or beige rectangular cards, no generic blank new book, no changed camera angle or zoom, no invented UI panels, no lettering whatsoever, no watermarks. Full opaque image, no transparent background. Food edit only in existing picture areas: replace both original mugwort platters and the small cutaway with tender rice cakes containing cooked soybean pieces, lightly dusted with soybean powder, on the same wooden platter, one cut to reveal the soft soybean/rice interior. Replace ingredient pictures in the same positions with rice-flour bowl, cooked soybeans, and a small roasted soybean-powder bowl. Keep photograph frames, notes, plate placement, materials, all layout geometry and lighting unchanged. Do not add text.
```

### Royal

```text
Use case: precise-object-edit
Asset type: production Unity recipe-book UI artwork, edited from the supplied full reference.
Input image role: EDIT TARGET, keep the provided image's exact composition and visual identity.
Primary request: prepare this same richly aged Korean mother's scrapbook UI as a clean textless interactive game background. Do not redesign it. Keep the canvas 16:9 and keep the book, central gutter, curled brown leather binding, desk objects, golden sunlight, torn ingredient paper, experiment paper, salmon mother-letter note, red underline, taped tilted food photograph, right recipe illustration frame, five translucent rows, coin/document/chart/calendar/moon pictograms, and ornate navy-and-gold bottom button at their original normalized positions.
Change only: carefully remove ALL lettering and ALL numerals everywhere, including top navigation and status figures, left calligraphy and prose, ingredient labels, experimental notes, arrows' annotations, letter, right headings/descriptions/row labels/prices and the button caption. Reconstruct their underlying textured parchment or navy fill without covering them with flat new cards. Preserve all pictures, pressed leaves, tiny floral motifs, adhesive tape, pictograms, red underlines, decorative border corner flourishes, paper grain, edge damage, lighting, and navy/gold button outline.
Layout invariants: original full-image book spans about x100–1740 and y115–1010 on the 1818x1023 reference; preserve these proportions. Left tilted photograph at x615–958 y173–461; ingredients at x282–746 y492–606; small cutaway food drawing near x683–814 y640–756; right food illustration at x1340–1630 y248–473. The live Unity labels will be laid over the now-empty text areas, so those areas must remain clean and textured.
Constraints: same image and same arrangement, no new white or beige rectangular cards, no generic blank new book, no changed camera angle or zoom, no invented UI panels, no lettering whatsoever, no watermarks. Full opaque image, no transparent background. Food edit only in existing picture areas: replace both original mugwort platters and the small cutaway with elegant square Korean royal rice cakes decorated with jujube slices, chestnut and pine nuts, on the same wooden platter. One cut shows varied rice/jujube/chestnut ingredients. Replace ingredient pictures in the same positions with rice-flour bowl and little clusters of jujubes, chestnuts and pine nuts. Keep photograph frames, notes, plate placement, materials, all layout geometry and lighting unchanged. Do not add text.
```

