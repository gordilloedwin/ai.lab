# Accessibility & Contrast – AI Lab Redesign

This document outlines accessibility considerations for the new UI design system.

## Color Tokens Contrast (Approximate WCAG 2.1 AA)
Foreground vs background contrast (using representative hex approximations):
- Primary text (#E2E8F0) on surface (#121820): Contrast > 7:1 (AA/AAA pass).
- Muted text (#94A3B8) on surface (#121820): Contrast ~4.5:1 (AA pass for normal text threshold ~4.5).
- Faint text (#64748B) on surface (#121820): Contrast ~3.1:1 (Use only for supplementary non-essential text).
- Badge accent gradient (indigo/cyan) with white text (#FFFFFF): Both intermediate colors maintain >4.5:1 against gradient mid-tones (AA pass). Avoid small font sizes below 11px.
- Danger (#EF4444) on surface (#121820): Contrast ~4.5:1 (AA pass). For text over danger backgrounds ensure font-size >= 13px.

## Focus States
Interactive elements rely on a dual-layer focus style (border-color + glow using `--color-border-glow`). Provide visible non-color-only focus: future iteration can add outline thickness or underline.

## Motion Preferences
Animations respect `prefers-reduced-motion: reduce`; all non-essential transitions disabled for users who opt out.

## Keyboard Navigation
- Buttons and links use native focus order.
- Custom clickable cards avoided; buttons placed inside cards for actions.
- Ensure new components maintain accessible names (text inside buttons).

## Recommendations / TODO
- Add aria-live region for AI message streaming responses (future enhancement).
- Add skip link to jump from sidebar to main content.
- Ensure form validation errors are programmatically associated with inputs (aria-describedby) if expanded.

## Testing Checklist
1. Tab through sidebar and verify visible focus.
2. Use screen reader (NVDA) to read card room titles sequentially.
3. Check contrast with tooling (e.g. axe, WAVE) for primary surfaces.
4. Verify no flashing animations > 3 per second (none present).

## Future Enhancements
- Provide high contrast mode override (increase border brightness).
- Theme toggling (light mode) with automatic contrast recalculation.

---
Maintainer: Design system evolving; update this file with each token change.
