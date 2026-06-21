# design-to-ui-implementation.md

# Design To UI Implementation

Turns approved design screenshots into production UI.

Rules:
- Implement the provided design exactly.
- Do not redesign.
- Reuse default theme tokens and existing Web.UI components.
- Add new components only when they are generic and reusable.
- Add icons through theme/icon system, not repeated inline SVG.
- Match spacing, typography, border radius, shadows, status badges, empty states, and mobile layout.

Checklist:
- Compare every screen with design screenshots.
- Verify RTL layout.
- Verify mobile viewport.
- Verify loading, empty, success, error states.
- Verify disabled menu behavior.
- Verify no duplicate CSS/components.