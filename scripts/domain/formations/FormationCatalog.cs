using Godot;
using Godot.Collections;

[GlobalClass]
public partial class FormationCatalog : RefCounted
{
    public Array<FormationDefinition> all() => new()
    {
        Create433(),
        Create4231(),
        Create442(),
        Create352(),
        Create4123()
    };

    public FormationDefinition find(StringName formationId)
    {
        foreach (FormationDefinition formation in all())
        {
            if (formation.id == formationId)
                return formation;
        }
        return Create433();
    }

    private static Dictionary Slot(string slotId, string role, float x, float y) => new()
    {
        { "id", new StringName(slotId) },
        { "role", role },
        { "x", x },
        { "y", y }
    };

    private static FormationDefinition Create433()
    {
        Array<Dictionary> slots = new()
        {
            Slot("gk", "GK", 0.50f, 0.90f),
            Slot("lb", "LB", 0.14f, 0.70f), Slot("lcb", "CB", 0.38f, 0.74f),
            Slot("rcb", "CB", 0.62f, 0.74f), Slot("rb", "RB", 0.86f, 0.70f),
            Slot("lcm", "CM", 0.25f, 0.48f), Slot("dm", "DM", 0.50f, 0.57f),
            Slot("rcm", "CM", 0.75f, 0.48f),
            Slot("lw", "LW", 0.18f, 0.23f), Slot("st", "ST", 0.50f, 0.16f),
            Slot("rw", "RW", 0.82f, 0.23f)
        };
        return new FormationDefinition().setup("4_3_3", "4-3-3", slots);
    }

    private static FormationDefinition Create4231()
    {
        Array<Dictionary> slots = new()
        {
            Slot("gk", "GK", 0.50f, 0.90f),
            Slot("lb", "LB", 0.14f, 0.70f), Slot("lcb", "CB", 0.38f, 0.74f),
            Slot("rcb", "CB", 0.62f, 0.74f), Slot("rb", "RB", 0.86f, 0.70f),
            Slot("ldm", "DM", 0.35f, 0.55f), Slot("rdm", "DM", 0.65f, 0.55f),
            Slot("lw", "LW", 0.18f, 0.34f), Slot("am", "AM", 0.50f, 0.36f),
            Slot("rw", "RW", 0.82f, 0.34f), Slot("st", "ST", 0.50f, 0.15f)
        };
        return new FormationDefinition().setup("4_2_3_1", "4-2-3-1", slots);
    }

    private static FormationDefinition Create442()
    {
        Array<Dictionary> slots = new()
        {
            Slot("gk", "GK", 0.50f, 0.90f),
            Slot("lb", "LB", 0.14f, 0.70f), Slot("lcb", "CB", 0.38f, 0.74f),
            Slot("rcb", "CB", 0.62f, 0.74f), Slot("rb", "RB", 0.86f, 0.70f),
            Slot("lm", "LW", 0.14f, 0.46f), Slot("lcm", "CM", 0.38f, 0.50f),
            Slot("rcm", "CM", 0.62f, 0.50f), Slot("rm", "RW", 0.86f, 0.46f),
            Slot("lst", "ST", 0.38f, 0.18f), Slot("rst", "ST", 0.62f, 0.18f)
        };
        return new FormationDefinition().setup("4_4_2", "4-4-2", slots);
    }

    private static FormationDefinition Create352()
    {
        Array<Dictionary> slots = new()
        {
            Slot("gk", "GK", 0.50f, 0.90f),
            Slot("lcb", "CB", 0.25f, 0.72f), Slot("cb", "CB", 0.50f, 0.76f),
            Slot("rcb", "CB", 0.75f, 0.72f),
            Slot("lwb", "LB", 0.10f, 0.48f), Slot("lcm", "CM", 0.33f, 0.50f),
            Slot("dm", "DM", 0.50f, 0.58f), Slot("rcm", "CM", 0.67f, 0.50f),
            Slot("rwb", "RB", 0.90f, 0.48f),
            Slot("lst", "ST", 0.38f, 0.18f), Slot("rst", "ST", 0.62f, 0.18f)
        };
        return new FormationDefinition().setup("3_5_2", "3-5-2", slots);
    }

    private static FormationDefinition Create4123()
    {
        Array<Dictionary> slots = new()
        {
            Slot("gk", "GK", 0.50f, 0.90f),
            Slot("lb", "LB", 0.14f, 0.70f), Slot("lcb", "CB", 0.38f, 0.74f),
            Slot("rcb", "CB", 0.62f, 0.74f), Slot("rb", "RB", 0.86f, 0.70f),
            Slot("dm", "DM", 0.50f, 0.58f),
            Slot("cm", "CM", 0.34f, 0.46f), Slot("am", "AM", 0.66f, 0.36f),
            Slot("lw", "LW", 0.16f, 0.22f), Slot("st", "ST", 0.50f, 0.16f),
            Slot("rw", "RW", 0.84f, 0.22f)
        };
        return new FormationDefinition().setup("4_1_2_3", "4-1-2-3", slots);
    }
}
