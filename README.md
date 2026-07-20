# AdvancedControls

HTML5 / CSS3 스타일의 WinForms 커스텀 컨트롤 라이브러리입니다. 표준 컨트롤을 색만 바꿔 쓰는 대신 전부 직접 그려서, border-radius · box-shadow · gradient · transition · placeholder 같은 웹의 표현을 GDI+로 옮겼습니다.

.NET Framework 4.8, 외부 의존성 없음. 컨트롤 14종(입력 · 선택 · 표시 · 컨테이너)과 라이트/다크 테마를 제공합니다.

컨트롤마다 테마 색을 그대로 따르되, 필요하면 디자이너에서 색을 개별로 덮어쓸 수 있습니다. 속성 창의 `AdvancedControlOptions › Palette`에서 강조색(Accent) · 면색(Surface) · 테두리 · 글자색 등을 지정하면 그 색만 테마 대신 쓰이고, 비워 두면 테마를 따릅니다. 모서리 반경 · 테두리 두께 · 선 모양(실선/점선)은 `AdvancedControlOptions › Styling`에서 조정합니다. 체크박스와 라디오 버튼은 `ButtonStyle`을 켜면 Bootstrap의 버튼형 선택 컨트롤처럼 눌리는 버튼 모양으로 그려집니다.

**학습과 실험을 목적으로 만든 테스트 프로젝트입니다.** 실제 서비스 환경에서의 사용은 검증되지 않았습니다.

MIT 라이선스입니다. 자세한 내용은 `LICENSE`를 참고해 주세요. 이 라이브러리를 사용해 발생한 모든 결과에 대한 책임은 사용자에게 있습니다.

---

## English

A WinForms custom control library with HTML5 / CSS3 styling. Every control is drawn from scratch rather than recoloring the standard ones, bringing web idioms — border-radius, box-shadow, gradient, transition, placeholder — over to GDI+.

.NET Framework 4.8, no external dependencies. 14 controls (input, selection, display, container) with light and dark themes.

Every control follows the theme by default, but any color can be overridden per control from the designer. Set an accent, surface, border, or text color under `AdvancedControlOptions › Palette` and only that color replaces the theme value; leave it blank to keep following the theme. Corner radius, border width, and dash style live under `AdvancedControlOptions › Styling`. Check boxes and radio buttons can be drawn as pressable buttons — Bootstrap's button-style toggles — by turning on `ButtonStyle`.

**This is a test project built for learning and experimentation.** It has not been validated for production use.

MIT licensed — see `LICENSE` for details. You assume all responsibility for any outcome of using this library.
