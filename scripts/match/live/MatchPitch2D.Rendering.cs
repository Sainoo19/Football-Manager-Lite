using Godot;

public partial class MatchPitch2D
{
    public override void _Draw()
    {
        if (Simulation is null)
            return;
        Rect2 field = new(new Vector2(18, 10), Size - new Vector2(36, 20));
        DrawRect(field, new Color("176b45"));
        float stripeWidth = field.Size.X / 12f;
        for (int i = 0; i < 12; i++)
        {
            if (i % 2 == 0)
                DrawRect(new Rect2(field.Position + new Vector2(stripeWidth * i, 0), new Vector2(stripeWidth, field.Size.Y)), new Color("1c764e"));
        }

        Color line = new(1, 1, 1, 0.66f);
        Vector2 center = field.GetCenter();
        DrawRect(field, line, false, 2);
        DrawLine(new Vector2(center.X, field.Position.Y), new Vector2(center.X, field.End.Y), line, 2);
        DrawArc(center, field.Size.Y * 0.18f, 0, Mathf.Tau, 48, line, 2);
        DrawCircle(center, 3, line);

        float penaltyWidth = field.Size.X * 0.165f;
        float penaltyHeight = field.Size.Y * 0.62f;
        DrawRect(new Rect2(new Vector2(field.Position.X, center.Y - penaltyHeight / 2), new Vector2(penaltyWidth, penaltyHeight)), line, false, 2);
        DrawRect(new Rect2(new Vector2(field.End.X - penaltyWidth, center.Y - penaltyHeight / 2), new Vector2(penaltyWidth, penaltyHeight)), line, false, 2);

        float goalAreaWidth = field.Size.X * 0.065f;
        float goalAreaHeight = field.Size.Y * 0.32f;
        DrawRect(new Rect2(new Vector2(field.Position.X, center.Y - goalAreaHeight / 2), new Vector2(goalAreaWidth, goalAreaHeight)), line, false, 2);
        DrawRect(new Rect2(new Vector2(field.End.X - goalAreaWidth, center.Y - goalAreaHeight / 2), new Vector2(goalAreaWidth, goalAreaHeight)), line, false, 2);

        Vector2 leftSpot = new(field.Position.X + field.Size.X * 0.115f, center.Y);
        Vector2 rightSpot = new(field.End.X - field.Size.X * 0.115f, center.Y);
        DrawCircle(leftSpot, 2.5f, line);
        DrawCircle(rightSpot, 2.5f, line);
        Vector2 arcRadius = new(field.Size.X * 0.087f, field.Size.Y * 0.135f);
        DrawEllipticalArc(leftSpot, arcRadius, -0.93f, 0.93f, line);
        DrawEllipticalArc(rightSpot, arcRadius, Mathf.Pi - 0.93f, Mathf.Pi + 0.93f, line);

        float goalHeight = field.Size.Y * 0.26f;
        DrawRect(new Rect2(new Vector2(field.Position.X - 8, center.Y - goalHeight / 2), new Vector2(8, goalHeight)), line, false, 2);
        DrawRect(new Rect2(new Vector2(field.End.X, center.Y - goalHeight / 2), new Vector2(8, goalHeight)), line, false, 2);

        foreach ((StringName playerId, Vector2 normalized) in CurrentPositions)
        {
            Vector2 point = ToFieldPoint(normalized, field);
            bool isHome = _playerTeams[playerId] == Simulation.home.team.id;
            Color color = isHome ? HomeColor : AwayColor;
            if (_playerRoles[playerId] == "GK")
                color = isHome ? new Color("f1c75b") : new Color("ec9f45");
            DrawCircle(point + new Vector2(1.5f, 2), 8.5f, new Color(0, 0, 0, 0.32f));
            DrawCircle(point, 8, color);
            DrawArc(point, 8, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.84f), 1.5f);
        }

        Vector2 ballPoint = ToFieldPoint(BallPosition, field);
        if (_ballActionActive)
            DrawLine(ballPoint, ToFieldPoint(_ballActionTo, field), new Color(1, 1, 1, 0.13f), 1);
        DrawCircle(ballPoint + new Vector2(1.5f, 2), 5, new Color(0, 0, 0, 0.38f));
        DrawCircle(ballPoint, 4.5f, BallColor);
        DrawArc(ballPoint, 4.5f, 0, Mathf.Tau, 20, new Color("27313d"), 1);
    }

    private void DrawEllipticalArc(Vector2 center, Vector2 radius, float start, float end, Color color)
    {
        var points = new Vector2[25];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Lerp(start, end, i / 24f);
            points[i] = center + new Vector2(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y);
        }
        DrawPolyline(points, color, 2, true);
    }

    private static Vector2 ToFieldPoint(Vector2 normalized, Rect2 field) =>
        field.Position + new Vector2(normalized.X * field.Size.X, normalized.Y * field.Size.Y);
}
