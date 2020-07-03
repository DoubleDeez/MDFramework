using Godot;

/*
 * MDControlExtensions
 *
 * Extension class to provide useful UI control methods
 */
public static class MDControlExtensions
{
    // Sets a uniform anchor
    public static void SetAnchor(this Control Instance, float Anchor)
    {
        Instance.SetAnchor(Anchor, Anchor, Anchor, Anchor);
    }

    // Set each anchor component individually
    public static void SetAnchor(this Control Instance, float LeftAnchor, float TopAnchor, float RightAnchor,
        float BottomAnchor)
    {
        Instance.SetAnchor(Margin.Left, LeftAnchor);
        Instance.SetAnchor(Margin.Top, TopAnchor);
        Instance.SetAnchor(Margin.Right, RightAnchor);
        Instance.SetAnchor(Margin.Bottom, BottomAnchor);
    }

    // Sets a uniform margin
    public static void SetMargin(this Control Instance, float InMargin)
    {
        Instance.SetMargin(InMargin, InMargin, InMargin, InMargin);
    }

    // Sets a directionally uniform margin
    public static void SetMargin(this Control Instance, float HorMargin, float VertMargin)
    {
        Instance.SetMargin(HorMargin, VertMargin, HorMargin, VertMargin);
    }

    // Set each margin component individually
    public static void SetMargin(this Control Instance, float LeftMargin, float TopMargin, float RightMargin,
        float BottomMargin)
    {
        Instance.MarginLeft = LeftMargin;
        Instance.MarginTop = TopMargin;
        Instance.MarginRight = RightMargin;
        Instance.MarginBottom = BottomMargin;
    }
}