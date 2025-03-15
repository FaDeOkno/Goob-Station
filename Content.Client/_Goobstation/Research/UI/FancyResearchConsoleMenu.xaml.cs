using System.Linq;
using System.Numerics;
using Content.Client.Research;
using Content.Client.UserInterface.Controls;
using Content.Shared._Goobstation.Research;
using Content.Shared.Access.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Goobstation.Research.UI;

[GenerateTypedNameReferences]
public sealed partial class FancyResearchConsoleMenu : FancyWindow
{
    public Action<string>? OnTechnologyCardPressed;
    public Action? OnServerButtonPressed;

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly ResearchSystem _research;
    private readonly SpriteSystem _sprite;
    private readonly AccessReaderSystem _accessReader;

    /// <summary>
    /// Console entity
    /// </summary>
    public EntityUid Entity;

    /// <summary>
    /// Currently selected discipline
    /// </summary>
    public ProtoId<TechDisciplinePrototype> CurrentDiscipline = "Industrial";

    /// <summary>
    /// All technologies and their availablity
    /// </summary>
    public Dictionary<string, ResearchAvailablity> List = new();

    /// <summary>
    /// Contains BUI state for some stuff
    /// </summary>
    private ResearchConsoleBoundInterfaceState _localState = new(0, new());

    /// <summary>
    /// Is tech currently being dragged
    /// </summary>
    private bool _draggin;

    /// <summary>
    /// Global position that all tech relates to.
    /// For dragging mostly
    /// </summary>
    private Vector2 _position = new Vector2(45, 250);

