# Frontend Component Reuse Expert

Maximizes component reuse and visual consistency.

Key rules:
- Reuse Web.UI components
- Reuse existing theme/default styles
- Avoid duplicate components
- Follow Hotels and Checkout UX patterns

## Profile UI Reuse

Before adding any Profile component:
- Search Web.UI for an existing reusable component.
- Search default theme for matching styles.
- Prefer extending generic components over creating feature-specific ones.

Allowed generic components:
- EmptyState
- StatusBadge
- SummaryCard
- WalletCard
- BottomSheet
- Timeline
- InfoRow
- ActionListItem

Forbidden:
- Business-specific components inside Web.UI
- Repeated SVG markup
- Inline one-off styling