using Godot;
using System;

/*
 * MDControlExtensions
 *
 * Extension class to provide useful UI control methods
 */
public static class MDControlExtensions
{
    // Sets a uniform anchor
    public static void SetAnchor(this Control instance, float Anchor)
    {
        instance.SetAnchor(Anchor, Anchor, Anchor, Anchor);
    }
    
    // Set each anchor component individually
    public static void SetAnchor(this Control instance, float LeftAnchor, float TopAnchor, float RightAnchor, float BottomAnchor)
    {
        instance.SetAnchor(Margin.Left, LeftAnchor);
        instance.SetAnchor(Margin.Top, TopAnchor);
        instance.SetAnchor(Margin.Right, RightAnchor);
        instance.SetAnchor(Margin.Bottom, BottomAnchor);
    }
    
    // Sets a uniform margin
    public static void SetMargin(this Control instance, float InMargin)
    {
        instance.SetMargin(InMargin, InMargin, InMargin, InMargin);
    }
    
    // Sets a directionally uniform margin
    public static void SetMargin(this Control instance, float HorMargin, float VertMargin)
    {
        instance.SetMargin(HorMargin, VertMargin, HorMargin, VertMargin);
    }
    
    // Set each margin component individually
    public static void SetMargin(this Control instance, float LeftMargin, float TopMargin, float RightMargin, float BottomMargin)
    {
        instance.MarginLeft = LeftMargin;
        instance.MarginTop = TopMargin;
        instance.MarginRight = RightMargin;
        instance.MarginBottom = BottomMargin;
    }
}