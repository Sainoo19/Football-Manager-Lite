using Godot;

public partial class DotNetTestRunner : Node
{
    public override void _Ready()
    {
        Callable.From(RunTests).CallDeferred();
    }
}