    public FancyResearchConsoleMenu()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        _research = _entity.System<ResearchSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _accessReader = _entity.System<AccessReaderSystem>();
        StaticSprite.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(new("_Goobstation/Interface/rnd-static.rsi"), "static"));

        ServerButton.OnPressed += _ => OnServerButtonPressed?.Invoke();
        DragContainer.OnKeyBindDown += OnKeybindDown;
        DragContainer.OnKeyBindUp += OnKeybindUp;
        RecenterButton.OnPressed += _ => Recenter();
    }

    public void SetEntity(EntityUid entity)
    {
        Entity = entity;
    }

    public void UpdatePanels(ResearchConsoleBoundInterfaceState state)
    {
        DragContainer.DisposeAllChildren();
        DisciplinesContainer.DisposeAllChildren();
        List = state.Researches;
        _localState = state;

        // Добавляем к верхней панели все дисциплины
        var disciplines = _prototype.EnumeratePrototypes<TechDisciplinePrototype>()
                .ToList()
                .OrderBy(x => x.UiName);

        foreach (var proto in disciplines)
        {
            var discipline = new DisciplineButton(proto)
            {
                ToggleMode = true,
                HorizontalExpand = true,
                VerticalExpand = true,
                MuteSounds = true,  // idk why, but when closed UI spams this buttons
                Text = Loc.GetString(proto.UiName),
                Margin = new(5)
            };

            discipline.SetClickPressed(proto.ID == CurrentDiscipline);
            DisciplinesContainer.AddChild(discipline);

            discipline.OnPressed += SelectDiscipline;
        }

        foreach (var tech in _prototype.EnumeratePrototypes<TechnologyPrototype>().Where(x => x.Discipline == CurrentDiscipline))
        {
            if (!List.ContainsKey(tech.ID))
                continue;

            var control = new FancyResearchConsoleItem(tech, _sprite, List[tech.ID]);
            DragContainer.AddChild(control);

            // Set position for all tech, relating to _position
            LayoutContainer.SetPosition(control, _position + tech.Position * 150);
            control.SelectAction += SelectTech;
        }
    }

    public void UpdateInformationPanel(ResearchConsoleBoundInterfaceState state)
    {
        var amountMsg = new FormattedMessage();
        amountMsg.AddMarkupOrThrow(Loc.GetString("research-console-menu-research-points-text",
            ("points", state.Points)));
        ResearchAmountLabel.SetMessage(amountMsg);

        if (!_entity.TryGetComponent(Entity, out TechnologyDatabaseComponent? database))
            return;

        TierDisplayContainer.DisposeAllChildren();
        foreach (var disciplineId in database.SupportedDisciplines)
        {
            var discipline = _prototype.Index<TechDisciplinePrototype>(disciplineId);
            var tier = _research.GetTierCompletionPercentage(database, discipline);

            // don't show tiers with no available tech
            if (tier == 0)
                continue;

            // i'm building the small-ass control here to spare me some mild annoyance in making a new file
            var texture = new TextureRect
            {
                TextureScale = new Vector2(2, 2),
                VerticalAlignment = VAlignment.Center
            };
            var label = new RichTextLabel();
            texture.Texture = _sprite.Frame0(discipline.Icon);
            label.SetMessage(Loc.GetString("research-console-tier-percentage", ("perc", tier)));

            var control = new BoxContainer
            {
                Children =
                {
                    texture,
                    label,
                    new Control
                    {
                        MinWidth = 10
                    }
                }
            };
            TierDisplayContainer.AddChild(control);
        }
    }

    #region Drag handle
    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_draggin)
        {
            _position += args.Relative;

            // Move all tech
            foreach (var child in DragContainer.Children)
            {
                LayoutContainer.SetPosition(child, child.Position + args.Relative);
            }
        }
    }

    /// <summary>
    /// Raised when LMB is pressed at <see cref="DragContainer"/>
    /// </summary>
    private void OnKeybindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.Use)
            _draggin = true;
    }

    /// <summary>
    /// Raised when LMB is unpressed at <see cref="DragContainer"/>
    /// </summary>
    private void OnKeybindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.Use)
            _draggin = false;
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return _draggin ? DragMode.None : base.GetDragModeFor(relativeMousePos);
    }
    #endregion

    /// <summary>
    /// Selects a tech prototype and opens info panel
    /// </summary>
    /// <param name="proto">Tech proto</param>
    /// <param name="availablity">Tech availablity</param>
    public void SelectTech(TechnologyPrototype proto, ResearchAvailablity availablity)
    {
        InfoContainer.DisposeAllChildren();
        if (!_player.LocalEntity.HasValue)
            return;

        var control = new FancyTechnologyInfoPanel(proto, _sprite, _accessReader.IsAllowed(_player.LocalEntity.Value, Entity), availablity);
        control.BuyAction += args => OnTechnologyCardPressed?.Invoke(args.ID);
        InfoContainer.AddChild(control);
    }

    /// <summary>
    /// Selects the discipline and updates visible tech
    /// </summary>
    public void SelectDiscipline(BaseButton.ButtonEventArgs args)
    {
        if (args.Button is not DisciplineButton discipline)
            return;
        var proto = discipline.Proto;

        CurrentDiscipline = proto.ID;
        discipline.SetClickPressed(false);
        UserInterfaceManager.ClickSound();
        UpdatePanels(_localState);
        Recenter();
    }

    /// <summary>
    /// Sets <see cref="_position"/> to its default value
    /// </summary>
    public void Recenter()
    {
        _position = new(45, 250);
        foreach (var item in DragContainer.Children)
        {
            if (item is not FancyResearchConsoleItem research)
                continue;
            LayoutContainer.SetPosition(item, _position + (research.Prototype.Position * 150));
        }
    }

    public override void Close()
    {
        base.Close();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        InfoContainer.DisposeAllChildren();

        foreach (var item in DisciplinesContainer.Children)
        {
            if (item is not DisciplineButton button)
                continue;
            button.OnPressed -= SelectDiscipline;
        }
    }

    private sealed partial class DisciplineButton(TechDisciplinePrototype proto) : Button
    {
        public TechDisciplinePrototype Proto = proto;
    }
}
