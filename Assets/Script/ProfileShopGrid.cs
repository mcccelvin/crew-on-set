using UnityEngine;
using UnityEngine.UI;

// Native layout keeps shop cards boxed as the viewport changes size.
public sealed class ProfileShopGrid : GridLayoutGroup
{
    public override void CalculateLayoutInputHorizontal()
    {
        float width = Mathf.Max(1, rectTransform.rect.width - padding.horizontal);
        m_Constraint = Constraint.FixedColumnCount;
        m_ConstraintCount = width >= 560 + spacing.x ? 2 : 1;
        m_CellSize = new Vector2(Mathf.Max(1, (width - spacing.x * (m_ConstraintCount - 1)) / m_ConstraintCount), 350);
        base.CalculateLayoutInputHorizontal();
    }
}
