using System.Linq;
using Content.Shared.Targeting;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.UserInterface.Systems.PartStatus.Widgets;

[GenerateTypedNameReferences]
public sealed partial class PartStatusControl : UIWidget
{
    private readonly PartStatusUIController _controller;
    private readonly Dictionary<TargetBodyPart, TextureRect> _partStatusControls;

    public PartStatusControl()
    {
        RobustXamlLoader.Load(this);
        _controller = UserInterfaceManager.GetUIController<PartStatusUIController>();

        _bodyPartControls = new Dictionary<TargetBodyPart, (TextureButton, PanelContainer)>
        {
            { TargetBodyPart.Head, DollHead },
            { TargetBodyPart.Torso, DollTorso },
            { TargetBodyPart.LeftArm, DollLeftArm },
            { TargetBodyPart.RightArm, DollRightArm },
            { TargetBodyPart.LeftLeg, DollLeftLeg },
            { TargetBodyPart.RightLeg, DollRightLeg }
        };

        foreach (var buttonPair in _bodyPartControls)
        {
            buttonPair.Value.Button.MouseFilter = MouseFilterMode.Stop;
            buttonPair.Value.Button.OnPressed += args => SetActiveBodyPart(buttonPair.Key);
        }
    }
    private void SetActiveBodyPart(TargetBodyPart bodyPart)
    {
        _controller.CycleTarget(bodyPart, this);
    }

    public void SetColors(TargetBodyPart bodyPart)
    {
        foreach (var buttonPair in _bodyPartControls)
        {
            var styleBox = (StyleBoxFlat) buttonPair.Value.Panel.PanelOverride!;
            styleBox.BackgroundColor = buttonPair.Key == bodyPart ? new Color(243, 10, 12, 51) : new Color(0, 0, 0, 0);
            styleBox.BorderColor = buttonPair.Key == bodyPart ? new Color(243, 10, 12, 255) : new Color(0, 0, 0, 0);
        }
    }

    public void SetVisible(bool visible)
    {
        this.Visible = visible;
    }
}