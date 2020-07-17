using Godot;

namespace MD
{
    /// <summary>
    /// Extension class to provide useful UI control methods
    /// </summary>
    public static class MDControlExtensions
    {
        /// <summary>
        /// Sets a uniform anchor
        /// </summary>
        /// <param name="Instance">The control instance</param>
        /// <param name="Anchor">The anchor</param>
        public static void SetAnchor(this Control Instance, float Anchor)
        {
            Instance.SetAnchor(Anchor, Anchor, Anchor, Anchor);
        }

        /// <summary>
        /// Set each anchor component individually
        /// </summary>
        /// <param name="Instance">The instance</param>
        /// <param name="LeftAnchor">Left Anchor</param>
        /// <param name="TopAnchor">Top Anchor</param>
        /// <param name="RightAnchor">Right Anchor</param>
        /// <param name="BottomAnchor">Bottom Anchor</param>
        public static void SetAnchor(this Control Instance, float LeftAnchor, float TopAnchor, float RightAnchor,
            float BottomAnchor)
        {
            Instance.SetAnchor(Margin.Left, LeftAnchor);
            Instance.SetAnchor(Margin.Top, TopAnchor);
            Instance.SetAnchor(Margin.Right, RightAnchor);
            Instance.SetAnchor(Margin.Bottom, BottomAnchor);
        }

        /// <summary>
        /// Sets a uniform margin
        /// </summary>
        /// <param name="Instance">The control instance</param>
        /// <param name="InMargin">The margin</param>
        public static void SetMargin(this Control Instance, float InMargin)
        {
            Instance.SetMargin(InMargin, InMargin, InMargin, InMargin);
        }

        /// <summary>
        /// Sets a directionally uniform margin
        /// </summary>
        /// <param name="Instance">The control instance</param>
        /// <param name="HorMargin">Horizontal margin</param>
        /// <param name="VertMargin">Vertical margin</param>
        public static void SetMargin(this Control Instance, float HorMargin, float VertMargin)
        {
            Instance.SetMargin(HorMargin, VertMargin, HorMargin, VertMargin);
        }

        /// <summary>
        /// Set each margin component individually
        /// </summary>
        /// <param name="Instance">The control instance</param>
        /// <param name="LeftMargin">Left margin</param>
        /// <param name="TopMargin">Top margin</param>
        /// <param name="RightMargin">Right margin</param>
        /// <param name="BottomMargin">Bottom margin</param>
        public static void SetMargin(this Control Instance, float LeftMargin, float TopMargin, float RightMargin,
            float BottomMargin)
        {
            Instance.MarginLeft = LeftMargin;
            Instance.MarginTop = TopMargin;
            Instance.MarginRight = RightMargin;
            Instance.MarginBottom = BottomMargin;
        }
    }
}