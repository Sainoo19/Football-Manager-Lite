using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class SpriteVisualPreviewRunner : Node
{
    private const string OutputDirectory = "res://.artifacts/test-reports/pitch-layers/v2";

    public override void _Ready()
    {
        Callable.From(() =>
        {
            _ = RunPreviewAsync();
        }).CallDeferred();
    }

    private async Task RunPreviewAsync()
    {
        try
        {
            Main main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            AddChild(main);
            main.ChooseSelectedTeam();
            main.ShowMatchView();
            main.MatchView.PrepareNewMatch();
            if (!OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                return;
            }
            if (DisplayServer.GetName() == "headless")
            {
                throw new InvalidOperationException("Visual capture needs a graphics renderer.");
            }
            await CaptureAsync("home-kit.png");
            OptionButton kitSelector = FindKitSelector(main)
                ?? throw new InvalidOperationException("The kit selector is missing.");
            kitSelector.Select(1);
            kitSelector.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            await CaptureAsync("away-kit.png");
            main.MatchView.PrepareScenario(MatchScenarioKind.ThreeAttackersVersusTwoDefenders);
            await ToSignal(GetTree().CreateTimer(0.6d), SceneTreeTimer.SignalName.Timeout);
            main.MatchView.PauseMatch();
            await CaptureAsync("moving-scenario.png");
            GD.Print($"PASS: visual sprite captures saved to {ProjectSettings.GlobalizePath(OutputDirectory)}");
            RemoveChild(main);
            main.QueueFree();
            AddChild(new GoalOcclusionPreviewRunner());
        }
        catch (Exception exception)
        {
            GD.PushError($"Sprite preview failed: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task CaptureAsync(string filename)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string directory = ProjectSettings.GlobalizePath(OutputDirectory);
        Error directoryError = DirAccess.MakeDirRecursiveAbsolute(directory);
        if (directoryError != Error.Ok)
        {
            throw new InvalidOperationException($"Cannot create capture folder: {directoryError}");
        }
        using Image screenshot = GetViewport().GetTexture().GetImage();
        Error saveError = screenshot.SavePng($"{directory}/{filename}");
        if (saveError != Error.Ok)
        {
            throw new InvalidOperationException($"Cannot save capture: {saveError}");
        }
    }

    private static OptionButton? FindKitSelector(Node node)
    {
        if (node is OptionButton option && option.ItemCount == 2 && option.GetItemText(0) == "Sân nhà")
        {
            return option;
        }
        foreach (Node child in node.GetChildren())
        {
            OptionButton? found = FindKitSelector(child);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }
}
