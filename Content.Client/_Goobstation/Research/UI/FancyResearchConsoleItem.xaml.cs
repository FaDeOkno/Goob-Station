using Content.Shared._Goobstation.Research;
using Content.Shared.Research.Prototypes;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Goobstation.Research.UI;

[GenerateTypedNameReferences]
public sealed partial class FancyResearchConsoleItem : LayoutContainer
{
    // Public fields
    public TechnologyPrototype Prototype;
    public Action<TechnologyPrototype, ResearchAvailability>? SelectAction;
    public ResearchAvailability Availability;

    // Some visuals
    public static readonly Color DefaultColor = Color.FromHex("#141F2F");
    public static readonly Color DefaultBorderColor = Color.FromHex("#4972A1");
    public static readonly Color DefaultHoveredColor = Color.FromHex("#4972A1");

    public Color Color = DefaultColor;
    public Color BorderColor = DefaultBorderColor;
    public Color HoveredColor = DefaultHoveredColor;

    public FancyResearchConsoleItem(TechnologyPrototype proto, SpriteSystem sprite, ResearchAvailability availability)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        Availability = availability;
        Prototype = proto;

        ResearchDisplay.Texture = sprite.Frame0(proto.Icon);
        Button.OnPressed += Selected;
        Button.OnDrawModeChanged += UpdateColor;

        (Color, HoveredColor, BorderColor) = availability switch
        {
            ResearchAvailability.Researched => (Color.PaleGreen, Color.PaleGreen, Color.LimeGreen),
            ResearchAvailability.Available => (Color.DarkOliveGreen, Color.PaleGreen, Color.LimeGreen),
            ResearchAvailability.Unavailable => (Color.DarkRed, Color.PaleVioletRed, Color.Crimson),
            _ => (Color.White, Color.White, Color.White)
        };

        UpdateColor();
    }

    private void UpdateColor()
    {
        var panel = (StyleBoxFlat) Panel.PanelOverride!;
        panel.BackgroundColor = Button.IsHovered ? HoveredColor : Color;

        panel.BorderColor = BorderColor;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        Button.OnPressed -= Selected;
    }

    private void Selected(BaseButton.ButtonEventArgs args)
    {
        SelectAction?.Invoke(Prototype, Availability);
    }
}

public sealed class DrawButton : Button
{
    public event Action? OnDrawModeChanged;

    public DrawButton()
    {
    }

    protected override void DrawModeChanged()
    {
        OnDrawModeChanged?.Invoke();
    }
}
