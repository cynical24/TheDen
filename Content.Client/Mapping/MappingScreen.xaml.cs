﻿using System.Linq;
using System.Numerics;
using Content.Client.Decals;
using Content.Client.Decals.UI;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Decals;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Serilog;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Mapping;

[GenerateTypedNameReferences]
public sealed partial class MappingScreen : InGameScreen
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public DecalPlacementSystem DecalSystem = default!;

    private PaletteColorPicker? _picker;

    private ProtoId<DecalPrototype>? _id;
    private FloatSpinBox RotationSpinBox;
    public Color DecalColor { get; private set; } = Color.White;
    private bool _decalEnableColor;
    private float _decalRotation;
    private bool _decalSnap;
    private int _decalZIndex;
    private bool _decalCleanable;

    private bool _decalAuto;

    public override ChatBox ChatBox => GetWidget<ChatBox>()!;

    public MappingScreen()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        var visibilityUIController = UserInterfaceManager.GetUIController<MappingVisibilityUIController>();

        AutoscaleMaxResolution = new Vector2i(1080, 770);

        SetAnchorPreset(LeftContainer, LayoutPreset.Wide);
        SetAnchorPreset(ViewportContainer, LayoutPreset.Wide);
        SetAnchorPreset(SpawnContainer, LayoutPreset.Wide);
        SetAnchorPreset(MainViewport, LayoutPreset.Wide);
        SetAnchorAndMarginPreset(Hotbar, LayoutPreset.BottomWide, margin: 5);
        SetAnchorAndMarginPreset(Actions, LayoutPreset.TopWide, margin: 5);

        LeftContainer.OnSplitResizeFinished += () =>
            OnChatResized?.Invoke(new Vector2(LeftContainer.SplitFraction, 0));

        RotationSpinBox = new FloatSpinBox(90.0f, 0)
        {
            HorizontalExpand = true
        };
        DecalSpinBoxContainer.AddChild(RotationSpinBox);

        DecalColorPicker.OnColorChanged += OnDecalColorPicked;
        DecalPickerOpen.OnPressed += OnDecalPickerOpenPressed;
        RotationSpinBox.OnValueChanged += args =>
        {
            _decalRotation = args.Value;
            UpdateDecal();
        };
        DecalEnableAuto.OnToggled += args =>
        {
            _decalAuto = args.Pressed;
            if (_id is { } id)
                SelectDecal(id);
        };
        DecalEnableColor.OnToggled += args =>
        {
            _decalEnableColor = args.Pressed;
            UpdateDecal();
            RefreshDecalList();
        };
        DecalEnableSnap.OnToggled += args =>
        {
            _decalSnap = args.Pressed;
            UpdateDecal();
        };
        DecalEnableCleanable.OnToggled += args =>
        {
            _decalCleanable = args.Pressed;
            UpdateDecal();
        };
        DecalZIndexSpinBox.ValueChanged += args =>
        {
            _decalZIndex = args.Value;
            UpdateDecal();
        };

        for (var i = 0; i < EntitySpawnWindow.InitOpts.Length; i++)
        {
            EntityPlacementMode.AddItem(EntitySpawnWindow.InitOpts[i], i);
        }

        Pick.Texture.TexturePath = "/Textures/Interface/eyedropper.svg.png";
        PickDecal.Texture.TexturePath = "/Textures/Interface/VerbIcons/wand-magic-sparkles-solid.svg.192dpi.png";
        Flip.Texture.TexturePath = "/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png";
        Flip.OnPressed += _ => FlipSides();
        Visibility.Texture.TexturePath = "/Textures/Interface/VerbIcons/layer-group-solid.svg.192dpi.png";
        Visibility.OnPressed += _ => visibilityUIController.ToggleWindow();
        FixGridAtmos.Texture.TexturePath = "/Textures/Interface/VerbIcons/oxygen.svg.192dpi.png";
        RemoveGrid.Texture.TexturePath = "/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png";
        MoveGrid.Texture.TexturePath = "/Textures/Interface/VerbIcons/point.svg.192dpi.png";
        GridVV.Texture.TexturePath = "/Textures/Interface/VerbIcons/vv.svg.192dpi.png";
        PipesColor.Texture.TexturePath = "/Textures/Interface/VerbIcons/paint-roller-solid.svg.192dpi.png";
    }

    public void FlipSides()
    {
        LeftContainer.Flip();
        RightContainer.Flip();

        if (SpawnContainer.GetPositionInParent() == 0)
        {
            Flip.Texture.TexturePath = "/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png";
        }
        else
        {
            Flip.Texture.TexturePath = "/Textures/Interface/VerbIcons/rotate_ccw.svg.192dpi.png";
        }
    }

    private void OnDecalColorPicked(Color color)
    {
        DecalColor = color;
        DecalColorPicker.Color = color;
        UpdateDecal();
        RefreshDecalList();
    }

    private void OnDecalPickerOpenPressed(ButtonEventArgs obj)
    {
        if (_picker == null)
        {
            _picker = new PaletteColorPicker();
            _picker.OpenToLeft();
            _picker.PaletteList.OnItemSelected += args =>
            {
                var color = ((Color?) args.ItemList.GetSelected().First().Metadata)!.Value;
                OnDecalColorPicked(color);
            };

            return;
        }

        if (_picker.IsOpen)
            _picker.Close();
        else
            _picker.Open();
    }

    private void UpdateDecal()
    {
        if (_id is not { } id)
            return;

        DecalSystem.UpdateDecalInfo(id, _decalEnableColor ? DecalColor : Color.White, _decalRotation, _decalSnap, _decalZIndex, _decalCleanable);
    }

    public void SelectDecal(string decalId)
    {
        if (!_prototype.TryIndex<DecalPrototype>(decalId, out var decal))
            return;

        _id = decalId;

        if (_decalAuto)
        {
            _decalEnableColor = decal.DefaultCustomColor;
            _decalCleanable = decal.DefaultCleanable;
            _decalSnap = decal.DefaultSnap;

            DecalColorPicker.Color = DecalColor;
            DecalEnableCleanable.Pressed = _decalCleanable;
            DecalEnableSnap.Pressed = _decalSnap;
            DecalEnableColor.Pressed = _decalEnableColor;
        }

        UpdateDecal();
        RefreshDecalList();
    }

    public void SelectDecal(Decal decal)
    {
        if (!_decalAuto)
            return;

        _id = decal.Id;
        _decalCleanable = decal.Cleanable;

        if (decal.Color is { } color)
            DecalColor = color;
        else
            _decalEnableColor = false;

        DecalColorPicker.Color = DecalColor;
        DecalEnableCleanable.Pressed = _decalCleanable;
        DecalEnableSnap.Pressed = _decalSnap;
        DecalEnableColor.Pressed = _decalEnableColor;

        UpdateDecal();
        RefreshDecalList();
    }

    public void ChangeDecalRotation(float rotation)
    {
        _decalRotation += rotation;

        if (_decalRotation > 360)
            _decalRotation = 0;
        if (_decalRotation < 0)
            _decalRotation = 360;

        RotationSpinBox.Value = _decalRotation;
        UpdateDecal();
    }

    private void RefreshDecalList()
    {
        Decals.TexturesModulate = _decalEnableColor ? DecalColor : Color.White;
        var children = Decals.PrototypeList.Children.ToList().Union(Decals.SearchList.Children);
        foreach (var control in children)
        {
            if (control is not MappingSpawnButton button)
                continue;

            RefreshDecalButton(button);
        }
    }

    private void RefreshDecalButton(MappingSpawnButton button)
    {
        var children =
            button.ChildrenPrototypes.Children.ToList().Union(button.ChildrenPrototypesGallery.Children);

        foreach (var control in children)
        {
            if (control is not MappingSpawnButton { } childButton)
                continue;

            if (childButton.Texture.Visible)
                childButton.Texture.Modulate = _decalEnableColor ? DecalColor : Color.White;

            RefreshDecalButton(childButton);
        }
    }

    public override void SetChatSize(Vector2 size)
    {
        LeftContainer.ResizeMode = SplitContainer.SplitResizeMode.RespectChildrenMinSize;
    }

    public void UnPressActionsExcept(Control except)
    {
        Add.Pressed = Add == except;
        Fill.Pressed = Fill == except;
        Grab.Pressed = Grab == except;
        Move.Pressed = Move == except;
        Pick.Pressed = Pick == except;
        PickDecal.Pressed = PickDecal == except;
        FixGridAtmos.Pressed = FixGridAtmos == except;
        RemoveGrid.Pressed = RemoveGrid == except;
        MoveGrid.Pressed = MoveGrid == except;
        GridVV.Pressed = GridVV == except;
        PipesColor.Pressed = PipesColor == except;

        EraseEntityButton.Pressed = EraseEntityButton == except;
        EraseDecalButton.Pressed = EraseDecalButton == except;
        EraseTileButton.Pressed = EraseTileButton == except;
    }
}
