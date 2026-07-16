using Godot;
using Godot.Collections;

[GlobalClass]
public partial class FormationDefinition : Resource
{
    [Export] public StringName id { get; set; } = new();
    [Export] public string display_name { get; set; } = "";
    [Export] public Array<Dictionary> slots { get; set; } = new();

    public FormationDefinition setup(
        StringName formationId,
        string formationName,
        Array<Dictionary> formationSlots)
    {
        id = formationId;
        display_name = formationName;
        slots = formationSlots;
        return this;
    }

    public bool has_slot(StringName slotId)
    {
        foreach (Dictionary slot in slots)
        {
            if (slot["id"].AsStringName() == slotId)
                return true;
        }
        return false;
    }

    public Dictionary get_slot(StringName slotId)
    {
        foreach (Dictionary slot in slots)
        {
            if (slot["id"].AsStringName() == slotId)
                return slot;
        }
        return new Dictionary();
    }
}
